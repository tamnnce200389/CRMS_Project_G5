namespace CRMS.Models;

public sealed class BookingOfferSnapshot
{
    public long BookingId { get; set; }
    public long VehicleId { get; set; }
    public long OwnerId { get; set; }
    public string RentalMode { get; set; } = null!;
    public string PriceUnit { get; set; } = null!;
    public string PolicySnapshotJson { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
