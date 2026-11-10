namespace CRMS.Models;

public sealed class SearchOwnerRating
{
    public long OwnerId { get; set; }
    public long? ReviewCount { get; set; }
    public decimal? AverageRating { get; set; }
}
