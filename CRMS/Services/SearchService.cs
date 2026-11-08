using CRMS.Services.Vehicles;
using CRMS.Data;
using CRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Services;

public sealed class CarSearchItem
{
    public long VehicleId { get; set; }
    public int CategoryId { get; set; }
    public decimal? OwnerRating { get; set; }
    public long OwnerReviewCount { get; set; }
    public bool HasOwnerRating => OwnerRating.HasValue && OwnerReviewCount > 0;
    public string OwnerRatingLabel => HasOwnerRating ? $"{OwnerRating:0.0} · {OwnerReviewCount} đánh giá" : "Chưa có đánh giá";
    public bool SupportsInstant { get; set; }
    public bool NoCollateral { get; set; }
    public bool SupportsDelivery { get; set; }
    public bool HasChildSeat { get; set; }
    public bool SupportsFreeCityPickup { get; set; }
    public bool HasFreePickup => QuoteUnit == "Trip" && (DeliveryFee == 0 || SupportsFreeCityPickup);
    public bool IsHourlyQuote => QuoteUnit == "Hour";
    public string HourlyPackageLabel => QuotedRate > 0 ? $"{EstimatedTripTotal:N0} ₫ · gói {EstimatedTripTotal / QuotedRate:0.##} giờ tính phí" : "";
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public short ManufactureYear { get; set; }
    public byte SeatCount { get; set; }
    public string Transmission { get; set; } = "";
    public string FuelType { get; set; } = "";
    public decimal DailyRate { get; set; }
    public string? ImageUrl { get; set; }
    public string? DisplayPhotoUrl => Uri.TryCreate(ImageUrl, UriKind.Absolute, out var uri)
        && string.Equals(uri.Host, "placehold.co", StringComparison.OrdinalIgnoreCase) ? null : ImageUrl;

    public decimal LongTermBaseMonthlyRate { get; set; }
    public decimal LongTermMonthlyRate { get; set; }
    public decimal LongTermDiscountPercent { get; set; }
    public decimal LongTermTotalPrice { get; set; }
    public int DurationMonths { get; set; }
    public decimal QuotedRate { get; set; }
    public decimal EstimatedTripTotal { get; set; }
    public string QuoteUnit { get; set; } = "Day";
    public string PriceUnitLabel => QuoteUnit switch { "Hour" => "₫/giờ", "Month" => "₫/tháng", "Trip" => "₫/chuyến", _ => "₫/ngày" };
    public string QuotedPriceLabel => $"{QuotedRate:N0} {PriceUnitLabel}";
    public bool HasPromotion { get; set; }
    public string PromotionNote => HasPromotion && QuoteUnit != "Month" ? "Có mã ưu đãi · kiểm tra điều kiện khi đặt" : "";
    public string PriceNote => QuoteUnit is "Hour" or "Trip" ? "Giá tham khảo · chưa gồm phí phát sinh" : "Chưa gồm phí giao nhận và tiện ích";
    // Fixed-trip offers remain quote-only; Day/Hour/Month use the atomic booking procedure.
    public bool CanUseLegacyBooking { get; set; }
    public decimal? DeliveryFee { get; set; }
    public string CityName { get; set; } = "";
}

public sealed record CarSearchContext
{
    public int? PickupCityId { get; init; }
    public int? DestinationCityId { get; init; }
    public string RouteType { get; init; } = "InnerCity";
    public string? Fuel { get; init; }
    public string[] PurposeCodes { get; init; } = Array.Empty<string>();
    public string? BudgetCode { get; init; }
    public bool HourlyOnly { get; init; }
    public bool ChildSeatOnly { get; init; }
    public bool ExtendedTravelOnly { get; init; }
    public bool SportOnly { get; init; }
}

public sealed record CarSearchFilter
{
    public string[] ChildSeatAgeCodes { get; init; } = Array.Empty<string>();
    public string RentalMode { get; init; } = "SelfDrive";
    public string? Keyword { get; init; }
    public int? CategoryId { get; init; }
    public string? Brand { get; init; }
    public string? Transmission { get; init; }
    public string? FuelType { get; init; }
    public byte? Seats { get; init; }
    public short? MinYear { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public string Sort { get; init; } = "Mặc định";
    public bool DiscountOnly { get; init; }
    public bool HourlyOnly { get; init; }
    public bool DeliveryOnly { get; init; }
    public bool FreeDeliveryOnly { get; init; }
    public bool FreePickupInCityOnly { get; init; }
    public bool HighlyRatedOnly { get; init; }
    public bool InstantOnly { get; init; }
    public bool NoCollateralOnly { get; init; }
    public bool ChildSeatOnly { get; init; }
    public bool ExtendedTravelOnly { get; init; }
    public bool UnlimitedMileageOnly { get; init; }
    public bool SportOnly { get; init; }
    public string[] PurposeCodes { get; init; } = Array.Empty<string>();
    public int? CityId { get; init; }
    public int? DestinationCityId { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string RouteType { get; init; } = "InnerCity";
    public int DurationMonths { get; init; } = 3;
    public long? CustomerId { get; init; }
    public DateTime? StartAtLocal { get; init; }
    public DateTime? EndAtLocal { get; init; }

    public void Validate()
    {
        if (ChildSeatAgeCodes.Any(code => code is not ("ChildSeat:0-12" or "ChildSeat:0-1" or "ChildSeat:1-4" or "ChildSeat:4-7" or "ChildSeat:7-12"))) throw new ArgumentException("Nhóm tuổi ghế trẻ em không hợp lệ.");
        if (ChildSeatAgeCodes.Length > 0 && RentalMode != "SelfDrive") throw new ArgumentException("Lọc độ tuổi ghế trẻ em chỉ áp dụng xe tự lái.");
        if (RentalMode is not ("SelfDrive" or "WithDriver" or "LongTerm")) throw new ArgumentException("Chế độ thuê không hợp lệ.");
        if (MinPrice < 0 || MaxPrice < 0 || (MinPrice.HasValue && MaxPrice.HasValue && MinPrice > MaxPrice)) throw new ArgumentException("Khoảng giá không hợp lệ.");
        if (Seats is < 1 or > 30 || MinYear is < 1990 || MinYear > DateTime.Now.Year) throw new ArgumentException("Số chỗ hoặc năm sản xuất không hợp lệ.");
        if (Transmission is not (null or "Automatic" or "Manual")) throw new ArgumentException("Hộp số không hợp lệ.");
        if (FuelType is not (null or "Gasoline" or "Diesel" or "Electric" or "Hybrid")) throw new ArgumentException("Nhiên liệu không hợp lệ.");
        if (StartAtLocal.HasValue != EndAtLocal.HasValue) throw new ArgumentException("Vui lòng chọn đủ thời gian nhận và trả xe.");
        if (StartAtLocal.HasValue) AvailabilityRules.ValidatePeriod(StartAtLocal.Value, EndAtLocal!.Value, DateTime.UtcNow, requireFutureStart: true);
        if (HourlyOnly && (RentalMode != "SelfDrive" || !StartAtLocal.HasValue)) throw new ArgumentException("Thuê giờ cần chế độ tự lái và thời gian thuê cụ thể.");
        if (RentalMode == "WithDriver")
        {
            if (RouteType is not ("InnerCity" or "InterCityRoundTrip" or "InterCityOneWay")) throw new ArgumentException("Hành trình không hợp lệ.");
            if (!CityId.HasValue || !DestinationCityId.HasValue || !StartAtLocal.HasValue) throw new ArgumentException("Chọn điểm đón, điểm đến và thời gian thuê xe có tài xế.");
            if ((RouteType == "InnerCity") != (CityId == DestinationCityId)) throw new ArgumentException("Nội thành phải cùng thành phố; liên tỉnh phải khác thành phố.");
        }
        if (FreePickupInCityOnly && (RentalMode != "WithDriver" || !FreeDeliveryOnly || CityId is null or <= 0))
            throw new ArgumentException("Miễn phí đưa đón nội thành cần chế độ có tài xế và thành phố đón hợp lệ.");
        if (DeliveryOnly || (FreeDeliveryOnly && !FreePickupInCityOnly))
        {
            if (!CityId.HasValue || !Latitude.HasValue || !Longitude.HasValue || !double.IsFinite(Latitude.Value) || !double.IsFinite(Longitude.Value) || Latitude is < -90 or > 90 || Longitude is < -180 or > 180)
                throw new ArgumentException("Lọc giao/đón xe cần thành phố và tọa độ hợp lệ của điểm giao/đón.");
        }
        if (ExtendedTravelOnly && !DestinationCityId.HasValue) throw new ArgumentException("Chọn thành phố đích để lọc di chuyển mở rộng.");
        if (RentalMode == "LongTerm")
        {
            if (DurationMonths is not (1 or 3 or 6 or 9 or 12)) throw new ArgumentException("Gói dài hạn không hợp lệ.");
            if (StartAtLocal.HasValue && StartAtLocal.Value.AddMonths(DurationMonths) != EndAtLocal) throw new ArgumentException("Ngày trả phải đúng kỳ hạn thuê dài hạn đã chọn.");
        }
    }
}

public class SearchService
{
    private readonly Func<CrmsContext> _createContext;
    public SearchService() : this(CrmsContextFactory.Create) { }
    public SearchService(Func<CrmsContext> createContext) => _createContext = createContext;
    public virtual Task<SelfDriveFacets> GetSelfDriveFacetsAsync(CarSearchFilter context) => GetFacetsCoreAsync(context);
    public virtual Task<SelfDriveFacets> GetWithDriverFacetsAsync(CarSearchFilter context) => GetFacetsCoreAsync(context);
    public virtual Task<SelfDriveFacets> GetLongTermFacetsAsync(CarSearchFilter context) => GetFacetsCoreAsync(context);
    private async Task<SelfDriveFacets> GetFacetsCoreAsync(CarSearchFilter context)
    {
        using var db = _createContext();
        var query = BuildQuery(db, context);
        var types = await query.GroupBy(c => new { c.CategoryId, c.CategoryName, c.SeatCount })
            .Select(g => new { g.Key.CategoryId, g.Key.CategoryName, g.Key.SeatCount, Count = g.Count() }).ToListAsync();
        var brands = await query.GroupBy(c => c.Brand).Select(g => new { Name = g.Key, Count = g.Count() }).OrderBy(g => g.Name).ToListAsync();
        var ages = context.RentalMode == "SelfDrive" ? await db.VehicleAmenities.Where(a => a.IsActive && a.Quantity > 0 && a.RentalMode == "SelfDrive" && query.Any(c => c.VehicleId == a.VehicleId))
            .Join(db.Amenities.Where(t => t.IsActive && t.AmenityCode.StartsWith("ChildSeat:")), a => a.AmenityId, t => t.AmenityId, (a, t) => t.AmenityCode).Distinct().ToListAsync() : new List<string>();
        return new(types.OrderBy(t => t.SeatCount).ThenBy(t => t.CategoryName).Select(t => new VehicleTypeFacet(t.CategoryId,t.SeatCount,t.CategoryName,t.Count)).ToList(),
            brands.Select(b => new BrandFacet(b.Name,b.Count)).ToList(),ages);
    }
    public virtual async Task<List<VehicleCategory>> GetCategoriesAsync()
    {
        using var db = _createContext();
        return await db.VehicleCategories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.CategoryName).ToListAsync();
    }
    public virtual async Task<List<string>> GetBrandsAsync()
    {
        using var db = _createContext();
        return await db.SearchCatalogRows.Select(c => c.Brand).Distinct().OrderBy(b => b).ToListAsync();
    }
    public virtual async Task<List<SearchCity>> GetCitiesAsync()
    {
        using var db = _createContext();
        return await db.Cities.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.CityName).ToListAsync();
    }
    public virtual async Task<List<RentalPurpose>> GetPurposesAsync()
    {
        using var db = _createContext();
        return await db.RentalPurposes.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.PurposeName).ToListAsync();
    }
    public virtual async Task<List<RentalLocation>> GetLocationsAsync()
    {
        using var db = _createContext();
        return await db.RentalLocations.AsNoTracking().Where(l => l.IsActive && db.Cities.Any(c => c.CityId == l.CityId && c.IsActive)).OrderBy(l => l.DisplayName).ToListAsync();
    }
    public Task<List<CarSearchItem>> SearchCarsAsync(string? keyword = null, int? categoryId = null, decimal? minPrice = null, decimal? maxPrice = null)
        => SearchAsync(new CarSearchFilter { Keyword = keyword, CategoryId = categoryId, MinPrice = minPrice, MaxPrice = maxPrice });

    internal static IQueryable<CarSearchItem> BuildQuery(CrmsContext db, CarSearchFilter filter)
    {
        filter.Validate();
        var now = DateTime.UtcNow;
        var days = filter.StartAtLocal.HasValue ? (decimal)Math.Ceiling((filter.EndAtLocal!.Value - filter.StartAtLocal.Value).TotalSeconds / 86400d) : 1m;
        var hours = filter.StartAtLocal.HasValue ? (decimal)(filter.EndAtLocal!.Value - filter.StartAtLocal.Value).TotalHours : 0m;
        var catalog = db.SearchCatalogRows.AsNoTracking().Where(c => c.RentalMode == filter.RentalMode && c.SearchUnitRate.HasValue);
        if (filter.RentalMode == "LongTerm") catalog = catalog.Where(c => c.DurationMonths == filter.DurationMonths);
        if (filter.StartAtLocal.HasValue)
        {
            var (start, end, _, _) = AvailabilityRules.ValidatePeriod(filter.StartAtLocal.Value, filter.EndAtLocal!.Value, now, requireFutureStart: true);
            var available = db.SearchAvailableVehicles(start, end, now);
            catalog = catalog.Where(c => available.Any(a => a.VehicleId == c.VehicleId));
        }
        if (filter.CityId.HasValue && (filter.RentalMode == "LongTerm" || (filter.RentalMode != "WithDriver" && !filter.DeliveryOnly && !filter.FreeDeliveryOnly))) catalog = catalog.Where(c => c.CityId == filter.CityId);
        if (filter.RentalMode == "WithDriver") catalog = catalog.Where(c => db.VehicleDriverRoutes.Any(r => r.VehicleId == c.VehicleId && r.IsActive && r.RouteType == filter.RouteType && r.PickupCityId == filter.CityId && r.DestinationCityId == filter.DestinationCityId));
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim();
            catalog = catalog.Where(c => c.Brand.Contains(keyword) || c.Model.Contains(keyword) || db.Vehicles.Any(v => v.VehicleId == c.VehicleId && v.LicensePlate.Contains(keyword)));
        }
        if (filter.CategoryId.HasValue) catalog = catalog.Where(c => c.CategoryId == filter.CategoryId);
        if (!string.IsNullOrWhiteSpace(filter.Brand)) catalog = catalog.Where(c => c.Brand == filter.Brand);
        if (filter.Transmission != null) catalog = catalog.Where(c => c.Transmission == filter.Transmission);
        if (filter.FuelType != null) catalog = catalog.Where(c => c.FuelType == filter.FuelType);
        if (filter.Seats.HasValue) catalog = catalog.Where(c => c.SeatCount == filter.Seats);
        if (filter.MinYear.HasValue) catalog = catalog.Where(c => c.ManufactureYear >= filter.MinYear);
        if (filter.HourlyOnly) catalog = catalog.Where(c => c.HourlyRate.HasValue && hours >= c.MinimumHours && hours <= c.MaximumHours);
        if (filter.HighlyRatedOnly) catalog = catalog.Where(c => c.IsHighlyRatedOwner == true);
        if (filter.InstantOnly) catalog = catalog.Where(c => c.SupportsInstantBooking == true);
        if (filter.NoCollateralOnly) catalog = catalog.Where(c => c.RequiresCollateral == false);
        if (filter.UnlimitedMileageOnly) catalog = catalog.Where(c => c.UnlimitedMileage == true);
        if (filter.ExtendedTravelOnly) catalog = catalog.Where(c => c.AllowsIntercityTravel == true && db.VehicleTravelAreas.Any(a => a.VehicleId == c.VehicleId && a.RentalMode == c.RentalMode && a.IsActive && a.CityId == filter.DestinationCityId));
        if (filter.ChildSeatOnly) catalog = catalog.Where(c => db.VehicleAmenities.Any(a => a.VehicleId == c.VehicleId && a.RentalMode == c.RentalMode && a.IsActive && a.Quantity > 0 && db.Amenities.Any(type => type.AmenityId == a.AmenityId && type.IsActive && (type.AmenityCode == "ChildSeat" || type.AmenityCode.StartsWith("ChildSeat:")))));
        if (filter.ChildSeatAgeCodes.Length > 0) catalog = catalog.Where(c => db.VehicleAmenities.Any(a => a.VehicleId == c.VehicleId && a.RentalMode == c.RentalMode && a.IsActive && a.Quantity > 0 && db.Amenities.Any(type => type.AmenityId == a.AmenityId && type.IsActive && (filter.ChildSeatAgeCodes.Contains(type.AmenityCode) || type.AmenityCode == "ChildSeat:0-12"))));
        if (filter.SportOnly) catalog = catalog.Where(c => db.VehicleAmenities.Any(a => a.VehicleId == c.VehicleId && a.RentalMode == c.RentalMode && a.IsActive && db.Amenities.Any(type => type.AmenityId == a.AmenityId && type.IsActive && type.AmenityCode == "SportPackage")));
        foreach (var code in filter.PurposeCodes.Distinct())
        {
            var purpose = code;
            catalog = catalog.Where(c => db.VehicleRentalPurposes.Any(p => p.VehicleId == c.VehicleId && p.RentalMode == c.RentalMode && db.RentalPurposes.Any(type => type.PurposeId == p.PurposeId && type.IsActive && type.PurposeCode == purpose)));
        }
        var city = filter.CityId ?? -1;
        var quotes = db.SearchDeliveryQuotes(city, filter.Latitude ?? 0d, filter.Longitude ?? 0d, filter.RentalMode);
        var freeCityVehicles = db.VehicleDeliveryAreas.Where(a => a.IsActive && a.CityId == city && a.RentalMode == "WithDriver" && a.ServiceRadiusKm > 0 &&
            (a.FreeWithinKm > 0 || (a.FeeModel == "Flat" && a.FlatFee == 0) || (a.FeeModel == "PerKm" && a.FeePerKm == 0 && a.MinimumFee == 0))).Select(a => a.VehicleId);
        if (filter.FreePickupInCityOnly) catalog = catalog.Where(c => freeCityVehicles.Contains(c.VehicleId));
        if (filter.DeliveryOnly || (filter.FreeDeliveryOnly && !filter.FreePickupInCityOnly)) catalog = catalog.Where(c => quotes.Any(q => q.VehicleId == c.VehicleId && (!filter.FreeDeliveryOnly || q.DeliveryAndCollectionFee == 0)));
        // This reads the unchanged daily-driver fee. Fixed-trip quotes remain explicitly separate.
        var query = catalog.Select(c => new CarSearchItem
        {
            VehicleId = c.VehicleId, CategoryId = c.CategoryId, Brand = c.Brand, Model = c.Model, CategoryName = c.CategoryName,
            OwnerRating = c.AverageRating, OwnerReviewCount = c.ReviewCount ?? 0,
            SupportsInstant = c.SupportsInstantBooking == true, NoCollateral = c.RequiresCollateral == false,
            SupportsFreeCityPickup = filter.FreePickupInCityOnly && freeCityVehicles.Contains(c.VehicleId),
            SupportsDelivery = db.VehicleDeliveryAreas.Any(a => a.VehicleId == c.VehicleId && a.RentalMode == c.RentalMode && a.IsActive),
            HasChildSeat = db.VehicleAmenities.Any(a => a.VehicleId == c.VehicleId && a.RentalMode == c.RentalMode && a.IsActive && a.Quantity > 0 && db.Amenities.Any(t => t.AmenityId == a.AmenityId && t.IsActive && (t.AmenityCode == "ChildSeat" || t.AmenityCode.StartsWith("ChildSeat:")))),
            ManufactureYear = c.ManufactureYear, SeatCount = c.SeatCount, Transmission = c.Transmission, FuelType = c.FuelType,
            DailyRate = c.DailyRate, CityName = c.CityName,
            LongTermBaseMonthlyRate = c.BaseMonthlyRate ?? 0m, LongTermMonthlyRate = c.MonthlyRate ?? 0m,
            LongTermDiscountPercent = c.DiscountPercent ?? 0m,
            LongTermTotalPrice = (c.MonthlyRate ?? 0m) * filter.DurationMonths, DurationMonths = filter.DurationMonths,
            QuotedRate = filter.HourlyOnly ? c.HourlyRate!.Value : filter.RentalMode == "WithDriver"
                ? (db.VehicleDriverRoutes.Where(r => r.VehicleId == c.VehicleId && r.IsActive && r.RouteType == filter.RouteType && r.PickupCityId == filter.CityId && r.DestinationCityId == filter.DestinationCityId).Select(r => r.TripBasePrice).FirstOrDefault() ?? (c.DailyRate + (db.RentalPolicies.Where(p => p.PolicyId == 1).Select(p => (decimal?)p.DriverDailyFee).FirstOrDefault() ?? 0m)) * days)
                : c.SearchUnitRate!.Value,
            QuoteUnit = filter.HourlyOnly ? "Hour" : filter.RentalMode == "WithDriver" ? "Trip" : c.SearchPriceUnit,
            EstimatedTripTotal = filter.HourlyOnly ? c.HourlyRate!.Value * Math.Ceiling(hours * 60m / c.BillingIncrementMinutes!.Value) * c.BillingIncrementMinutes.Value / 60m
                : filter.RentalMode == "LongTerm" ? c.MonthlyRate!.Value * filter.DurationMonths : filter.RentalMode == "WithDriver"
                ? (db.VehicleDriverRoutes.Where(r => r.VehicleId == c.VehicleId && r.IsActive && r.RouteType == filter.RouteType && r.PickupCityId == filter.CityId && r.DestinationCityId == filter.DestinationCityId).Select(r => r.TripBasePrice).FirstOrDefault() ?? (c.DailyRate + (db.RentalPolicies.Where(p => p.PolicyId == 1).Select(p => (decimal?)p.DriverDailyFee).FirstOrDefault() ?? 0m)) * days)
                : c.DailyRate * days,
            HasPromotion = filter.RentalMode == "LongTerm" && c.DiscountPercent > 0,
            CanUseLegacyBooking = (filter.RentalMode != "WithDriver" || db.VehicleDriverRoutes.Any(r => r.VehicleId == c.VehicleId && r.IsActive && r.RouteType == filter.RouteType && r.PickupCityId == filter.CityId && r.DestinationCityId == filter.DestinationCityId && r.PricingModel == "DailyPolicy")),
            DeliveryFee = filter.Latitude.HasValue && filter.Longitude.HasValue ? quotes.Where(q => q.VehicleId == c.VehicleId).Select(q => (decimal?)q.DeliveryAndCollectionFee).FirstOrDefault() : null,
            ImageUrl = db.VehicleImages.Where(i => i.VehicleId == c.VehicleId).OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).ThenBy(i => i.ImageId).Select(i => i.ImageUrl).FirstOrDefault()
        });
        if (filter.MinPrice.HasValue) query = query.Where(c => c.QuotedRate >= filter.MinPrice);
        if (filter.MaxPrice.HasValue) query = query.Where(c => c.QuotedRate <= filter.MaxPrice);
        if (filter.DiscountOnly)
        {
            if (filter.RentalMode == "LongTerm") query = query.Where(c => c.HasPromotion);
            else query = query.Where(c => db.Vouchers.Any(v =>
                (!db.VehicleVoucherEligibility.Any(e => e.VoucherId == v.VoucherId)
                 || db.VehicleVoucherEligibility.Any(e => e.VoucherId == v.VoucherId && e.VehicleId == c.VehicleId && e.RentalMode == filter.RentalMode && e.IsActive))
                && v.IsActive && v.StartsAt <= now && v.EndsAt > now && v.MinimumOrderAmount <= c.EstimatedTripTotal
                && (!v.UsageLimit.HasValue || db.BookingVouchers.Count(b => b.VoucherId == v.VoucherId && (b.RedemptionStatus == "Reserved" || b.RedemptionStatus == "Applied")) < v.UsageLimit)
                && (!filter.CustomerId.HasValue || db.BookingVouchers.Count(b => b.VoucherId == v.VoucherId && b.Booking.CustomerId == filter.CustomerId && (b.RedemptionStatus == "Reserved" || b.RedemptionStatus == "Applied")) < v.PerCustomerLimit)
                && (v.MaxDiscountAmount.HasValue && v.MaxDiscountAmount < (v.DiscountType == "Percent" ? Math.Round(c.EstimatedTripTotal * v.DiscountValue / 100m, 2) : v.DiscountValue)
                    ? v.MaxDiscountAmount.Value : (v.DiscountType == "Percent" ? Math.Round(c.EstimatedTripTotal * v.DiscountValue / 100m, 2) : v.DiscountValue)) < c.EstimatedTripTotal));
        }
        return filter.Sort switch
        {
            "Giá giảm dần" => query.OrderByDescending(c => c.QuotedRate).ThenBy(c => c.VehicleId),
            "Tên A-Z" => query.OrderBy(c => c.Brand).ThenBy(c => c.Model).ThenBy(c => c.VehicleId),
            _ => query.OrderBy(c => c.QuotedRate).ThenBy(c => c.VehicleId)
        };
    }
    public virtual async Task<List<CarSearchItem>> SearchAsync(CarSearchFilter filter, CancellationToken cancellationToken = default)
    {
        // Validate the complete snapshot before even constructing a database context.
        filter.Validate();
        using var db = _createContext();
        var cars = await BuildQuery(db, filter).ToListAsync(cancellationToken);
        if (filter.DiscountOnly) foreach (var car in cars) car.HasPromotion = true;
        return cars;
    }
}
