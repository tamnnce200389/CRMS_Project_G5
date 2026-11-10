namespace CRMS.Models;

public sealed class RentalPurpose
{
    public int PurposeId { get; set; }
    public string PurposeCode { get; set; } = null!;
    public string PurposeName { get; set; } = null!;
    public string Definition { get; set; } = null!;
    public bool IsActive { get; set; }
}
