namespace CRMS.Models;

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
