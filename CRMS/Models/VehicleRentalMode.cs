namespace CRMS.Models;

public sealed class VehicleRentalMode
{
    public long VehicleId { get; set; }
    public string RentalMode { get; set; } = null!;
    public bool IsEnabled { get; set; }
    public DateTime? VerifiedAt { get; set; }
}
