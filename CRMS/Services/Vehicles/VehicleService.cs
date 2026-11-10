using System.Data;
using CRMS.Data;
using CRMS.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Services.Vehicles;

public sealed class VehicleService : IVehicleService
{
    private readonly Func<CrmsContext> _contextFactory;
    private readonly Func<long?> _currentUserId;

    public VehicleService()
        : this(CrmsContextFactory.Create, () => CurrentUserService.Instance.UserId)
    {
    }

    public VehicleService(Func<CrmsContext> contextFactory, Func<long?> currentUserId)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _currentUserId = currentUserId ?? throw new ArgumentNullException(nameof(currentUserId));
    }

    public async Task<List<VehicleListItem>> GetVehiclesAsync(VehicleFilter? filter = null)
    {
        filter ??= new VehicleFilter();
        if (filter.MinDailyRate < 0 || filter.MaxDailyRate < 0 ||
            (filter.MinDailyRate.HasValue && filter.MaxDailyRate.HasValue && filter.MinDailyRate > filter.MaxDailyRate))
        {
            throw new InvalidOperationException("Khoảng giá lọc không hợp lệ.");
        }

        using var db = _contextFactory();
        var query = db.Vehicles.AsNoTracking().AsQueryable();

        if (filter.OnlyActiveCategories)
            query = query.Where(v => v.Category.IsActive);

        if (filter.CategoryId.HasValue)
            query = query.Where(v => v.CategoryId == filter.CategoryId.Value);

        if (!string.IsNullOrWhiteSpace(filter.OperationalStatus))
        {
            var status = filter.OperationalStatus.Trim();
            query = query.Where(v => v.OperationalStatus == status);
        }

        if (filter.MinDailyRate.HasValue)
            query = query.Where(v => v.DailyRate >= filter.MinDailyRate.Value);

        if (filter.MaxDailyRate.HasValue)
            query = query.Where(v => v.DailyRate <= filter.MaxDailyRate.Value);

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim();
            query = query.Where(v =>
                v.LicensePlate.Contains(kw) ||
                v.Brand.Contains(kw) ||
                v.Model.Contains(kw) ||
                v.Category.CategoryName.Contains(kw));
        }

        return await query
            .OrderBy(v => v.VehicleId)
            .Select(v => new VehicleListItem
            {
                VehicleId = v.VehicleId,
                CategoryId = v.CategoryId,
                CategoryName = v.Category.CategoryName,
                LicensePlate = v.LicensePlate,
                Brand = v.Brand,
                Model = v.Model,
                ManufactureYear = v.ManufactureYear,
                SeatCount = v.SeatCount,
                Transmission = v.Transmission,
                FuelType = v.FuelType,
                DailyRate = v.DailyRate,
                OdometerKm = v.OdometerKm,
                OperationalStatus = v.OperationalStatus,
                Description = v.Description,
                ImageCount = v.VehicleImages.Count,
                PrimaryImageUrl = v.VehicleImages
                    .OrderByDescending(i => i.IsPrimary)
                    .ThenBy(i => i.SortOrder)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault()
            })
            .ToListAsync();
    }

    public async Task<VehicleDetailsDto?> GetVehicleDetailsAsync(long vehicleId)
    {
        using var db = _contextFactory();
        var vehicle = await db.Vehicles
            .AsNoTracking()
            .Include(v => v.Category)
            .Include(v => v.VehicleImages)
            .FirstOrDefaultAsync(v => v.VehicleId == vehicleId);

        if (vehicle is null)
            return null;

        var images = vehicle.VehicleImages
            .OrderBy(i => i.SortOrder)
            .ThenByDescending(i => i.IsPrimary)
            .ThenBy(i => i.ImageId)
            .Select(i => new VehicleImageDto(i.ImageId, i.ImageUrl, i.IsPrimary, i.SortOrder))
            .ToList();

        return new VehicleDetailsDto
        {
            VehicleId = vehicle.VehicleId,
            CategoryId = vehicle.CategoryId,
            CategoryName = vehicle.Category.CategoryName,
            LicensePlate = vehicle.LicensePlate,
            Brand = vehicle.Brand,
            Model = vehicle.Model,
            ManufactureYear = vehicle.ManufactureYear,
            SeatCount = vehicle.SeatCount,
            Transmission = vehicle.Transmission,
            FuelType = vehicle.FuelType,
            DailyRate = vehicle.DailyRate,
            OdometerKm = vehicle.OdometerKm,
            OperationalStatus = vehicle.OperationalStatus,
            Description = vehicle.Description,
            Images = images
        };
    }

    public async Task<long> SaveVehicleAsync(VehicleUpsertRequest request)
    {
        using var db = _contextFactory();
        await RequireActiveAdminAsync(db);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        Vehicle? existing = null;
        if (request.VehicleId.HasValue)
        {
            await AcquireVehicleLockAsync(db, request.VehicleId.Value);
            existing = await db.Vehicles
                .Include(v => v.VehicleImages)
                .FirstOrDefaultAsync(v => v.VehicleId == request.VehicleId.Value)
                ?? throw new InvalidOperationException("Không tìm thấy xe cần cập nhật.");
        }

        var clean = VehicleRules.ValidateVehicle(request, existing?.OdometerKm);

        var category = await db.VehicleCategories
            .FirstOrDefaultAsync(c => c.CategoryId == clean.CategoryId)
            ?? throw new InvalidOperationException("Loại xe đã chọn không tồn tại.");

        if (clean.OperationalStatus == "Active" && !category.IsActive)
            throw new InvalidOperationException($"Loại xe '{category.CategoryName}' đang ngừng hoạt động nên không thể để xe ở trạng thái Hoạt động.");

        var duplicatePlate = await db.Vehicles.AnyAsync(v =>
            v.LicensePlate == clean.LicensePlate &&
            (!clean.VehicleId.HasValue || v.VehicleId != clean.VehicleId.Value));

        if (duplicatePlate)
            throw new InvalidOperationException($"Biển số '{clean.LicensePlate}' đã tồn tại trên một xe khác trong hệ thống.");

        try
        {
            bool rateChanged;
            long targetVehicleId;

            if (existing is null)
            {
                var entity = new Vehicle
                {
                    CategoryId = clean.CategoryId,
                    LicensePlate = clean.LicensePlate,
                    Brand = clean.Brand,
                    Model = clean.Model,
                    ManufactureYear = clean.ManufactureYear,
                    SeatCount = clean.SeatCount,
                    Transmission = clean.Transmission,
                    FuelType = clean.FuelType,
                    DailyRate = clean.DailyRate,
                    OdometerKm = clean.OdometerKm,
                    OperationalStatus = clean.OperationalStatus,
                    Description = clean.Description,
                    CreatedAt = DateTime.UtcNow
                };

                db.Vehicles.Add(entity);
                await db.SaveChangesAsync();

                targetVehicleId = entity.VehicleId;
                rateChanged = true;
            }
            else
            {
                if (clean.OperationalStatus == "Suspended" && existing.OperationalStatus != "Suspended")
                {
                    await EnsureNoActiveBookingsForSuspensionAsync(db, existing.VehicleId);
                }

                rateChanged = existing.DailyRate != clean.DailyRate;
                existing.CategoryId = clean.CategoryId;
                existing.LicensePlate = clean.LicensePlate;
                existing.Brand = clean.Brand;
                existing.Model = clean.Model;
                existing.ManufactureYear = clean.ManufactureYear;
                existing.SeatCount = clean.SeatCount;
                existing.Transmission = clean.Transmission;
                existing.FuelType = clean.FuelType;
                existing.DailyRate = clean.DailyRate;
                existing.OdometerKm = clean.OdometerKm;
                existing.OperationalStatus = clean.OperationalStatus;
                existing.Description = clean.Description;

                // Remove existing images first so UX_Images_OnePrimary never collides on primary swap.
                if (existing.VehicleImages.Count > 0)
                {
                    db.VehicleImages.RemoveRange(existing.VehicleImages);
                }

                await db.SaveChangesAsync();
                targetVehicleId = existing.VehicleId;
            }

            if (clean.Images != null && clean.Images.Count > 0)
            {
                foreach (var img in clean.Images)
                {
                    db.VehicleImages.Add(new VehicleImage
                    {
                        VehicleId = targetVehicleId,
                        ImageUrl = img.ImageUrl,
                        IsPrimary = img.IsPrimary,
                        SortOrder = img.SortOrder
                    });
                }

                await db.SaveChangesAsync();
            }

            if (rateChanged)
            {
                await SyncLongTermRatesAsync(db, targetVehicleId, clean.DailyRate);
            }

            if (existing is null)
            {
                await EnsureSearchCatalogEntryAsync(db, targetVehicleId);
            }

            await tx.CommitAsync();
            return targetVehicleId;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("Dữ liệu xe vừa được thay đổi bởi phiên làm việc khác. Vui lòng làm mới và thử lại.");
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new InvalidOperationException($"Biển số '{clean.LicensePlate}' hoặc ảnh đại diện bị trùng trong cơ sở dữ liệu.");
        }
    }

    public async Task SetVehicleStatusAsync(long vehicleId, string operationalStatus)
    {
        var status = (operationalStatus ?? "").Trim();
        if (status != "Active" && status != "Suspended")
            throw new InvalidOperationException("Trạng thái xe chỉ chấp nhận Active hoặc Suspended.");

        using var db = _contextFactory();
        await RequireActiveAdminAsync(db);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await AcquireVehicleLockAsync(db, vehicleId);

        var vehicle = await db.Vehicles
            .Include(v => v.Category)
            .FirstOrDefaultAsync(v => v.VehicleId == vehicleId)
            ?? throw new InvalidOperationException("Không tìm thấy xe trong hệ thống.");

        if (status == "Active" && !vehicle.Category.IsActive)
            throw new InvalidOperationException($"Loại xe '{vehicle.Category.CategoryName}' đang ngừng hoạt động. Hãy bật lại loại xe trước khi kích hoạt xe này.");

        if (status == "Suspended" && vehicle.OperationalStatus != "Suspended")
        {
            await EnsureNoActiveBookingsForSuspensionAsync(db, vehicleId);
        }

        vehicle.OperationalStatus = status;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<List<CategoryListItem>> GetCategoriesAsync(bool includeInactive = true)
    {
        using var db = _contextFactory();
        var query = db.VehicleCategories.AsNoTracking().AsQueryable();
        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        return await query
            .OrderBy(c => c.CategoryName)
            .Select(c => new CategoryListItem
            {
                CategoryId = c.CategoryId,
                CategoryName = c.CategoryName,
                Description = c.Description,
                IsActive = c.IsActive,
                TotalVehicles = c.Vehicles.Count,
                ActiveVehicles = c.Vehicles.Count(v => v.OperationalStatus == "Active")
            })
            .ToListAsync();
    }

    public async Task<int> SaveCategoryAsync(CategoryUpsertRequest request)
    {
        var clean = VehicleRules.ValidateCategory(request);

        using var db = _contextFactory();
        await RequireActiveAdminAsync(db);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        var duplicateName = await db.VehicleCategories.AnyAsync(c =>
            c.CategoryName == clean.CategoryName &&
            (!clean.CategoryId.HasValue || c.CategoryId != clean.CategoryId.Value));

        if (duplicateName)
            throw new InvalidOperationException($"Loại xe '{clean.CategoryName}' đã tồn tại.");

        try
        {
            if (!clean.CategoryId.HasValue)
            {
                var entity = new VehicleCategory
                {
                    CategoryName = clean.CategoryName,
                    Description = clean.Description,
                    IsActive = clean.IsActive
                };
                db.VehicleCategories.Add(entity);
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                return entity.CategoryId;
            }
            else
            {
                var entity = await db.VehicleCategories
                    .FirstOrDefaultAsync(c => c.CategoryId == clean.CategoryId.Value)
                    ?? throw new InvalidOperationException("Không tìm thấy loại xe cần cập nhật.");

                if (!clean.IsActive && entity.IsActive)
                {
                    await EnsureNoActiveVehiclesInCategoryAsync(db, entity.CategoryId, entity.CategoryName);
                }

                entity.CategoryName = clean.CategoryName;
                entity.Description = clean.Description;
                entity.IsActive = clean.IsActive;

                await db.SaveChangesAsync();
                await tx.CommitAsync();
                return entity.CategoryId;
            }
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new InvalidOperationException($"Loại xe '{clean.CategoryName}' đã tồn tại.");
        }
    }

    public async Task SetCategoryActiveAsync(int categoryId, bool isActive)
    {
        using var db = _contextFactory();
        await RequireActiveAdminAsync(db);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        var entity = await db.VehicleCategories
            .FirstOrDefaultAsync(c => c.CategoryId == categoryId)
            ?? throw new InvalidOperationException("Không tìm thấy loại xe trong hệ thống.");

        if (!isActive && entity.IsActive)
        {
            await EnsureNoActiveVehiclesInCategoryAsync(db, categoryId, entity.CategoryName);
        }

        entity.IsActive = isActive;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private async Task RequireActiveAdminAsync(CrmsContext db)
    {
        var userId = _currentUserId();
        if (!userId.HasValue)
            throw new InvalidOperationException("Vui lòng đăng nhập tài khoản Admin để thực hiện thao tác này.");

        var isAdmin = await db.Users.AsNoTracking()
            .AnyAsync(u => u.UserId == userId.Value && u.UserRole == "Admin" && u.IsActive);

        if (!isAdmin)
            throw new InvalidOperationException("Chỉ tài khoản Admin đang hoạt động mới có quyền quản lý phương tiện và loại xe.");
    }

    private static async Task AcquireVehicleLockAsync(CrmsContext db, long vehicleId)
    {
        var resource = new SqlParameter("@Resource", SqlDbType.NVarChar, 255)
        {
            Value = $"CRMS_VEHICLE_{vehicleId}"
        };
        var result = new SqlParameter("@Result", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };

        await db.Database.ExecuteSqlRawAsync(
            "EXEC @Result = sys.sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;",
            resource,
            result);

        if (result.Value is int code && code < 0)
            throw new InvalidOperationException("Xe đang bận xử lý giao dịch khác. Vui lòng thử lại sau giây lát.");
    }

    private static async Task EnsureNoActiveBookingsForSuspensionAsync(CrmsContext db, long vehicleId)
    {
        var nowUtc = DateTime.UtcNow;
        var hasBlockingBooking = await db.Bookings.AnyAsync(b =>
            b.VehicleId == vehicleId &&
            (b.BookingStatus == "InUse" ||
             b.BookingStatus == "Confirmed" ||
             (b.BookingStatus == "PendingDeposit" && b.DepositDeadline > nowUtc)));

        if (hasBlockingBooking)
            throw new InvalidOperationException("Không thể tạm ngưng xe đang có đơn thuê hoặc đang trong thời gian giữ chỗ đặt cọc.");
    }

    private static async Task EnsureNoActiveVehiclesInCategoryAsync(CrmsContext db, int categoryId, string categoryName)
    {
        var hasActiveVehicle = await db.Vehicles.AnyAsync(v =>
            v.CategoryId == categoryId && v.OperationalStatus == "Active");

        if (hasActiveVehicle)
            throw new InvalidOperationException($"Không thể ngừng hoạt động loại xe '{categoryName}' vì vẫn còn xe đang ở trạng thái Hoạt động thuộc loại này.");
    }

    private static async Task SyncLongTermRatesAsync(CrmsContext db, long vehicleId, decimal dailyRate)
    {
        var baseMonthly = decimal.Round(dailyRate * 30m, 2);
        await db.Database.ExecuteSqlInterpolatedAsync($@"
MERGE dbo.VehicleLongTermRates AS target
USING (VALUES
    (CAST(1 AS TINYINT), CAST(0.00 AS DECIMAL(5,2))),
    (CAST(3 AS TINYINT), CAST(2.00 AS DECIMAL(5,2))),
    (CAST(6 AS TINYINT), CAST(3.00 AS DECIMAL(5,2))),
    (CAST(9 AS TINYINT), CAST(4.00 AS DECIMAL(5,2))),
    (CAST(12 AS TINYINT), CAST(5.00 AS DECIMAL(5,2)))
) AS src(DurationMonths, DiscountPercent)
ON target.VehicleId = {vehicleId} AND target.DurationMonths = src.DurationMonths
WHEN MATCHED THEN
    UPDATE SET BaseMonthlyRate = {baseMonthly}, UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (VehicleId, DurationMonths, BaseMonthlyRate, DiscountPercent, IsActive, UpdatedAt)
    VALUES ({vehicleId}, src.DurationMonths, {baseMonthly}, src.DiscountPercent, 1, SYSUTCDATETIME());");
    }

    private static async Task EnsureSearchCatalogEntryAsync(CrmsContext db, long vehicleId)
    {
        // shortcut: binds new vehicles to the default verified fleet owner & active city location so Guest/Customer catalog sees them immediately
        await db.Database.ExecuteSqlInterpolatedAsync($@"
IF OBJECT_ID(N'dbo.VehicleSearchProfiles', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.VehicleRentalModes', N'U') IS NOT NULL
BEGIN
    DECLARE @OwnerId BIGINT = (
        SELECT TOP (1) OwnerId
        FROM dbo.VehicleOwners
        WHERE IsActive = 1 AND IsVerified = 1
        ORDER BY OwnerId);

    DECLARE @LocationId BIGINT = (
        SELECT TOP (1) l.LocationId
        FROM dbo.RentalLocations l
        INNER JOIN dbo.Cities c ON c.CityId = l.CityId
        WHERE l.IsActive = 1 AND c.IsActive = 1
        ORDER BY l.LocationId);

    IF @OwnerId IS NOT NULL AND @LocationId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.VehicleSearchProfiles WHERE VehicleId = {vehicleId})
        BEGIN
            INSERT INTO dbo.VehicleSearchProfiles (VehicleId, OwnerId, LocationId, IsPublished, VerifiedAt, UpdatedAt)
            VALUES ({vehicleId}, @OwnerId, @LocationId, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
        END;

        MERGE dbo.VehicleRentalModes AS target
        USING (VALUES ('SelfDrive'), ('LongTerm')) AS src(RentalMode)
        ON target.VehicleId = {vehicleId} AND target.RentalMode = src.RentalMode
        WHEN NOT MATCHED THEN
            INSERT (VehicleId, RentalMode, IsEnabled, VerifiedAt)
            VALUES ({vehicleId}, src.RentalMode, 1, SYSUTCDATETIME());
    END;
END;");
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627);
}
