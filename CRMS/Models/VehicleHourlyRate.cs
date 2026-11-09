namespace CRMS.Models;

public sealed class VehicleHourlyRate
{
    public long VehicleId { get; set; }
    public string RentalMode { get; set; } = null!;
    public decimal HourlyRate { get; set; }
    public byte MinimumHours { get; set; }
    public byte MaximumHours { get; set; }
    public short BillingIncrementMinutes { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}
