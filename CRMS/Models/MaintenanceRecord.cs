using System;
using System.Collections.Generic;

namespace CRMS.Models;

public partial class MaintenanceRecord
{
    public long MaintenanceId { get; set; }

    public long VehicleId { get; set; }

    public long CreatedByUserId { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public string MaintenanceStatus { get; set; } = null!;

    public string Description { get; set; } = null!;

    public decimal Cost { get; set; }

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual Vehicle Vehicle { get; set; } = null!;
}
