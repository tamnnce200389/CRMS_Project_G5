using System;
using System.Collections.Generic;

namespace CRMS.Models;

public partial class BookingStatusHistory
{
    public long HistoryId { get; set; }

    public long BookingId { get; set; }

    public string? OldStatus { get; set; }

    public string NewStatus { get; set; } = null!;

    public long? ChangedByUserId { get; set; }

    public DateTime ChangedAt { get; set; }

    public string? ChangeNote { get; set; }

    public virtual Booking Booking { get; set; } = null!;

    public virtual User? ChangedByUser { get; set; }
}
