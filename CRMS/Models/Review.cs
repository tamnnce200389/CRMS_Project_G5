using System;
using System.Collections.Generic;

namespace CRMS.Models;

public partial class Review
{
    public long ReviewId { get; set; }

    public long BookingId { get; set; }

    public byte Rating { get; set; }

    public string? Comment { get; set; }

    public bool IsVisible { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Booking Booking { get; set; } = null!;
}
