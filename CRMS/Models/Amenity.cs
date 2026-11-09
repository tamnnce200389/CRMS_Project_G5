namespace CRMS.Models;

public sealed class Amenity
{
    public int AmenityId { get; set; }
    public string AmenityCode { get; set; } = null!;
    public string AmenityName { get; set; } = null!;
    public bool IsActive { get; set; }
}
