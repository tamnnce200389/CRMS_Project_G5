using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRMS.Data;
using CRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Services.Vehicles
{
    public class MaintenanceService : IMaintenanceService
    {
        private readonly Func<CrmsContext> _contextFactory;
        private readonly Func<long?> _currentUserId;

        public MaintenanceService()
            : this(CrmsContextFactory.Create, () => CurrentUserService.Instance.UserId)
        {
        }

        public MaintenanceService(Func<CrmsContext> contextFactory, Func<long?> currentUserId)
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

        public async Task<List<MaintenanceListItem>> GetMaintenanceRecordsAsync(MaintenanceFilter? filter = null)
        {
            if (filter == null)
            {
                filter = new MaintenanceFilter();
            }

            using (CrmsContext db = _contextFactory())
            {
                IQueryable<MaintenanceRecord> query = db.MaintenanceRecords.AsNoTracking().AsQueryable();

                if (filter.VehicleId.HasValue && filter.VehicleId.Value > 0)
                {
                    query = query.Where(m => m.VehicleId == filter.VehicleId.Value);
                }

                if (!string.IsNullOrWhiteSpace(filter.MaintenanceStatus) &&
                    !string.Equals(filter.MaintenanceStatus, "All", StringComparison.OrdinalIgnoreCase))
                {
                    string status = filter.MaintenanceStatus.Trim();
                    query = query.Where(m => m.MaintenanceStatus == status);
                }

                if (!string.IsNullOrWhiteSpace(filter.Keyword))
                {
                    string kw = filter.Keyword.Trim();
                    query = query.Where(m =>
                        m.Description.Contains(kw) ||
                        m.Vehicle.LicensePlate.Contains(kw) ||
                        m.Vehicle.Brand.Contains(kw) ||
                        m.Vehicle.Model.Contains(kw));
                }

                // shortcut: caps history at 500 active/recent rows; add paging if archive grows larger
                int cap = filter.MaxRecords > 0 ? filter.MaxRecords : 500;

                var rows = await query
                    .OrderBy(m =>
                        m.MaintenanceStatus == "InProgress" ? 0 :
                        m.MaintenanceStatus == "Scheduled" ? 1 :
                        m.MaintenanceStatus == "Completed" ? 2 : 3)
                    .ThenByDescending(m => m.StartAt)
                    .Take(cap)
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
        }

        public async Task<long> ScheduleMaintenanceAsync(MaintenanceScheduleRequest request)
        {
            DateTime nowUtc = DateTime.UtcNow;
            DateTime startUtc;
            DateTime endUtc;
            MaintenanceScheduleRequest normalized = MaintenanceRules.ValidateSchedule(
                request,
                nowUtc,
                out startUtc,
                out endUtc);

            using (CrmsContext db = _contextFactory())
            {
                long adminUserId = await RequireActiveAdminAsync(db);

                using (var tx = await db.Database.BeginTransactionAsync())
                {
                    bool vehicleExists = await db.Vehicles.AnyAsync(v => v.VehicleId == normalized.VehicleId);
                    if (!vehicleExists)
                    {
                        throw new InvalidOperationException("Không tìm thấy phương tiện trong hệ thống.");
                    }

                    await EnsureNoConflictsAsync(db, normalized.VehicleId, startUtc, endUtc, nowUtc, null);

                    string status = normalized.StartImmediately ? "InProgress" : "Scheduled";
                    var entity = new MaintenanceRecord
                    {
                        VehicleId = normalized.VehicleId,
                        CreatedByUserId = adminUserId,
                        StartAt = startUtc,
                        EndAt = endUtc,
                        MaintenanceStatus = status,
                        Description = normalized.Description,
                        Cost = normalized.Cost
                    };

                    db.MaintenanceRecords.Add(entity);
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                    return entity.MaintenanceId;
                }
            }
        }

        public async Task UpdateMaintenanceAsync(MaintenanceUpdateRequest request)
        {
            using (CrmsContext db = _contextFactory())
            {
                await RequireActiveAdminAsync(db);

                using (var tx = await db.Database.BeginTransactionAsync())
                {
                    MaintenanceRecord? existing = await db.MaintenanceRecords
                        .FirstOrDefaultAsync(m => m.MaintenanceId == request.MaintenanceId);

                    if (existing == null)
                    {
                        throw new InvalidOperationException($"Không tìm thấy phiếu bảo trì #{request.MaintenanceId}.");
                    }

                    DateTime nowUtc = DateTime.UtcNow;
                    DateTime startUtc;
                    DateTime endUtc;
                    MaintenanceUpdateRequest normalized = MaintenanceRules.ValidateUpdate(
                        request,
                        existing.MaintenanceStatus,
                        nowUtc,
                        existing.StartAt,
                        out startUtc,
                        out endUtc);

                    if (normalized.MaintenanceStatus == "Scheduled" || normalized.MaintenanceStatus == "InProgress")
                    {
                        await EnsureNoConflictsAsync(
                            db,
                            existing.VehicleId,
                            startUtc,
                            endUtc,
                            nowUtc,
                            existing.MaintenanceId);
                    }

                    existing.StartAt = startUtc;
                    existing.EndAt = endUtc;
                    existing.Description = normalized.Description;
                    existing.Cost = normalized.Cost;
                    existing.MaintenanceStatus = normalized.MaintenanceStatus;

                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
            }
        }

        public async Task ChangeStatusAsync(
            long maintenanceId,
            string nextStatus,
            decimal? finalCost = null,
            string? updatedDescription = null)
        {
            MaintenanceRecord? snapshot;
            using (CrmsContext db = _contextFactory())
            {
                snapshot = await db.MaintenanceRecords
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.MaintenanceId == maintenanceId);
            }

            if (snapshot == null)
            {
                throw new InvalidOperationException($"Không tìm thấy phiếu bảo trì #{maintenanceId}.");
            }

            string desc = string.IsNullOrWhiteSpace(updatedDescription)
                ? snapshot.Description
                : updatedDescription;

            decimal cost = finalCost.HasValue ? finalCost.Value : snapshot.Cost;

            var req = new MaintenanceUpdateRequest(
                maintenanceId,
                AvailabilityRules.ToVietnamLocal(snapshot.StartAt),
                AvailabilityRules.ToVietnamLocal(snapshot.EndAt),
                desc,
                cost,
                nextStatus);

            await UpdateMaintenanceAsync(req);
        }

        private static async Task EnsureNoConflictsAsync(
            CrmsContext db,
            long vehicleId,
            DateTime startUtc,
            DateTime endUtc,
            DateTime nowUtc,
            long? excludeMaintenanceId)
        {
            bool hasBookingConflict = await db.Bookings.AnyAsync(b =>
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

            bool hasMaintConflict = await db.MaintenanceRecords.AnyAsync(m =>
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

            bool hasCalendarBlock = await db.VehicleCalendarBlocks.AnyAsync(block =>
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

        private async Task<long> RequireActiveAdminAsync(CrmsContext db)
        {
            long? userId = _currentUserId();
            if (!userId.HasValue)
            {
                throw new InvalidOperationException("Vui lòng đăng nhập tài khoản Quản trị viên (Admin).");
            }

            bool isAdmin = await db.Users
                .AsNoTracking()
                .AnyAsync(u => u.UserId == userId.Value && u.IsActive && u.UserRole == "Admin");

            if (!isAdmin)
            {
                throw new InvalidOperationException("Chỉ Quản trị viên (Admin) đang hoạt động mới có quyền quản lý bảo trì.");
            }

            return userId.Value;
        }
    }
}
