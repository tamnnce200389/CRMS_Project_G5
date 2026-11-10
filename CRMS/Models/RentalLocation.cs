namespace CRMS.Models;

public sealed class RentalLocation
{
    public long LocationId { get; set; }
    public int CityId { get; set; }
    public string DisplayName { get; set; } = null!;
    public string AddressLine { get; set; } = null!;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsActive { get; set; }
}
