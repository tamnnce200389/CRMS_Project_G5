using System.Data;
using CRMS.Data;
using CRMS.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Services.Payments;

public sealed class PaymentService : IPaymentSummaryService, IRefundRecordingService
{
    private readonly Func<CrmsContext> _create;
    private readonly Func<long?> _userId;
    public PaymentService() : this(CrmsContextFactory.Create, () => CurrentUserService.Instance.UserId) { }
    public PaymentService(Func<CrmsContext> create, Func<long?> userId)
    { _create = create; _userId = userId; }

    private async Task<long> Authorize(CrmsContext db, Booking? booking = null)
    {
        var actor = _userId() ?? throw new InvalidOperationException("Vui lòng đăng nhập.");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == actor);
        if (user is null || !user.IsActive ||
            !(user.UserRole is "Staff" or "Admin" ||
              (booking is not null && user.UserRole == "Customer" && booking.CustomerId == actor)))
            throw new InvalidOperationException("Bạn không có quyền thực hiện thao tác này.");
        return actor;
    }

    private void CheckSession(long actor)
    {
        if (_userId() != actor) throw new InvalidOperationException("Phiên đăng nhập đã thay đổi. Hãy tải lại.");
    }

    public async Task<PaymentSummary> GetPaymentSummary(long bookingId)
    {
        using var db = _create();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(b => b.BookingId == bookingId)
            ?? throw new InvalidOperationException("Không tìm thấy đơn thuê.");
        var actor = await Authorize(db, booking);
        var summary = await ReadSummary(db, booking);
        CheckSession(actor);
        await tx.CommitAsync();
        return summary;
    }

    public async Task<decimal> GetOutstandingBalance(long bookingId)
        => (await GetPaymentSummary(bookingId)).OutstandingBalance;

    // Booking row serializes all TV3 writes, including retries from different application windows.
    private static async Task<Booking> LockBooking(CrmsContext db, long id)
        => await db.Bookings.FromSqlInterpolated(
            $"SELECT * FROM dbo.Bookings WITH (UPDLOCK, HOLDLOCK) WHERE BookingId = {id}")
            .SingleOrDefaultAsync() ?? throw new InvalidOperationException("Không tìm thấy đơn thuê.");

    private static async Task<PaymentSummary> ReadSummary(CrmsContext db, Booking booking)
    {
        var payments = await db.Payments.AsNoTracking().Where(p => p.BookingId == booking.BookingId).ToListAsync();
        var returned = await db.VehicleReturns.AsNoTracking().SingleOrDefaultAsync(r => r.BookingId == booking.BookingId);
        return PaymentRules.Summarize(booking, payments, returned);
    }

    private static async Task<Payment?> FindReplay(CrmsContext db, long bookingId,
        string type, string method, decimal amount, Guid key, string? note = null)
    {
        if (key == Guid.Empty) throw new InvalidOperationException("Thiếu mã yêu cầu chống giao dịch trùng.");
        var existing = await db.Payments.SingleOrDefaultAsync(p => p.IdempotencyKey == key);
        if (existing is not null && (existing.BookingId != bookingId || existing.PaymentType != type ||
            existing.PaymentMethod != method || existing.Amount != amount || existing.PaymentStatus != "Success" ||
            (note is not null && existing.Note != note)))
            throw new InvalidOperationException("Mã yêu cầu đã được dùng cho một giao dịch khác.");
        return existing;
    }

    private static Payment NewPayment(long bookingId, long actor, string type,
        string method, decimal amount, Guid key, string? note = null) => new()
    {
        BookingId = bookingId, VerifiedByUserId = actor, PaymentType = type,
        PaymentMethod = method, Amount = amount, IdempotencyKey = key,
        PaymentStatus = "Success", CreatedAt = DateTime.UtcNow, PaidAt = DateTime.UtcNow, Note = note
    };

    public async Task<PaymentReceipt> ConfirmCashDeposit(long bookingId, decimal expectedAmount, Guid requestKey)
    {
        using var db = _create();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var actor = await Authorize(db);
        var booking = await LockBooking(db, bookingId);
        var replay = await FindReplay(db, bookingId, "Deposit", "Cash", expectedAmount, requestKey);
        if (replay is not null)
        {
            CheckSession(actor);
            await tx.CommitAsync();
            return new(replay.PaymentId, true, booking.BookingStatus is "Expired" or "Cancelled");
        }
        PaymentRules.ValidateCashDeposit(booking, expectedAmount, DateTime.UtcNow);
        if (await db.Payments.AnyAsync(p => p.BookingId == bookingId && p.PaymentType == "Deposit" && p.PaymentStatus == "Success"))
            throw new InvalidOperationException("Đơn này đã có giao dịch cọc thành công.");
        var payment = NewPayment(bookingId, actor, "Deposit", "Cash", expectedAmount, requestKey);
        payment.PaymentStatus = "Pending";
        payment.PaidAt = null;
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        var needsRefund = new SqlParameter("@NeedsRefund", SqlDbType.Bit) { Direction = ParameterDirection.Output };
        await db.Database.ExecuteSqlRawAsync(
            "EXEC dbo.usp_ConfirmDeposit @PaymentId=@Id, @VerifiedByUserId=@Actor, @NeedsManualRefund=@NeedsRefund OUTPUT",
            new SqlParameter("@Id", payment.PaymentId), new SqlParameter("@Actor", actor), needsRefund);
        CheckSession(actor);
        await tx.CommitAsync();
        return new(payment.PaymentId, false, (bool)needsRefund.Value);
    }

    public async Task<PaymentReceipt> RecordFinalPayment(long bookingId, decimal expectedAmount, Guid requestKey)
    {
        using var db = _create();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var actor = await Authorize(db);
        var booking = await LockBooking(db, bookingId);
        var replay = await FindReplay(db, bookingId, "Balance", "Cash", expectedAmount, requestKey);
        if (replay is not null)
        {
            CheckSession(actor);
            await tx.CommitAsync();
            return new(replay.PaymentId, true, false);
        }
        PaymentRules.ValidateFinalPayment(await ReadSummary(db, booking), expectedAmount);
        var payment = NewPayment(bookingId, actor, "Balance", "Cash", expectedAmount, requestKey);
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        await Complete(db, bookingId, actor);
        CheckSession(actor);
        await tx.CommitAsync();
        return new(payment.PaymentId, false, false);
    }

    public async Task CompletePaidBooking(long bookingId)
    {
        using var db = _create();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var actor = await Authorize(db);
        var booking = await LockBooking(db, bookingId);
        if (booking.BookingStatus != "Completed")
        {
            if (!(await ReadSummary(db, booking)).CanComplete)
                throw new InvalidOperationException("Đơn phải có biên bản trả xe và đã thanh toán đủ.");
            await Complete(db, bookingId, actor);
        }
        CheckSession(actor);
        await tx.CommitAsync();
    }

    private static Task<int> Complete(CrmsContext db, long bookingId, long actor)
        => db.Database.ExecuteSqlInterpolatedAsync(
            $"EXEC dbo.usp_CompleteBooking @BookingId={bookingId}, @StaffUserId={actor}");

    public async Task<PaymentReceipt> RecordManualRefund(long bookingId, decimal amount,
        string method, string approvalReference, Guid requestKey)
    {
        var reference = approvalReference?.Trim() ?? "";
        using var db = _create();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var actor = await Authorize(db);
        var booking = await LockBooking(db, bookingId);
        var replay = await FindReplay(db, bookingId, "Refund", method, amount, requestKey, reference);
        if (replay is not null)
        {
            CheckSession(actor);
            await tx.CommitAsync();
            return new(replay.PaymentId, true, false);
        }
        PaymentRules.ValidateRefund(await ReadSummary(db, booking), amount, method, reference);
        // One reconciliation reference per booking, even if another window generates a new request key.
        if (await db.Payments.AnyAsync(p => p.BookingId == bookingId && p.PaymentType == "Refund" &&
            p.PaymentStatus == "Success" && p.Note == reference))
            throw new InvalidOperationException("Mã duyệt/đối soát này đã được ghi nhận hoàn tiền.");
        var payment = NewPayment(bookingId, actor, "Refund", method, amount, requestKey, reference);
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        CheckSession(actor);
        await tx.CommitAsync();
        return new(payment.PaymentId, false, false);
    }
}
