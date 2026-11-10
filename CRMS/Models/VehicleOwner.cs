namespace CRMS.Models;

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
