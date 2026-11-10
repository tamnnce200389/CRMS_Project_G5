using CRMS.Services.Vehicles;
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
        if (DeliveryOnly && CityId is null or <= 0)
            throw new ArgumentException("Chọn khu vực tìm xe trước khi bật giao nhận tận nơi.");
        if (FreeDeliveryOnly && !FreePickupInCityOnly)
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

