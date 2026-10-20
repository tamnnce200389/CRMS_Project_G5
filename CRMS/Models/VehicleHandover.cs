using System;
using System.Collections.Generic;

namespace CRMS.Models;

public partial class VehicleHandover
{
    public long HandoverId { get; set; }

    public long BookingId { get; set; }

    public long StaffId { get; set; }

    public DateTime HandedOverAt { get; set; }

    public int OdometerOutKm { get; set; }

    public byte FuelPercentOut { get; set; }

    public string? ConditionNotes { get; set; }

    public virtual Booking Booking { get; set; } = null!;

    public virtual User Staff { get; set; } = null!;
}
