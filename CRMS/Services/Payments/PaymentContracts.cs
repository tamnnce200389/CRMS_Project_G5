using CRMS.Models;

namespace CRMS.Services.Payments;

public sealed record PaymentSummary(long BookingId, string BookingCode, string Status,
    decimal RentalTotal, decimal Discount, decimal DepositRequired, decimal ExtraFees,
    decimal DepositsPaid, decimal BalancesPaid, decimal RefundsPaid, bool HasReturn)
{
    public decimal NetPaid => DepositsPaid + BalancesPaid - RefundsPaid;
    public decimal TotalDue => RentalTotal + ExtraFees;
    public decimal OutstandingBalance => Status is "Cancelled" or "Expired" ? 0 : Math.Max(0, TotalDue - NetPaid);
    public decimal RefundableAmount => Status is "Cancelled" or "Expired"
        ? Math.Max(0, NetPaid) : 0;
    public bool CanComplete => Status == "Returned" && HasReturn && OutstandingBalance == 0;
}

public sealed record PaymentReceipt(long PaymentId, bool AlreadyRecorded, bool NeedsManualRefund);

public interface IPaymentSummaryService
{
    Task<PaymentSummary> GetPaymentSummary(long bookingId);
    Task<decimal> GetOutstandingBalance(long bookingId);
}

public interface IRefundRecordingService
{
    // TV7 supplies a stable request key and approval/reconciliation reference.
    Task<PaymentReceipt> RecordManualRefund(long bookingId, decimal amount,
        string method, string approvalReference, Guid requestKey);
}

public static class PaymentRules
{
    public static void ValidateAmount(decimal amount)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount)
            throw new InvalidOperationException("Số tiền phải lớn hơn 0 và có tối đa 2 chữ số thập phân.");
    }

    public static void ValidateCashDeposit(Booking booking, decimal amount, DateTime nowUtc)
    {
        ValidateAmount(amount);
        if (booking.BookingStatus != "PendingDeposit" || booking.DepositMethod != "Cash")
            throw new InvalidOperationException("Đơn không còn ở trạng thái chờ cọc tiền mặt.");
        if (booking.DepositDeadline <= nowUtc)
            throw new InvalidOperationException("Đơn đã hết hạn cọc. Không tiếp tục nhận tiền cho đơn này.");
        var required = decimal.Round(booking.TotalAmount * 0.5m, 2, MidpointRounding.AwayFromZero);
        if (booking.DepositAmount != required || amount != booking.DepositAmount)
            throw new InvalidOperationException("Số tiền cọc phải đúng 50% tổng tiền sau giảm giá. Hãy tải lại đơn.");
    }

    public static PaymentSummary Summarize(Booking booking, IEnumerable<Payment> payments, VehicleReturn? returned)
    {
        var paid = payments.Where(p => p.BookingId == booking.BookingId && p.PaymentStatus == "Success").ToList();
        return new(booking.BookingId, booking.BookingCode, booking.BookingStatus,
            booking.TotalAmount, booking.DiscountAmount, booking.DepositAmount,
            returned is null ? 0 : returned.LateFee + returned.DamageFee + returned.OtherFee,
            paid.Where(p => p.PaymentType == "Deposit").Sum(p => p.Amount),
            paid.Where(p => p.PaymentType == "Balance").Sum(p => p.Amount),
            paid.Where(p => p.PaymentType == "Refund").Sum(p => p.Amount), returned is not null);
    }

    public static void ValidateFinalPayment(PaymentSummary summary, decimal amount)
    {
        ValidateAmount(amount);
        if (summary.Status != "Returned" || !summary.HasReturn)
            throw new InvalidOperationException("Chỉ thu tiền cuối khi có biên bản trả xe và đơn ở trạng thái Đã trả xe.");
        if (amount != summary.OutstandingBalance)
            throw new InvalidOperationException("Số tiền còn lại đã thay đổi hoặc đã thu đủ. Hãy tải lại trước khi xác nhận.");
    }

    public static void ValidateRefund(PaymentSummary summary, decimal amount, string method, string reference)
    {
        ValidateAmount(amount);
        if (method is not ("Cash" or "BankTransfer"))
            throw new InvalidOperationException("Hoàn tiền thủ công chỉ dùng tiền mặt hoặc chuyển khoản.");
        if (string.IsNullOrWhiteSpace(reference) || reference.Trim().Length > 400)
            throw new InvalidOperationException("Nhập mã duyệt/đối soát hoàn tiền (tối đa 400 ký tự).");
        if (amount > summary.RefundableAmount)
            throw new InvalidOperationException("Chỉ hoàn cho đơn đã hủy/hết hạn và không vượt số tiền thực nhận còn lại.");
    }
}
