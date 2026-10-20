using System;
using System.Collections.Generic;

namespace CRMS.Models;

public partial class Booking
{
    public long BookingId { get; set; }

    public string BookingCode { get; set; } = null!;

    public Guid RequestKey { get; set; }

    public long CustomerId { get; set; }

    public long VehicleId { get; set; }

    public long? DriverId { get; set; }

    public string RentalType { get; set; } = null!;

    public string DepositMethod { get; set; } = null!;

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public int RentalDays { get; set; }

    public decimal VehicleDailyRate { get; set; }

    public decimal DriverDailyRate { get; set; }

    public decimal VehicleAmount { get; set; }

    public decimal DriverAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal DepositAmount { get; set; }

    public DateTime DepositDeadline { get; set; }

    public string BookingStatus { get; set; } = null!;

    public DateTime? ConfirmedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancelReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<BookingStatusHistory> BookingStatusHistories { get; set; } = new List<BookingStatusHistory>();

    public virtual BookingVoucher? BookingVoucher { get; set; }

    public virtual User Customer { get; set; } = null!;

    public virtual Driver? Driver { get; set; }

    public virtual Payment? Payment { get; set; }

    public virtual Review? Review { get; set; }

    public virtual Vehicle Vehicle { get; set; } = null!;

    public virtual VehicleHandover? VehicleHandover { get; set; }

    public virtual VehicleReturn? VehicleReturn { get; set; }
}
