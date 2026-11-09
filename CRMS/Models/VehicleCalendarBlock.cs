namespace CRMS.Models;

public sealed class VehicleCalendarBlock
{
    public long BlockId { get; set; }
    public long VehicleId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Reason { get; set; } = null!;
    public bool IsActive { get; set; }
    public long CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
