namespace CRMS.Models;

public sealed class DemoPayment
{
    public long DemoPaymentId { get; set; }
    public long BookingId { get; set; }
    public decimal SimulatedDepositAmount { get; set; }
    public decimal RequiredTransferAmount { get; set; } = 1000m;
    public decimal ActualAmount { get; set; }
    public string TransferContent { get; set; } = "";
    public string BankBin { get; set; } = "";
    public string BankName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string PaymentStatus { get; set; } = "Pending";
    public string? BankTransactionReference { get; set; }
    public Guid? ConfirmationRequestKey { get; set; }
    public long? VerifiedByUserId { get; set; }
    public DateTime? PaidAt { get; set; }
    public bool ConfirmedBooking { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
