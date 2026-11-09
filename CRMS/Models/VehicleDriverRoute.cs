namespace CRMS.Models;

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
