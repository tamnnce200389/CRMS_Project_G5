namespace CRMS.Models;

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
