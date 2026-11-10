using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRMS.Data;
using CRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Services.Vehicles
{
    public class VehicleService : IVehicleService
    {
        private readonly Func<CrmsContext> _contextFactory;
        private readonly Func<long?> _currentUserId;

        public VehicleService()
            : this(CrmsContextFactory.Create, () => CurrentUserService.Instance.UserId)
        {
        }

        public VehicleService(Func<CrmsContext> contextFactory, Func<long?> currentUserId)
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

        public async Task<List<VehicleListItem>> GetVehiclesAsync(VehicleFilter? filter = null)
        {
            if (filter == null)
            {
                filter = new VehicleFilter();
            }

            if (filter.MinDailyRate < 0 || filter.MaxDailyRate < 0 ||
                (filter.MinDailyRate.HasValue && filter.MaxDailyRate.HasValue && filter.MinDailyRate > filter.MaxDailyRate))
            {
                throw new InvalidOperationException("Khoảng giá lọc không hợp lệ.");
            }

            using (CrmsContext db = _contextFactory())
            {
                IQueryable<Vehicle> query = db.Vehicles.AsNoTracking().AsQueryable();

                if (filter.OnlyActiveCategories)
                {
                    query = query.Where(v => v.Category.IsActive);
                }

                if (filter.CategoryId.HasValue)
                {
                    query = query.Where(v => v.CategoryId == filter.CategoryId.Value);
                }

                if (!string.IsNullOrWhiteSpace(filter.OperationalStatus))
                {
                    string status = filter.OperationalStatus.Trim();
                    query = query.Where(v => v.OperationalStatus == status);
                }

                if (filter.MinDailyRate.HasValue)
                {
                    query = query.Where(v => v.DailyRate >= filter.MinDailyRate.Value);
                }

                if (filter.MaxDailyRate.HasValue)
                {
                    query = query.Where(v => v.DailyRate <= filter.MaxDailyRate.Value);
                }

                if (!string.IsNullOrWhiteSpace(filter.Keyword))
                {
                    string kw = filter.Keyword.Trim();
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
        }

        public async Task<VehicleDetailsDto?> GetVehicleDetailsAsync(long vehicleId)
        {
            using (CrmsContext db = _contextFactory())
            {
                Vehicle? vehicle = await db.Vehicles
                    .AsNoTracking()
                    .Include(v => v.Category)
                    .Include(v => v.VehicleImages)
                    .FirstOrDefaultAsync(v => v.VehicleId == vehicleId);

                if (vehicle == null)
                {
                    return null;
                }

                List<VehicleImageDto> images = vehicle.VehicleImages
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
        }

        public async Task<long> SaveVehicleAsync(VehicleUpsertRequest request)
        {
            using (CrmsContext db = _contextFactory())
            {
                await RequireActiveAdminAsync(db);

                using (var tx = await db.Database.BeginTransactionAsync())
                {
                    Vehicle? existing = null;
                    if (request.VehicleId.HasValue)
                    {
                        existing = await db.Vehicles
                            .Include(v => v.VehicleImages)
                            .FirstOrDefaultAsync(v => v.VehicleId == request.VehicleId.Value);

                        if (existing == null)
                        {
                            throw new InvalidOperationException("Không tìm thấy xe cần cập nhật.");
                        }
                    }

                    int? currentOdo = existing != null ? existing.OdometerKm : null;
                    VehicleUpsertRequest clean = VehicleRules.ValidateVehicle(request, currentOdo);

                    VehicleCategory? category = await db.VehicleCategories
                        .FirstOrDefaultAsync(c => c.CategoryId == clean.CategoryId);

                    if (category == null)
                    {
                        throw new InvalidOperationException("Loại xe đã chọn không tồn tại.");
                    }

                    if (clean.OperationalStatus == "Active" && !category.IsActive)
                    {
                        throw new InvalidOperationException($"Loại xe '{category.CategoryName}' đang ngừng hoạt động nên không thể để xe ở trạng thái Hoạt động.");
                    }

                    bool duplicatePlate = await db.Vehicles.AnyAsync(v =>
                        v.LicensePlate == clean.LicensePlate &&
                        (!clean.VehicleId.HasValue || v.VehicleId != clean.VehicleId.Value));

                    if (duplicatePlate)
                    {
                        throw new InvalidOperationException($"Biển số '{clean.LicensePlate}' đã tồn tại trên một xe khác trong hệ thống.");
                    }

                    try
                    {
                        bool rateChanged;
                        long targetVehicleId;

                        if (existing == null)
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
                            foreach (VehicleImageInput img in clean.Images)
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

                        if (existing == null)
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
                    catch (DbUpdateException)
                    {
                        throw new InvalidOperationException($"Không thể lưu dữ liệu xe '{clean.LicensePlate}'. Vui lòng kiểm tra thông tin trùng lặp và thử lại.");
                    }
                }
            }
        }

        public async Task SetVehicleStatusAsync(long vehicleId, string operationalStatus)
        {
            string status = (operationalStatus ?? string.Empty).Trim();
            if (status != "Active" && status != "Suspended")
            {
                throw new InvalidOperationException("Trạng thái xe chỉ chấp nhận Active hoặc Suspended.");
            }

            using (CrmsContext db = _contextFactory())
            {
                await RequireActiveAdminAsync(db);

                using (var tx = await db.Database.BeginTransactionAsync())
                {
                    Vehicle? vehicle = await db.Vehicles
                        .Include(v => v.Category)
                        .FirstOrDefaultAsync(v => v.VehicleId == vehicleId);

                    if (vehicle == null)
                    {
                        throw new InvalidOperationException("Không tìm thấy xe trong hệ thống.");
                    }

                    if (status == "Active" && !vehicle.Category.IsActive)
                    {
                        throw new InvalidOperationException($"Loại xe '{vehicle.Category.CategoryName}' đang ngừng hoạt động. Hãy bật lại loại xe trước khi kích hoạt xe này.");
                    }

                    if (status == "Suspended" && vehicle.OperationalStatus != "Suspended")
                    {
                        await EnsureNoActiveBookingsForSuspensionAsync(db, vehicleId);
                    }

                    vehicle.OperationalStatus = status;
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
            }
        }

        public async Task<List<CategoryListItem>> GetCategoriesAsync(bool includeInactive = true)
        {
            using (CrmsContext db = _contextFactory())
            {
                IQueryable<VehicleCategory> query = db.VehicleCategories.AsNoTracking().AsQueryable();
                if (!includeInactive)
                {
                    query = query.Where(c => c.IsActive);
                }

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
        }

        public async Task<int> SaveCategoryAsync(CategoryUpsertRequest request)
        {
            CategoryUpsertRequest clean = VehicleRules.ValidateCategory(request);

            using (CrmsContext db = _contextFactory())
            {
                await RequireActiveAdminAsync(db);

                using (var tx = await db.Database.BeginTransactionAsync())
                {
                    bool duplicateName = await db.VehicleCategories.AnyAsync(c =>
                        c.CategoryName == clean.CategoryName &&
                        (!clean.CategoryId.HasValue || c.CategoryId != clean.CategoryId.Value));

                    if (duplicateName)
                    {
                        throw new InvalidOperationException($"Loại xe '{clean.CategoryName}' đã tồn tại.");
                    }

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
                            VehicleCategory? entity = await db.VehicleCategories
                                .FirstOrDefaultAsync(c => c.CategoryId == clean.CategoryId.Value);

                            if (entity == null)
                            {
                                throw new InvalidOperationException("Không tìm thấy loại xe cần cập nhật.");
                            }

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
                    catch (DbUpdateException)
                    {
                        throw new InvalidOperationException($"Loại xe '{clean.CategoryName}' đã tồn tại.");
                    }
                }
            }
        }

        public async Task SetCategoryActiveAsync(int categoryId, bool isActive)
        {
            using (CrmsContext db = _contextFactory())
            {
                await RequireActiveAdminAsync(db);

                using (var tx = await db.Database.BeginTransactionAsync())
                {
                    VehicleCategory? entity = await db.VehicleCategories
                        .FirstOrDefaultAsync(c => c.CategoryId == categoryId);

                    if (entity == null)
                    {
                        throw new InvalidOperationException("Không tìm thấy loại xe trong hệ thống.");
                    }

                    if (!isActive && entity.IsActive)
                    {
                        await EnsureNoActiveVehiclesInCategoryAsync(db, categoryId, entity.CategoryName);
                    }

                    entity.IsActive = isActive;
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
            }
        }

        private async Task RequireActiveAdminAsync(CrmsContext db)
        {
            long? userId = _currentUserId();
            if (!userId.HasValue)
            {
                throw new InvalidOperationException("Vui lòng đăng nhập tài khoản Admin để thực hiện thao tác này.");
            }

            bool isAdmin = await db.Users.AsNoTracking()
                .AnyAsync(u => u.UserId == userId.Value && u.UserRole == "Admin" && u.IsActive);

            if (!isAdmin)
            {
                throw new InvalidOperationException("Chỉ tài khoản Admin đang hoạt động mới có quyền quản lý phương tiện và loại xe.");
            }
        }

        private static async Task EnsureNoActiveBookingsForSuspensionAsync(CrmsContext db, long vehicleId)
        {
            DateTime nowUtc = DateTime.UtcNow;
            bool hasBlockingBooking = await db.Bookings.AnyAsync(b =>
                b.VehicleId == vehicleId &&
                (b.BookingStatus == "InUse" ||
                 b.BookingStatus == "Confirmed" ||
                 (b.BookingStatus == "PendingDeposit" && b.DepositDeadline > nowUtc)));

            if (hasBlockingBooking)
            {
                throw new InvalidOperationException("Không thể tạm ngưng xe đang có đơn thuê hoặc đang trong thời gian giữ chỗ đặt cọc.");
            }
        }

        private static async Task EnsureNoActiveVehiclesInCategoryAsync(CrmsContext db, int categoryId, string categoryName)
        {
            bool hasActiveVehicle = await db.Vehicles.AnyAsync(v =>
                v.CategoryId == categoryId && v.OperationalStatus == "Active");

            if (hasActiveVehicle)
            {
                throw new InvalidOperationException($"Không thể ngừng hoạt động loại xe '{categoryName}' vì vẫn còn xe đang ở trạng thái Hoạt động thuộc loại này.");
            }
        }

        private static async Task SyncLongTermRatesAsync(CrmsContext db, long vehicleId, decimal dailyRate)
        {
            decimal baseMonthly = decimal.Round(dailyRate * 30m, 2);
            DateTime nowUtc = DateTime.UtcNow;

            int[] durations = { 1, 3, 6, 9, 12 };
            decimal[] discounts = { 0.00m, 2.00m, 3.00m, 4.00m, 5.00m };

            List<VehicleLongTermRate> existingRates = await db.VehicleLongTermRates
                .Where(r => r.VehicleId == vehicleId)
                .ToListAsync();

            for (int i = 0; i < durations.Length; i++)
            {
                int months = durations[i];
                decimal discount = discounts[i];

                VehicleLongTermRate? existingRate = existingRates
                    .FirstOrDefault(r => r.DurationMonths == months);

                if (existingRate != null)
                {
                    existingRate.BaseMonthlyRate = baseMonthly;
                    existingRate.UpdatedAt = nowUtc;
                }
                else
                {
                    db.VehicleLongTermRates.Add(new VehicleLongTermRate
                    {
                        VehicleId = vehicleId,
                        DurationMonths = months,
                        BaseMonthlyRate = baseMonthly,
                        DiscountPercent = discount,
                        IsActive = true,
                        UpdatedAt = nowUtc
                    });
                }
            }

            await db.SaveChangesAsync();
        }

        private static async Task EnsureSearchCatalogEntryAsync(CrmsContext db, long vehicleId)
        {
            // shortcut: binds new vehicles to the default verified fleet owner & active city location so Guest/Customer catalog sees them immediately
            VehicleOwner? defaultOwner = await db.VehicleOwners
                .AsNoTracking()
                .Where(o => o.IsActive && o.IsVerified)
                .OrderBy(o => o.OwnerId)
                .FirstOrDefaultAsync();

            List<int> activeCityIds = await db.Cities
                .AsNoTracking()
                .Where(c => c.IsActive)
                .Select(c => c.CityId)
                .ToListAsync();

            RentalLocation? defaultLocation = await db.RentalLocations
                .AsNoTracking()
                .Where(l => l.IsActive && activeCityIds.Contains(l.CityId))
                .OrderBy(l => l.LocationId)
                .FirstOrDefaultAsync();

            if (defaultOwner == null || defaultLocation == null)
            {
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;

            bool hasProfile = await db.VehicleSearchProfiles
                .AnyAsync(p => p.VehicleId == vehicleId);

            if (!hasProfile)
            {
                db.VehicleSearchProfiles.Add(new VehicleSearchProfile
                {
                    VehicleId = vehicleId,
                    OwnerId = defaultOwner.OwnerId,
                    LocationId = defaultLocation.LocationId,
                    IsPublished = true,
                    VerifiedAt = nowUtc,
                    UpdatedAt = nowUtc
                });
            }

            string[] defaultModes = { "SelfDrive", "LongTerm" };
            for (int i = 0; i < defaultModes.Length; i++)
            {
                string mode = defaultModes[i];
                bool hasMode = await db.VehicleRentalModes
                    .AnyAsync(m => m.VehicleId == vehicleId && m.RentalMode == mode);

                if (!hasMode)
                {
                    db.VehicleRentalModes.Add(new VehicleRentalMode
                    {
                        VehicleId = vehicleId,
                        RentalMode = mode,
                        IsEnabled = true,
                        VerifiedAt = nowUtc
                    });
                }
            }

            await db.SaveChangesAsync();
        }
    }
}
