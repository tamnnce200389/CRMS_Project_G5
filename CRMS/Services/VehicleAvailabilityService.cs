using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRMS.Data;
using CRMS.Models;
using CRMS.Services.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Services
{
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
            if (contextFactory == null)
            {
                throw new ArgumentNullException(nameof(contextFactory));
            }
            if (currentUserId == null)
            {
                throw new ArgumentNullException(nameof(currentUserId));
            }

            _contextFactory = contextFactory;
            _currentUserId = currentUserId;
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
                        (m.MaintenanceStatus == "Scheduled" || m.MaintenanceStatus == "InProgress")))
                .Where(v => !context.VehicleCalendarBlocks.Any(block =>
                    block.VehicleId == v.VehicleId && block.IsActive &&
                    block.StartAt < endUtc && block.EndAt > startUtc));
        }

        public async Task<HashSet<long>> GetAvailableVehicleIdsAsync(DateTime startLocal, DateTime endLocal)
        {
            DateTime nowUtc = DateTime.UtcNow;
            var period = AvailabilityRules.ValidatePeriod(startLocal, endLocal, nowUtc, true);

            using (CrmsContext context = _contextFactory())
            {
                List<long> vehicleIds = await BuildAvailableQuery(context, period.StartUtc, period.EndUtc, nowUtc)
                    .Select(v => v.VehicleId)
                    .ToListAsync();

                return new HashSet<long>(vehicleIds);
            }
        }

        public async Task<bool> IsAvailableAsync(long vehicleId, DateTime startLocal, DateTime endLocal)
        {
            DateTime nowUtc = DateTime.UtcNow;
            var period = AvailabilityRules.ValidatePeriod(startLocal, endLocal, nowUtc, true);

            using (CrmsContext context = _contextFactory())
            {
                return await BuildAvailableQuery(context, period.StartUtc, period.EndUtc, nowUtc)
                    .AnyAsync(v => v.VehicleId == vehicleId);
            }
        }

        public async Task<VehicleAvailabilityResult> CheckAvailabilityAsync(
            long vehicleId,
            DateTime startAt,
            DateTime endAt,
            bool requireFutureStart = true)
        {
            DateTime nowUtc = DateTime.UtcNow;
            var period = AvailabilityRules.ValidatePeriod(startAt, endAt, nowUtc, requireFutureStart);

            List<VehicleAvailabilityResult> list = await CheckFleetAvailabilityAsync(
                startAt,
                endAt,
                null,
                vehicleId,
                true,
                requireFutureStart);

            if (list.Count > 0)
            {
                return list[0];
            }

            var notFoundConflicts = new List<AvailabilityConflict>
            {
                new AvailabilityConflict(
                    AvailabilityConflictType.VehicleNotFound,
                    "Không tìm thấy phương tiện",
                    $"Không tìm thấy phương tiện #{vehicleId} trong hệ thống.")
            };

            return new VehicleAvailabilityResult
            {
                VehicleId = vehicleId,
                LicensePlate = $"#{vehicleId}",
                Brand = "Không tìm thấy",
                Model = string.Empty,
                OperationalStatus = "Unknown",
                StartAtLocal = period.StartLocal,
                EndAtLocal = period.EndLocal,
                Conflicts = notFoundConflicts
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
            DateTime nowUtc = DateTime.UtcNow;
            var period = AvailabilityRules.ValidatePeriod(startAt, endAt, nowUtc, requireFutureStart);
            DateTime startUtc = period.StartUtc;
            DateTime endUtc = period.EndUtc;
            DateTime startLocal = period.StartLocal;
            DateTime endLocal = period.EndLocal;

            using (CrmsContext db = _contextFactory())
            {
                bool includeInternalRefs = await IsActiveManagementActorAsync(db);

                IQueryable<Vehicle> query = db.Vehicles.AsNoTracking().AsQueryable();

                if (vehicleId.HasValue)
                {
                    query = query.Where(v => v.VehicleId == vehicleId.Value);
                }

                if (categoryId.HasValue && categoryId.Value > 0)
                {
                    query = query.Where(v => v.CategoryId == categoryId.Value);
                }

                if (!includeSuspendedVehicles)
                {
                    query = query.Where(v => v.OperationalStatus == "Active");
                }

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
                {
                    return new List<VehicleAvailabilityResult>();
                }

                List<long> targetIds = vehicles.Select(v => v.VehicleId).ToList();

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
                        b.BookingCode,
                        b.BookingStatus,
                        b.StartAt,
                        b.EndAt,
                        b.DepositDeadline
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
                        m.MaintenanceId,
                        m.MaintenanceStatus,
                        m.StartAt,
                        m.EndAt,
                        m.Description
                    })
                    .ToListAsync();

                List<VehicleCalendarBlock> calendarBlocks = await db.VehicleCalendarBlocks
                    .AsNoTracking()
                    .Where(block => targetIds.Contains(block.VehicleId) && block.IsActive &&
                        block.StartAt < endUtc && block.EndAt > startUtc)
                    .OrderBy(block => block.StartAt)
                    .ToListAsync();

                var results = new List<VehicleAvailabilityResult>(vehicles.Count);
                foreach (var v in vehicles)
                {
                    List<BookingConflictSnapshot> vBookings = candidateBookings
                        .Where(x => x.VehicleId == v.VehicleId)
                        .Select(x => new BookingConflictSnapshot(
                            x.BookingCode,
                            x.BookingStatus,
                            x.StartAt,
                            x.EndAt,
                            x.DepositDeadline))
                        .ToList();

                    List<MaintenanceConflictSnapshot> vMaints = candidateMaintenances
                        .Where(x => x.VehicleId == v.VehicleId)
                        .Select(x => new MaintenanceConflictSnapshot(
                            x.MaintenanceId,
                            x.MaintenanceStatus,
                            x.StartAt,
                            x.EndAt,
                            x.Description))
                        .ToList();

                    List<AvailabilityConflict> conflicts = AvailabilityRules.EvaluateConflicts(
                        v.OperationalStatus,
                        v.IsCategoryActive,
                        v.CategoryName,
                        vBookings,
                        vMaints,
                        startUtc,
                        endUtc,
                        nowUtc,
                        includeInternalRefs);

                    foreach (VehicleCalendarBlock block in calendarBlocks.Where(b => b.VehicleId == v.VehicleId))
                    {
                        conflicts.Add(new AvailabilityConflict(
                            AvailabilityConflictType.CalendarBlock,
                            "Xe đã khóa lịch",
                            includeInternalRefs ? block.Reason : "Xe không nhận đặt trong khoảng thời gian này.",
                            AvailabilityRules.ToVietnamLocal(block.StartAt),
                            AvailabilityRules.ToVietnamLocal(block.EndAt),
                            includeInternalRefs ? $"LOCK-#{block.BlockId}" : null));
                    }

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
        }

        private async Task<bool> IsActiveManagementActorAsync(CrmsContext db)
        {
            long? userId = _currentUserId();
            if (!userId.HasValue)
            {
                return false;
            }

            return await db.Users
                .AsNoTracking()
                .AnyAsync(u =>
                    u.UserId == userId.Value &&
                    u.IsActive &&
                    (u.UserRole == "Admin" || u.UserRole == "Staff"));
        }
    }
}
