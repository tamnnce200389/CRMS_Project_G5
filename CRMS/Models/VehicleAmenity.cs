namespace CRMS.Models;

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
