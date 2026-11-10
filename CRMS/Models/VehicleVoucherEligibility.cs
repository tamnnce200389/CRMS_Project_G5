namespace CRMS.Models;

public sealed class VehicleVoucherEligibility
{
    public long VehicleId { get; set; }
    public string RentalMode { get; set; } = null!;
    public long VoucherId { get; set; }
    public bool IsActive { get; set; }
}
