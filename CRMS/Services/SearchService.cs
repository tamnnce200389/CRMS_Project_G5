
using CRMS.Data;
using CRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Services
{
    // Du lieu hien thi tren mot the xe
    public class CarSearchItem
    {
        public long VehicleId { get; set; }

        public string Brand { get; set; } = "";

        public string Model { get; set; } = "";

        public string CategoryName { get; set; } = "";

        public short ManufactureYear { get; set; }

        public byte SeatCount { get; set; }

        public string Transmission { get; set; } = "";

        public string FuelType { get; set; } = "";

        public decimal DailyRate { get; set; }

        public string? ImageUrl { get; set; }
    }

    public class SearchService
    {
        // Lay danh sach loai xe
        public async Task<List<VehicleCategory>> GetCategoriesAsync()
        {
            using var db = CrmsContextFactory.Create();

            return await db.VehicleCategories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.CategoryName)
                .ToListAsync();
        }

        // F05 - Tim kiem va loc xe
        public async Task<List<CarSearchItem>> SearchCarsAsync(
            string? keyword = null,
            int? categoryId = null,
            decimal? minPrice = null,
            decimal? maxPrice = null)
        {
            if (minPrice < 0 || maxPrice < 0 ||
                (minPrice.HasValue && maxPrice.HasValue &&
                 minPrice > maxPrice))
            {
                throw new ArgumentException("Khoang gia khong hop le.");
            }

            using var db = CrmsContextFactory.Create();

            // Chi lay xe va loai xe dang hoat dong
            var query = db.Vehicles
                .AsNoTracking()
                .Where(v => v.OperationalStatus == "Active"
                         && v.Category.IsActive);

            // Tim theo hang, mau hoac bien so
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string search = keyword.Trim();

                query = query.Where(v =>
                    v.Brand.Contains(search) ||
                    v.Model.Contains(search) ||
                    v.LicensePlate.Contains(search));
            }

            // Loc theo loai xe
            if (categoryId.HasValue)
            {
                query = query.Where(v =>
                    v.CategoryId == categoryId.Value);
            }

            // Loc gia thap nhat
            if (minPrice.HasValue)
            {
                query = query.Where(v =>
                    v.DailyRate >= minPrice.Value);
            }

            // Loc gia cao nhat
            if (maxPrice.HasValue)
            {
                query = query.Where(v =>
                    v.DailyRate <= maxPrice.Value);
            }

            var cars = await query
                .OrderBy(v => v.DailyRate)
                .Select(v => new CarSearchItem
                {
                    VehicleId = v.VehicleId,
                    Brand = v.Brand,
                    Model = v.Model,
                    CategoryName = v.Category.CategoryName,
                    ManufactureYear = v.ManufactureYear,
                    SeatCount = v.SeatCount,
                    Transmission = v.Transmission,
                    FuelType = v.FuelType,
                    DailyRate = v.DailyRate
                })
                .ToListAsync();

            // Lay anh dai dien cho cac xe tim duoc
            var vehicleIds = cars
                .Select(c => c.VehicleId)
                .ToArray();

            if (vehicleIds.Length == 0)
                return cars;

            var images = await db.VehicleImages
                .AsNoTracking()
                .Where(i => vehicleIds.Contains(i.VehicleId))
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.SortOrder)
                .Select(i => new
                {
                    i.VehicleId,
                    i.ImageUrl
                })
                .ToListAsync();

            // Moi xe lay mot anh dau tien, uu tien anh chinh
            var imageLookup = images
                .GroupBy(i => i.VehicleId)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().ImageUrl);

            foreach (var car in cars)
            {
                if (imageLookup.TryGetValue(
                    car.VehicleId, out string? imageUrl))
                {
                    car.ImageUrl = imageUrl;
                }
            }

            return cars;
        }
    }
}
