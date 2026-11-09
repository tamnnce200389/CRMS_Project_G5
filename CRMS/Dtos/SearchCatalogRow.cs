namespace CRMS.Models;

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
