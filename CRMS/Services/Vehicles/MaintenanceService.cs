using System.Data;
using CRMS.Data;
using CRMS.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CRMS.Services.Vehicles;

public sealed class MaintenanceService : IMaintenanceService
{
    private readonly Func<CrmsContext> _contextFactory;
    private readonly Func<long?> _currentUserId;

    public MaintenanceService()
        : this(CrmsContextFactory.Create, () => CurrentUserService.Instance.UserId)
    {
    }

    public MaintenanceService(Func<CrmsContext> contextFactory, Func<long?> currentUserId)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _currentUserId = currentUserId ?? throw new ArgumentNullException(nameof(currentUserId));
    }

    public async Task<List<MaintenanceListItem>> GetMaintenanceRecordsAsync(MaintenanceFilter? filter = null)
    {
        filter ??= new MaintenanceFilter();
        using var db = _contextFactory();

        var query = db.MaintenanceRecords.AsNoTracking().AsQueryable();

        if (filter.VehicleId.HasValue && filter.VehicleId.Value > 0)
            query = query.Where(m => m.VehicleId == filter.VehicleId.Value);

        if (!string.IsNullOrWhiteSpace(filter.MaintenanceStatus) &&
            !string.Equals(filter.MaintenanceStatus, "All", StringComparison.OrdinalIgnoreCase))
        {
            var status = filter.MaintenanceStatus.Trim();
            query = query.Where(m => m.MaintenanceStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim();
            query = query.Where(m =>
                m.Description.Contains(kw) ||
                m.Vehicle.LicensePlate.Contains(kw) ||
                m.Vehicle.Brand.Contains(kw) ||
                m.Vehicle.Model.Contains(kw));
        }

        var rows = await query
            .OrderBy(m =>
                m.MaintenanceStatus == "InProgress" ? 0 :
                m.MaintenanceStatus == "Scheduled" ? 1 :
                m.MaintenanceStatus == "Completed" ? 2 : 3)
            .ThenByDescending(m => m.StartAt)
            .Select(m => new
            {
                m.MaintenanceId,
                m.VehicleId,
                m.Vehicle.LicensePlate,
                m.Vehicle.Brand,
                m.Vehicle.Model,
                CategoryName = m.Vehicle.Category.CategoryName,
                m.CreatedByUserId,
                CreatedByName = m.CreatedByUser.FullName,
                m.StartAt,
                m.EndAt,
                m.MaintenanceStatus,
                m.Description,
                m.Cost
            })
            .ToListAsync();

        return rows.Select(r => new MaintenanceListItem
        {
            MaintenanceId = r.MaintenanceId,
            VehicleId = r.VehicleId,
            LicensePlate = r.LicensePlate,
            Brand = r.Brand,
            Model = r.Model,
            CategoryName = r.CategoryName,
            CreatedByUserId = r.CreatedByUserId,
            CreatedByName = r.CreatedByName,
            StartAtLocal = AvailabilityRules.ToVietnamLocal(r.StartAt),
            EndAtLocal = AvailabilityRules.ToVietnamLocal(r.EndAt),
            MaintenanceStatus = r.MaintenanceStatus,
            Description = r.Description,
            Cost = r.Cost
        }).ToList();
    }

    public async Task<long> ScheduleMaintenanceAsync(MaintenanceScheduleRequest request)
    {
        var nowUtc = DateTime.UtcNow;
        var (normalized, startUtc, endUtc) = MaintenanceRules.ValidateSchedule(request, nowUtc);

        using var db = _contextFactory();
        var adminUserId = await RequireActiveAdminAsync(db);

        if (!normalized.StartImmediately)
        {
            return await ExecuteScheduleStoredProcAsync(db, adminUserId, normalized, startUtc, endUtc);
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        await AcquireVehicleLockAsync(db, tx, normalized.VehicleId);

        if (!await db.Vehicles.AnyAsync(v => v.VehicleId == normalized.VehicleId))
            throw new InvalidOperationException("Không tìm thấy phương tiện trong hệ thống.");

        await EnsureNoConflictsAsync(db, normalized.VehicleId, startUtc, endUtc, nowUtc, excludeMaintenanceId: null);

        var entity = new MaintenanceRecord
        {
            VehicleId = normalized.VehicleId,
            CreatedByUserId = adminUserId,
            StartAt = startUtc,
            EndAt = endUtc,
            MaintenanceStatus = "InProgress",
            Description = normalized.Description,
            Cost = normalized.Cost
        };

        db.MaintenanceRecords.Add(entity);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return entity.MaintenanceId;
    }

    public async Task UpdateMaintenanceAsync(MaintenanceUpdateRequest request)
    {
        using var db = _contextFactory();
        await RequireActiveAdminAsync(db);

        await using var tx = await db.Database.BeginTransactionAsync();

        var existing = await db.MaintenanceRecords
            .FirstOrDefaultAsync(m => m.MaintenanceId == request.MaintenanceId)
            ?? throw new InvalidOperationException($"Không tìm thấy phiếu bảo trì #{request.MaintenanceId}.");

        await AcquireVehicleLockAsync(db, tx, existing.VehicleId);

        // Reload current state under per-vehicle exclusive transaction lock
        await db.Entry(existing).ReloadAsync();

        var (normalized, startUtc, endUtc) = MaintenanceRules.ValidateUpdate(request, existing.MaintenanceStatus);

        if (normalized.MaintenanceStatus is "Scheduled" or "InProgress")
        {
            await EnsureNoConflictsAsync(
                db,
                existing.VehicleId,
                startUtc,
                endUtc,
                DateTime.UtcNow,
                excludeMaintenanceId: existing.MaintenanceId);
        }

        existing.StartAt = startUtc;
        existing.EndAt = endUtc;
        existing.Description = normalized.Description;
        existing.Cost = normalized.Cost;
        existing.MaintenanceStatus = normalized.MaintenanceStatus;

        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task ChangeStatusAsync(
        long maintenanceId,
        string nextStatus,
        decimal? finalCost = null,
        string? updatedDescription = null)
    {
        MaintenanceRecord snapshot;
        using (var db = _contextFactory())
        {
            snapshot = await db.MaintenanceRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MaintenanceId == maintenanceId)
                ?? throw new InvalidOperationException($"Không tìm thấy phiếu bảo trì #{maintenanceId}.");
        }

        var req = new MaintenanceUpdateRequest(
            MaintenanceId: maintenanceId,
            StartAtLocal: AvailabilityRules.ToVietnamLocal(snapshot.StartAt),
            EndAtLocal: AvailabilityRules.ToVietnamLocal(snapshot.EndAt),
            Description: string.IsNullOrWhiteSpace(updatedDescription) ? snapshot.Description : updatedDescription,
            Cost: finalCost ?? snapshot.Cost,
            MaintenanceStatus: nextStatus);

        await UpdateMaintenanceAsync(req);
    }

    private static async Task<long> ExecuteScheduleStoredProcAsync(
        CrmsContext db,
        long adminUserId,
        MaintenanceScheduleRequest normalized,
        DateTime startUtc,
        DateTime endUtc)
    {
        var conn = (SqlConnection)db.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;
        if (shouldClose)
            await conn.OpenAsync();

        try
        {
            await using var cmd = new SqlCommand("dbo.usp_ScheduleMaintenance", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add(new SqlParameter("@VehicleId", SqlDbType.BigInt) { Value = normalized.VehicleId });
            cmd.Parameters.Add(new SqlParameter("@CreatedByUserId", SqlDbType.BigInt) { Value = adminUserId });
            cmd.Parameters.Add(new SqlParameter("@StartAt", SqlDbType.DateTime2, 0) { Value = startUtc });
            cmd.Parameters.Add(new SqlParameter("@EndAt", SqlDbType.DateTime2, 0) { Value = endUtc });
            cmd.Parameters.Add(new SqlParameter("@Description", SqlDbType.NVarChar, 1500) { Value = normalized.Description });

            var outId = new SqlParameter("@MaintenanceId", SqlDbType.BigInt)
            {
                Direction = ParameterDirection.Output
            };
            cmd.Parameters.Add(outId);

            await cmd.ExecuteNonQueryAsync();
            var maintenanceId = Convert.ToInt64(outId.Value);

            if (normalized.Cost > 0m)
            {
                await using var costCmd = new SqlCommand(
                    "UPDATE dbo.MaintenanceRecords SET Cost = @Cost WHERE MaintenanceId = @Id",
                    conn);
                costCmd.Parameters.Add(new SqlParameter("@Cost", SqlDbType.Decimal)
                {
                    Precision = 18,
                    Scale = 2,
                    Value = normalized.Cost
                });
                costCmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.BigInt) { Value = maintenanceId });
                await costCmd.ExecuteNonQueryAsync();
            }

            return maintenanceId;
        }
        catch (SqlException ex) when (ex.Number is >= 51400 and <= 51406)
        {
            throw new InvalidOperationException(TranslateProcError(ex.Number), ex);
        }
        finally
        {
            if (shouldClose && conn.State == ConnectionState.Open)
                await conn.CloseAsync();
        }
    }

    private static string TranslateProcError(int sqlErrorNumber) =>
        sqlErrorNumber switch
        {
            51400 => "Khoảng thời gian bảo trì không hợp lệ (bắt đầu phải ở tương lai và kết thúc sau thời điểm bắt đầu).",
            51401 => "Chỉ Quản trị viên (Admin) đang hoạt động mới có quyền xếp lịch bảo trì.",
            51402 => "Không thể khóa giao dịch phương tiện lúc này, vui lòng thử lại.",
            51403 => "Không tìm thấy phương tiện trong hệ thống.",
            51404 => "Không thể xếp lịch bảo trì: phương tiện đã có đơn thuê (Đang sử dụng / Đã xác nhận / Giữ chỗ chờ cọc) trùng khoảng thời gian.",
            51405 => "Không thể xếp lịch bảo trì: phương tiện đã có lịch bảo trì khác chồng lấn trong khoảng thời gian này.",
            51406 => "Không thể xếp lịch bảo trì: phương tiện đang bị khóa lịch trong khoảng thời gian này.",
            _ => "Không thể lưu lịch bảo trì."
        };

    private static async Task EnsureNoConflictsAsync(
        CrmsContext db,
        long vehicleId,
        DateTime startUtc,
        DateTime endUtc,
        DateTime nowUtc,
        long? excludeMaintenanceId)
    {
        var hasBookingConflict = await db.Bookings.AnyAsync(b =>
            b.VehicleId == vehicleId &&
            (
                (b.BookingStatus == "InUse" && b.StartAt < endUtc) ||
                (b.BookingStatus == "Confirmed" && b.StartAt < endUtc && b.EndAt > startUtc) ||
                (b.BookingStatus == "PendingDeposit" && b.DepositDeadline > nowUtc && b.StartAt < endUtc && b.EndAt > startUtc)
            ));

        if (hasBookingConflict)
        {
            throw new InvalidOperationException(
                "Không thể lưu lịch bảo trì: phương tiện đã có đơn thuê (Đang sử dụng / Đã xác nhận / Giữ chỗ chờ cọc) trùng khoảng thời gian.");
        }

        var hasMaintConflict = await db.MaintenanceRecords.AnyAsync(m =>
            m.VehicleId == vehicleId &&
            (!excludeMaintenanceId.HasValue || m.MaintenanceId != excludeMaintenanceId.Value) &&
            (m.MaintenanceStatus == "Scheduled" || m.MaintenanceStatus == "InProgress") &&
            m.StartAt < endUtc &&
            m.EndAt > startUtc);

        if (hasMaintConflict)
        {
            throw new InvalidOperationException(
                "Không thể lưu lịch bảo trì: phương tiện đã có lịch bảo trì khác chồng lấn trong khoảng thời gian này.");
        }

        var hasCalendarBlock = await db.VehicleCalendarBlocks.AnyAsync(block =>
            block.VehicleId == vehicleId &&
            block.IsActive &&
            block.StartAt < endUtc &&
            block.EndAt > startUtc);

        if (hasCalendarBlock)
        {
            throw new InvalidOperationException(
                "Không thể lưu lịch bảo trì: phương tiện đang bị khóa lịch trong khoảng thời gian này.");
        }
    }

    private static async Task AcquireVehicleLockAsync(CrmsContext db, IDbContextTransaction tx, long vehicleId)
    {
        var conn = (SqlConnection)db.Database.GetDbConnection();
        await using var cmd = new SqlCommand("sys.sp_getapplock", conn, (SqlTransaction)tx.GetDbTransaction())
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@Resource", $"CRMS_VEHICLE_{vehicleId}");
        cmd.Parameters.AddWithValue("@LockMode", "Exclusive");
        cmd.Parameters.AddWithValue("@LockOwner", "Transaction");
        cmd.Parameters.AddWithValue("@LockTimeout", 10000);

        var returnParam = new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.ReturnValue };
        cmd.Parameters.Add(returnParam);

        await cmd.ExecuteNonQueryAsync();
        if (Convert.ToInt32(returnParam.Value) < 0)
            throw new InvalidOperationException("Không thể khóa giao dịch phương tiện lúc này, vui lòng thử lại.");
    }

    private async Task<long> RequireActiveAdminAsync(CrmsContext db)
    {
        var userId = _currentUserId();
        if (!userId.HasValue)
            throw new InvalidOperationException("Vui lòng đăng nhập tài khoản Quản trị viên (Admin).");

        var isAdmin = await db.Users
            .AsNoTracking()
            .AnyAsync(u => u.UserId == userId.Value && u.IsActive && u.UserRole == "Admin");

        if (!isAdmin)
            throw new InvalidOperationException("Chỉ Quản trị viên (Admin) đang hoạt động mới có quyền quản lý bảo trì.");

        return userId.Value;
    }
}
