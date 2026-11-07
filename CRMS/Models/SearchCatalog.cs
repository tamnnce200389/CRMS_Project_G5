// Search metadata in the isolated database. Existing domain models are unchanged.
namespace CRMS.Models;

public sealed class SearchSetting
{
    public byte Id { get; set; }
    public decimal MinimumOwnerRating { get; set; }
    public int MinimumOwnerReviews { get; set; }
}

public sealed class SearchCity
{
    public int CityId { get; set; }
    public string CityKey { get; set; } = null!;
    public string CityName { get; set; } = null!;
    public string CountryCode { get; set; } = null!;
    public bool IsActive { get; set; }
    public string? SourceReference { get; set; }
}

public sealed class RentalLocation
{
    public long LocationId { get; set; }
    public int CityId { get; set; }
    public string DisplayName { get; set; } = null!;
    public string AddressLine { get; set; } = null!;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsActive { get; set; }
}

public sealed class VehicleOwner
{
    public long OwnerId { get; set; }
    public long? UserId { get; set; }
    public string OwnerType { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? ContactPhone { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class VehicleSearchProfile
{
    public long VehicleId { get; set; }
    public long? OwnerId { get; set; }
    public long? LocationId { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}

public sealed class VehicleRentalMode
{
    public long VehicleId { get; set; }
    public string RentalMode { get; set; } = null!;
    public bool IsEnabled { get; set; }
    public DateTime? VerifiedAt { get; set; }
}

public sealed class VehicleRentalPolicy
{
    public long VehicleId { get; set; }
    public string RentalMode { get; set; } = null!;
    public bool? SupportsInstantBooking { get; set; }
    public bool? RequiresCollateral { get; set; }
    public decimal? CollateralAmount { get; set; }
    public string? CollateralDescription { get; set; }
    public bool? UnlimitedMileage { get; set; }
    public int? IncludedKm { get; set; }
    public string? MileagePeriod { get; set; }
    public decimal? ExcessKmFee { get; set; }
    public bool? AllowsIntercityTravel { get; set; }
    public DateTime UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}

public sealed class VehicleHourlyRate
{
    public long VehicleId { get; set; }
    public string RentalMode { get; set; } = null!;
    public decimal HourlyRate { get; set; }
    public byte MinimumHours { get; set; }
    public byte MaximumHours { get; set; }
    public short BillingIncrementMinutes { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class Amenity
{
    public int AmenityId { get; set; }
    public string AmenityCode { get; set; } = null!;
    public string AmenityName { get; set; } = null!;
    public bool IsActive { get; set; }
}

public sealed class VehicleAmenity
{
    public long VehicleId { get; set; }
    public string RentalMode { get; set; } = null!;
    public int AmenityId { get; set; }
    public short Quantity { get; set; }
    public decimal AdditionalFee { get; set; }
    public string FeeUnit { get; set; } = null!;
    public bool IsActive { get; set; }
}

public sealed class RentalPurpose
{
    public int PurposeId { get; set; }
    public string PurposeCode { get; set; } = null!;
    public string PurposeName { get; set; } = null!;
    public string Definition { get; set; } = null!;
    public bool IsActive { get; set; }
}

public sealed class VehicleRentalPurpose
{
    public long VehicleId { get; set; }
    public string RentalMode { get; set; } = null!;
    public int PurposeId { get; set; }
    public DateTime VerifiedAt { get; set; }
}

public sealed class VehicleTravelArea
{
    public long VehicleId { get; set; }
    public string RentalMode { get; set; } = null!;
    public int CityId { get; set; }
    public bool IsActive { get; set; }
}

public sealed class VehicleDeliveryArea
{
    public long VehicleId { get; set; }
    public string RentalMode { get; set; } = null!;
    public int CityId { get; set; }
    public string FeeModel { get; set; } = null!;
    public decimal? FlatFee { get; set; }
    public decimal? FeePerKm { get; set; }
    public decimal? MinimumFee { get; set; }
    public decimal ServiceRadiusKm { get; set; }
    public decimal FreeWithinKm { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class VehicleDriverRoute
{
    public long RouteId { get; set; }
    public long VehicleId { get; set; }
    public string RentalMode { get; set; } = null!;
    public string RouteType { get; set; } = null!;
    public int PickupCityId { get; set; }
    public int DestinationCityId { get; set; }
    public string PricingModel { get; set; } = null!;
    public decimal? TripBasePrice { get; set; }
    public int? IncludedKm { get; set; }
    public short? IncludedHours { get; set; }
    public decimal? ExcessKmFee { get; set; }
    public decimal? ExcessHourFee { get; set; }
    public bool IsActive { get; set; }
}

public sealed class VehicleVoucherEligibility
{
    public long VehicleId { get; set; }
    public string RentalMode { get; set; } = null!;
    public long VoucherId { get; set; }
    public bool IsActive { get; set; }
}

public sealed class VehicleCalendarBlock
{
    public long BlockId { get; set; }
    public long VehicleId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Reason { get; set; } = null!;
    public bool IsActive { get; set; }
    public long CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class BookingOfferSnapshot
{
    public long BookingId { get; set; }
    public long VehicleId { get; set; }
    public long OwnerId { get; set; }
    public string RentalMode { get; set; } = null!;
    public string PriceUnit { get; set; } = null!;
    public string PolicySnapshotJson { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public sealed class SearchOwnerRating
{
    public long OwnerId { get; set; }
    public long? ReviewCount { get; set; }
    public decimal? AverageRating { get; set; }
}

public sealed class SearchCatalogRow
{
    public long VehicleId { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public string Brand { get; set; } = null!;
    public string Model { get; set; } = null!;
    public short ManufactureYear { get; set; }
    public byte SeatCount { get; set; }
    public string Transmission { get; set; } = null!;
    public string FuelType { get; set; } = null!;
    public decimal DailyRate { get; set; }
    public string RentalMode { get; set; } = null!;
    public long? OwnerId { get; set; }
    public string OwnerName { get; set; } = null!;
    public long LocationId { get; set; }
    public int CityId { get; set; }
    public string CityName { get; set; } = null!;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public byte? DurationMonths { get; set; }
    public decimal? BaseMonthlyRate { get; set; }
    public decimal? MonthlyRate { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? SearchUnitRate { get; set; }
    public string SearchPriceUnit { get; set; } = null!;
    public bool? SupportsInstantBooking { get; set; }
    public bool? RequiresCollateral { get; set; }
    public decimal? CollateralAmount { get; set; }
    public bool? UnlimitedMileage { get; set; }
    public int? IncludedKm { get; set; }
    public string? MileagePeriod { get; set; }
    public decimal? ExcessKmFee { get; set; }
    public bool? AllowsIntercityTravel { get; set; }
    public decimal? HourlyRate { get; set; }
    public byte? MinimumHours { get; set; }
    public byte? MaximumHours { get; set; }
    public short? BillingIncrementMinutes { get; set; }
    public long? ReviewCount { get; set; }
    public decimal? AverageRating { get; set; }
    public bool? IsHighlyRatedOwner { get; set; }
}

public sealed class SearchAvailableVehicle { public long VehicleId { get; set; } }
public sealed class SearchDeliveryQuote { public long VehicleId { get; set; } public string RentalMode { get; set; } = ""; public int CityId { get; set; } public double DistanceKm { get; set; } public decimal DeliveryAndCollectionFee { get; set; } }
