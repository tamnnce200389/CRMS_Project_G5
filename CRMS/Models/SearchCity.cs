namespace CRMS.Models;

public sealed class SearchCity
{
    public int CityId { get; set; }
    public string CityKey { get; set; } = null!;
    public string CityName { get; set; } = null!;
    public string CountryCode { get; set; } = null!;
    public bool IsActive { get; set; }
    public string? SourceReference { get; set; }
}
