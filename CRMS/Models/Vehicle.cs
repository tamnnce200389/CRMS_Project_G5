using System;
using System.Collections.Generic;

namespace CRMS.Models;

public partial class Vehicle
{
    public long VehicleId { get; set; }

    public int CategoryId { get; set; }

    public string LicensePlate { get; set; } = null!;

    public string Brand { get; set; } = null!;

    public string Model { get; set; } = null!;

    public short ManufactureYear { get; set; }

    public byte SeatCount { get; set; }

    public string Transmission { get; set; } = null!;

    public string FuelType { get; set; } = null!;

    public decimal DailyRate { get; set; }

    public int OdometerKm { get; set; }

    public string OperationalStatus { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual VehicleCategory Category { get; set; } = null!;

    public virtual ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();

    public virtual VehicleImage? VehicleImage { get; set; }
}
