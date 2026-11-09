namespace CRMS.Models;

public sealed class VehicleTravelArea
{
    public long VehicleId { get; set; }
    public string RentalMode { get; set; } = null!;
    public int CityId { get; set; }
    public bool IsActive { get; set; }
}
