using System;
using System.Collections.Generic;

namespace CRMS.Models;

public partial class Driver
{
    public long DriverId { get; set; }

    public string FullName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? LicenseNumber { get; set; }

    public bool IsActive { get; set; }

    public string? Notes { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
