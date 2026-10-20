using System;
using System.Collections.Generic;

namespace CRMS.Models;

public partial class Payment
{
    public long PaymentId { get; set; }

    public long BookingId { get; set; }

    public string PaymentType { get; set; } = null!;

    public string PaymentMethod { get; set; } = null!;

    public decimal Amount { get; set; }

    public string PaymentStatus { get; set; } = null!;

    public string? GatewayTxnRef { get; set; }

    public string? GatewayTransactionNo { get; set; }

    public string? GatewayResponseCode { get; set; }

    public Guid? IdempotencyKey { get; set; }

    public long? VerifiedByUserId { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? Note { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Booking Booking { get; set; } = null!;

    public virtual User? VerifiedByUser { get; set; }
}
