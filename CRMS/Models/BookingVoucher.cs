using System;
using System.Collections.Generic;

namespace CRMS.Models;

public partial class BookingVoucher
{
    public long BookingId { get; set; }

    public long VoucherId { get; set; }

    public decimal DiscountSnapshot { get; set; }

    public string RedemptionStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? AppliedAt { get; set; }

    public virtual Booking Booking { get; set; } = null!;

    public virtual Voucher Voucher { get; set; } = null!;
}
