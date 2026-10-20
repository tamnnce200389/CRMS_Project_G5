using System;
using System.Collections.Generic;

namespace CRMS.Models;

public partial class Voucher
{
    public long VoucherId { get; set; }

    public string VoucherCode { get; set; } = null!;

    public string DiscountType { get; set; } = null!;

    public decimal DiscountValue { get; set; }

    public decimal? MaxDiscountAmount { get; set; }

    public decimal MinimumOrderAmount { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    public int? UsageLimit { get; set; }

    public int PerCustomerLimit { get; set; }

    public bool IsActive { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<BookingVoucher> BookingVouchers { get; set; } = new List<BookingVoucher>();
}
