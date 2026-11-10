namespace CRMS.Models;

public sealed class SearchSetting
{
    public byte Id { get; set; }
    public decimal MinimumOwnerRating { get; set; }
    public int MinimumOwnerReviews { get; set; }
}
