namespace CRMS.Models;

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
