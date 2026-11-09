namespace CRMS.Models;

public sealed class VehicleRentalPurpose
{
    public long VehicleId { get; set; }
    public string RentalMode { get; set; } = null!;
    public int PurposeId { get; set; }
    public DateTime VerifiedAt { get; set; }
}
