namespace CRMS.Models;

public sealed class SearchDeliveryQuote
{
    public long VehicleId { get; set; }
    public string RentalMode { get; set; } = "";
    public int CityId { get; set; }
    public double DistanceKm { get; set; }
    public decimal DeliveryAndCollectionFee { get; set; }
}
