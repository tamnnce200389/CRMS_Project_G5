using System;
using System.Collections.Generic;

namespace CRMS.Models;

public partial class VehicleReturn
{
    public long ReturnId { get; set; }

    public long BookingId { get; set; }

    public long StaffId { get; set; }

    public DateTime ReturnedAt { get; set; }

    public int OdometerInKm { get; set; }

    public byte FuelPercentIn { get; set; }

    public decimal LateFee { get; set; }

    public decimal DamageFee { get; set; }

    public decimal OtherFee { get; set; }

    public string? InspectionNotes { get; set; }

    public virtual Booking Booking { get; set; } = null!;

    public virtual User Staff { get; set; } = null!;
}
