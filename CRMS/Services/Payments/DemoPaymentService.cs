using System.Data;
using CRMS.Data;
using CRMS.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Services.Payments;

public sealed record DemoPaymentSession(long BookingId, string BookingCode, string BookingStatus,
    decimal RentalTotal, decimal SimulatedDeposit, decimal TransferAmount, decimal ActualReceived,
    string TransferContent, string BankBin, string BankName, string AccountNumber, string AccountName,
    string PaymentStatus, bool ConfirmedBooking, DateTime Deadline, DateTime? PaidAt)
{
    public string EffectiveBookingStatus => BookingStatusRules.EffectiveStatus(BookingStatus, Deadline);
    public bool CanTransfer => PaymentStatus == "Pending" && BookingStatus == "PendingDeposit" && Deadline > DateTime.UtcNow;
    public bool IsSuccessful => PaymentStatus == "Success" && ConfirmedBooking;
}

public sealed record DemoPaymentReceipt(long DemoPaymentId, bool AlreadyRecorded, bool NeedsManualRefund);

public interface IDemoPaymentService
{
    Task<DemoPaymentSession> GetSessionAsync(long bookingId, CancellationToken cancellationToken = default);
    Task<DemoPaymentReceipt> ConfirmReceivedAsync(long bookingId, decimal actualAmount, string bankTransactionReference, Guid requestKey);
}

public sealed class DemoPaymentService : IDemoPaymentService
{
    private readonly Func<CrmsContext> _create;
    private readonly Func<long?> _userId;
    private readonly DemoPaymentOptions? _options;
    public DemoPaymentService() : this(CrmsContextFactory.Create, () => CurrentUserService.Instance.UserId) { }
    public DemoPaymentService(Func<CrmsContext> create, Func<long?> userId, DemoPaymentOptions? options = null)
    { _create = create; _userId = userId; _options = options; }

    internal static DemoPayment NewPending(Booking booking, DemoPaymentOptions options) => new()
    {
        BookingId = booking.BookingId, SimulatedDepositAmount = booking.DepositAmount,
        RequiredTransferAmount = DemoPaymentOptions.TransferAmount, ActualAmount = 0,
        TransferContent = "DEMO" + Guid.NewGuid().ToString("N")[..20].ToUpperInvariant(),
        BankBin = options.BankBin, BankName = options.BankName, AccountNumber = options.AccountNumber,
        AccountName = options.AccountName, PaymentStatus = "Pending", CreatedAt = DateTime.UtcNow
    };

    private void CheckSession(long actor)
    {
        if (_userId() != actor) throw new InvalidOperationException("Phiên đăng nhập đã thay đổi. Vui lòng tải lại.");
    }

    private async Task<long> Authorize(CrmsContext db, Booking booking, bool staffOnly, CancellationToken ct = default)
    {
        var actor = _userId() ?? throw new InvalidOperationException("Vui lòng đăng nhập.");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == actor, ct);
        if (user == null || !user.IsActive || !(user.UserRole is "Staff" or "Admin" ||
            (!staffOnly && user.UserRole == "Customer" && booking.CustomerId == actor)))
            throw new InvalidOperationException("Bạn không có quyền thao tác thanh toán demo của đơn này.");
        return actor;
    }

    public async Task<DemoPaymentSession> GetSessionAsync(long bookingId, CancellationToken cancellationToken = default)
    {
        using var db = _create();
        // Read the booking and receipt under the same lock used by Staff confirmation.
        // A poll must never combine an old booking status with a newer receipt status.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var booking = await db.Bookings.FromSqlInterpolated($"SELECT * FROM dbo.Bookings WITH(UPDLOCK,HOLDLOCK) WHERE BookingId={bookingId}")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy đơn thuê.");
        var actor = await Authorize(db, booking, false, cancellationToken);
        if (!booking.IsDemo) throw new InvalidOperationException("Đơn này không phải đơn thanh toán demo.");
        var payment = await db.DemoPayments.AsNoTracking().SingleOrDefaultAsync(p => p.BookingId == bookingId, cancellationToken)
            ?? throw new InvalidOperationException("Chưa có yêu cầu thanh toán demo cho đơn này.");
        CheckSession(actor);
        await tx.CommitAsync(cancellationToken);
        return new(booking.BookingId, booking.BookingCode, booking.BookingStatus, booking.TotalAmount,
            booking.DepositAmount, payment.RequiredTransferAmount, payment.ActualAmount, payment.TransferContent,
            payment.BankBin, payment.BankName, payment.AccountNumber, payment.AccountName, payment.PaymentStatus,
            payment.ConfirmedBooking, booking.DepositDeadline, payment.PaidAt);
    }

    public async Task<DemoPaymentReceipt> ConfirmReceivedAsync(long bookingId, decimal actualAmount, string bankTransactionReference, Guid requestKey)
    {
        (_options ?? DemoPaymentOptions.Load()).RequireEnabled();
        if (actualAmount != DemoPaymentOptions.TransferAmount)
            throw new InvalidOperationException("Chỉ xác nhận đúng khoản thực chuyển thử nghiệm 1.000đ, không phải tiền cọc mô phỏng.");
        var reference = bankTransactionReference?.Trim().ToUpperInvariant() ?? "";
        if (reference.Length is < 3 or > 100 || reference.Any(char.IsControl) || requestKey == Guid.Empty)
            throw new InvalidOperationException("Nhập mã giao dịch ngân hàng hợp lệ để đối soát khoản tiền demo.");
        using var db = _create();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var booking = await db.Bookings.FromSqlInterpolated($"SELECT * FROM dbo.Bookings WITH(UPDLOCK,HOLDLOCK) WHERE BookingId={bookingId}")
            .SingleOrDefaultAsync() ?? throw new InvalidOperationException("Không tìm thấy đơn thuê.");
        var actor = await Authorize(db, booking, true);
        if (!booking.IsDemo) throw new InvalidOperationException("Không được xác nhận tiền demo cho đơn thanh toán thật.");
        var payment = await db.DemoPayments.SingleOrDefaultAsync(p => p.BookingId == bookingId)
            ?? throw new InvalidOperationException("Không tìm thấy yêu cầu chuyển khoản demo.");
        if (payment.PaymentStatus == "Success")
        {
            if (payment.BankTransactionReference != reference || payment.ActualAmount != actualAmount)
                throw new InvalidOperationException("Đơn đã ghi nhận tiền demo. Không thu hoặc xác nhận thêm tiền.");
            CheckSession(actor);
            await tx.CommitAsync();
            return new(payment.DemoPaymentId, true, !payment.ConfirmedBooking || booking.BookingStatus is "Cancelled" or "Expired");
        }
        if (payment.PaymentStatus != "Pending" || booking.BookingStatus is not ("PendingDeposit" or "Cancelled" or "Expired"))
            throw new InvalidOperationException("Trạng thái đơn không phù hợp để xác nhận khoản tiền demo.");
        if (await db.DemoPayments.AnyAsync(p => p.BankTransactionReference == reference || p.ConfirmationRequestKey == requestKey))
            throw new InvalidOperationException("Mã giao dịch hoặc yêu cầu đã được dùng cho một khoản demo khác.");
        if (await db.Payments.AnyAsync(p => p.BookingId == bookingId && p.PaymentType != "Refund" && p.PaymentStatus == "Success"))
            throw new InvalidOperationException("Đơn demo có dữ liệu thu tiền không phù hợp. Cần đối soát trước khi xác nhận.");
        var now = await db.Database.SqlQueryRaw<DateTime>("SELECT SYSUTCDATETIME() AS [Value]").SingleAsync();
        var canConfirm = booking.BookingStatus == "PendingDeposit" && booking.DepositDeadline > now;
        payment.SimulatedDepositAmount = booking.DepositAmount;
        payment.ActualAmount = actualAmount;
        payment.PaymentStatus = "Success";
        payment.BankTransactionReference = reference;
        payment.ConfirmationRequestKey = requestKey;
        payment.VerifiedByUserId = actor;
        payment.PaidAt = now;
        payment.ConfirmedBooking = canConfirm;
        if (booking.BookingStatus == "PendingDeposit")
        {
            booking.BookingStatus = canConfirm ? "Confirmed" : "Expired";
            booking.ConfirmedAt = canConfirm ? now : null;
            booking.UpdatedAt = now;
            var voucher = await db.BookingVouchers.SingleOrDefaultAsync(v => v.BookingId == bookingId);
            if (voucher is { RedemptionStatus: "Reserved" })
            {
                voucher.RedemptionStatus = canConfirm ? "Applied" : "Released";
                voucher.AppliedAt = canConfirm ? now : null;
            }
            db.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingId = bookingId, OldStatus = "PendingDeposit", NewStatus = booking.BookingStatus,
                ChangedByUserId = actor, ChangedAt = now,
                ChangeNote = canConfirm ? "QR DEMO: received 1,000 VND; simulated deposit confirmed."
                    : "QR DEMO: received after deadline; reconcile actual 1,000 VND refund."
            });
        }
        CheckSession(actor);
        try
        {
            await db.SaveChangesAsync();
            CheckSession(actor);
            await tx.CommitAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new InvalidOperationException("Mã giao dịch ngân hàng đã được ghi nhận. Vui lòng tải lại để đối soát.", ex);
        }
        return new(payment.DemoPaymentId, false, !canConfirm);
    }
}
