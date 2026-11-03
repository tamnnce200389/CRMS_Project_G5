using CRMS.Data;
using CRMS.Models;
using CRMS.Services.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Services;

public class VehicleAvailabilityService : IAvailabilityService
{
    private readonly Func<CrmsContext> _contextFactory;
    private readonly Func<long?> _currentUserId;

    public VehicleAvailabilityService()
        : this(CrmsContextFactory.Create, () => CurrentUserService.Instance.UserId)
    {
    }

    public VehicleAvailabilityService(Func<CrmsContext> contextFactory, Func<long?> currentUserId)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _currentUserId = currentUserId ?? throw new ArgumentNullException(nameof(currentUserId));
    }

    private static IQueryable<Vehicle> BuildAvailableQuery(
        CrmsContext context,
        DateTime startUtc,
        DateTime endUtc,
        DateTime nowUtc)
    {
        return context.Vehicles
            .AsNoTracking()
            .Where(v => v.OperationalStatus == "Active" && v.Category.IsActive)
            .Where(v =>
                !context.Bookings.Any(b =>
                    b.VehicleId == v.VehicleId &&
                    (
                        (b.BookingStatus == "InUse" && b.StartAt < endUtc) ||
                        (b.BookingStatus == "Confirmed" && b.StartAt < endUtc && b.EndAt > startUtc) ||
                        (b.BookingStatus == "PendingDeposit" && b.DepositDeadline > nowUtc && b.StartAt < endUtc && b.EndAt > startUtc)
                    )))
            .Where(v =>
                !context.MaintenanceRecords.Any(m =>
                    m.VehicleId == v.VehicleId &&
                    m.StartAt < endUtc &&
                    m.EndAt > startUtc &&
                    (m.MaintenanceStatus == "Scheduled" || m.MaintenanceStatus == "InProgress")));
    }

    public async Task<HashSet<long>> GetAvailableVehicleIdsAsync(DateTime startLocal, DateTime endLocal)
    {
        var nowUtc = DateTime.UtcNow;
        var (startUtc, endUtc, _, _) = AvailabilityRules.ValidatePeriod(startLocal, endLocal, nowUtc, requireFutureStart: true);

        using var context = _contextFactory();
        var vehicleIds = await BuildAvailableQuery(context, startUtc, endUtc, nowUtc)
            .Select(v => v.VehicleId)
            .ToListAsync();

        return vehicleIds.ToHashSet();
    }

    public async Task<bool> IsAvailableAsync(long vehicleId, DateTime startLocal, DateTime endLocal)
    {
        var nowUtc = DateTime.UtcNow;
        var (startUtc, endUtc, _, _) = AvailabilityRules.ValidatePeriod(startLocal, endLocal, nowUtc, requireFutureStart: true);

        using var context = _contextFactory();
        return await BuildAvailableQuery(context, startUtc, endUtc, nowUtc)
            .AnyAsync(v => v.VehicleId == vehicleId);
    }

    public async Task<VehicleAvailabilityResult> CheckAvailabilityAsync(
        long vehicleId,
        DateTime startAt,
        DateTime endAt,
        bool requireFutureStart = true)
    {
        var nowUtc = DateTime.UtcNow;
        var (_, _, startLocal, endLocal) = AvailabilityRules.ValidatePeriod(startAt, endAt, nowUtc, requireFutureStart);

        var list = await CheckFleetAvailabilityAsync(
            startAt,
            endAt,
            categoryId: null,
            vehicleId: vehicleId,
            includeSuspendedVehicles: true,
            requireFutureStart: requireFutureStart);

        if (list.Count > 0)
            return list[0];

        return new VehicleAvailabilityResult
        {
            VehicleId = vehicleId,
            LicensePlate = $"#{vehicleId}",
            Brand = "Không tìm thấy",
            Model = string.Empty,
            OperationalStatus = "Unknown",
            StartAtLocal = startLocal,
            EndAtLocal = endLocal,
            Conflicts = new[]
            {
                new AvailabilityConflict(
                    AvailabilityConflictType.VehicleNotFound,
                    "Không tìm thấy phương tiện",
                    $"Không tìm thấy phương tiện #{vehicleId} trong hệ thống.")
            }
        };
    }

    public async Task<List<VehicleAvailabilityResult>> CheckFleetAvailabilityAsync(
        DateTime startAt,
        DateTime endAt,
        int? categoryId = null,
        long? vehicleId = null,
        bool includeSuspendedVehicles = true,
        bool requireFutureStart = false)
    {
        var nowUtc = DateTime.UtcNow;
        var (startUtc, endUtc, startLocal, endLocal) =
            AvailabilityRules.ValidatePeriod(startAt, endAt, nowUtc, requireFutureStart);

        using var db = _contextFactory();
        var includeInternalRefs = await IsActiveManagementActorAsync(db);

        var query = db.Vehicles.AsNoTracking().AsQueryable();

        if (vehicleId.HasValue)
            query = query.Where(v => v.VehicleId == vehicleId.Value);

        if (categoryId.HasValue && categoryId.Value > 0)
            query = query.Where(v => v.CategoryId == categoryId.Value);

        if (!includeSuspendedVehicles)
            query = query.Where(v => v.OperationalStatus == "Active");

        var vehicles = await query
            .OrderBy(v => v.OperationalStatus == "Active" ? 0 : 1)
            .ThenBy(v => v.Brand)
            .ThenBy(v => v.Model)
            .ThenBy(v => v.LicensePlate)
            .Select(v => new
            {
                v.VehicleId,
                v.CategoryId,
                CategoryName = v.Category.CategoryName,
                IsCategoryActive = v.Category.IsActive,
                v.LicensePlate,
                v.Brand,
                v.Model,
                v.SeatCount,
                v.Transmission,
                v.FuelType,
                v.DailyRate,
                v.OperationalStatus,
                PrimaryImageUrl = v.VehicleImages
                    .OrderByDescending(i => i.IsPrimary)
                    .ThenBy(i => i.SortOrder)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault()
            })
            .ToListAsync();

        if (vehicles.Count == 0)
            return new List<VehicleAvailabilityResult>();

        var targetIds = vehicles.Select(v => v.VehicleId).ToList();

        var candidateBookings = await db.Bookings
            .AsNoTracking()
            .Where(b =>
                targetIds.Contains(b.VehicleId) &&
                b.StartAt < endUtc &&
                (
                    b.BookingStatus == "InUse" ||
                    (b.BookingStatus == "Confirmed" && b.EndAt > startUtc) ||
                    (b.BookingStatus == "PendingDeposit" && b.DepositDeadline > nowUtc && b.EndAt > startUtc)
                ))
            .Select(b => new
            {
                b.VehicleId,
                Snapshot = new BookingConflictSnapshot(
                    b.BookingCode,
                    b.BookingStatus,
                    b.StartAt,
                    b.EndAt,
                    b.DepositDeadline)
            })
            .ToListAsync();

        var candidateMaintenances = await db.MaintenanceRecords
            .AsNoTracking()
            .Where(m =>
                targetIds.Contains(m.VehicleId) &&
                m.StartAt < endUtc &&
                m.EndAt > startUtc &&
                (m.MaintenanceStatus == "Scheduled" || m.MaintenanceStatus == "InProgress"))
            .Select(m => new
            {
                m.VehicleId,
                Snapshot = new MaintenanceConflictSnapshot(
                    m.MaintenanceId,
                    m.MaintenanceStatus,
                    m.StartAt,
                    m.EndAt,
                    m.Description)
            })
            .ToListAsync();

        var bookingsByVehicle = candidateBookings
            .GroupBy(x => x.VehicleId)
            .ToDictionary(g => g.Key, g => (IEnumerable<BookingConflictSnapshot>)g.Select(x => x.Snapshot).ToList());

        var maintByVehicle = candidateMaintenances
            .GroupBy(x => x.VehicleId)
            .ToDictionary(g => g.Key, g => (IEnumerable<MaintenanceConflictSnapshot>)g.Select(x => x.Snapshot).ToList());

        var results = new List<VehicleAvailabilityResult>(vehicles.Count);
        foreach (var v in vehicles)
        {
            var vBookings = bookingsByVehicle.TryGetValue(v.VehicleId, out var bl)
                ? bl
                : Array.Empty<BookingConflictSnapshot>();

            var vMaints = maintByVehicle.TryGetValue(v.VehicleId, out var ml)
                ? ml
                : Array.Empty<MaintenanceConflictSnapshot>();

            var conflicts = AvailabilityRules.EvaluateConflicts(
                v.OperationalStatus,
                v.IsCategoryActive,
                v.CategoryName,
                vBookings,
                vMaints,
                startUtc,
                endUtc,
                nowUtc,
                includeInternalRefs);

            results.Add(new VehicleAvailabilityResult
            {
                VehicleId = v.VehicleId,
                CategoryId = v.CategoryId,
                CategoryName = v.CategoryName,
                LicensePlate = v.LicensePlate,
                Brand = v.Brand,
                Model = v.Model,
                SeatCount = v.SeatCount,
                Transmission = v.Transmission,
                FuelType = v.FuelType,
                DailyRate = v.DailyRate,
                OperationalStatus = v.OperationalStatus,
                PrimaryImageUrl = v.PrimaryImageUrl,
                StartAtLocal = startLocal,
                EndAtLocal = endLocal,
                Conflicts = conflicts
            });
        }

        return results;
    }

    private async Task<bool> IsActiveManagementActorAsync(CrmsContext db)
    {
        var userId = _currentUserId();
        if (!userId.HasValue)
            return false;

        return await db.Users
            .AsNoTracking()
            .AnyAsync(u =>
                u.UserId == userId.Value &&
                u.IsActive &&
                (u.UserRole == "Admin" || u.UserRole == "Staff"));
    }
}
