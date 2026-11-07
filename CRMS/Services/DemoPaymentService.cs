using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CRMS.Data;
using CRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Services;

public sealed class DemoPaymentInfo
{
    public long BookingId { get; init; }
    public string BookingCode { get; init; } = "";
    public string VehicleName { get; init; } = "";
    public decimal DepositAmount { get; init; }
    public DateTime DepositDeadline { get; init; }
    public string BookingStatus { get; init; } = "";
    public string? PaymentStatus { get; init; }
}

public sealed class DemoPaymentService
{
    private static DateTime EnsureUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Local ? dt.ToUniversalTime() : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    public async Task<DemoPaymentInfo> GetInfoAsync(
        long bookingId, long customerId, CancellationToken ct = default)
    {
        await using var db = CrmsContextFactory.Create();
        var booking = await db.Bookings
            .AsNoTracking()
            .Include(b => b.Vehicle)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customerId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy Booking thuộc tài khoản của bạn.");

        if (booking.DepositMethod != "VNPay")
            throw new InvalidOperationException("Booking này không sử dụng phương thức VNPay.");

        var depositPayment = booking.Payments
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefault(p => p.PaymentType == "Deposit");

        return new DemoPaymentInfo
        {
            BookingId = booking.BookingId,
            BookingCode = booking.BookingCode,
            VehicleName = booking.Vehicle.Brand + " " + booking.Vehicle.Model,
            DepositAmount = booking.DepositAmount,
            DepositDeadline = EnsureUtc(booking.DepositDeadline).ToLocalTime(),
            BookingStatus = booking.BookingStatus,
            PaymentStatus = depositPayment?.PaymentStatus
        };
    }

    public async Task<string> ProcessAsync(
        long bookingId, long customerId, string outcome, CancellationToken ct = default)
    {
        if (outcome is not ("Success" or "Failed" or "Cancelled"))
            throw new ArgumentException("Kết quả mô phỏng không hợp lệ.");

        await using var db = CrmsContextFactory.Create();
        await using var tx = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);

        var booking = await db.Bookings
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customerId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy Booking thuộc tài khoản của bạn.");

        if (booking.DepositMethod != "VNPay")
            throw new InvalidOperationException("Booking này không sử dụng VNPay.");

        var depositPayment = booking.Payments
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefault(p => p.PaymentType == "Deposit");

        // Idempotent: nếu đã thanh toán thành công thì không ghi nhận lần hai.
        if (booking.BookingStatus == "Confirmed"
            && depositPayment?.PaymentStatus == "Success")
        {
            await tx.CommitAsync(ct);
            return "Booking này đã được thanh toán thành công trước đó.";
        }

        if (booking.BookingStatus != "PendingDeposit")
            throw new InvalidOperationException(
                $"Không thể thanh toán ở trạng thái Booking hiện tại: {booking.BookingStatus}.");
        if (booking.DepositAmount <= 0)
            throw new InvalidOperationException("Số tiền cọc phải lớn hơn 0 mới có thể mô phỏng thanh toán.");

        var now = DateTime.UtcNow;
        if (EnsureUtc(booking.DepositDeadline) <= now)
            throw new InvalidOperationException(
                "Đã quá hạn thanh toán tiền cọc. Booking không còn đủ điều kiện thanh toán.");

        var responseCode = outcome switch
        {
            "Success" => "00",
            "Failed" => "99",
            _ => "24"
        };

        var note = outcome switch
        {
            "Success" => "DEMO ONLY: VNPay simulation payment succeeded. No real money was transferred.",
            "Failed" => "DEMO ONLY: VNPay simulation payment failed. No real money was transferred.",
            _ => "DEMO ONLY: VNPay simulation transaction was cancelled. No real money was transferred."
        };

        if (depositPayment == null)
        {
            depositPayment = new Payment
            {
                BookingId = booking.BookingId,
                PaymentType = "Deposit",
                PaymentMethod = "VNPay",
                Amount = booking.DepositAmount,
                PaymentStatus = outcome,
                GatewayTxnRef = $"DEMO-{booking.BookingCode}",
                GatewayTransactionNo = $"SIM-{booking.BookingId}-{Guid.NewGuid():N}",
                GatewayResponseCode = responseCode,
                IdempotencyKey = Guid.NewGuid(),
                PaidAt = outcome == "Success" ? now : null,
                CreatedAt = now,
                Note = note
            };
            db.Payments.Add(depositPayment);
        }
        else
        {
            if (depositPayment.PaymentType != "Deposit" || depositPayment.PaymentMethod != "VNPay")
                throw new InvalidOperationException("Bản ghi Payment hiện tại không phải tiền cọc VNPay.");

            depositPayment.Amount = booking.DepositAmount;
            depositPayment.PaymentStatus = outcome;
            depositPayment.GatewayTxnRef ??= $"DEMO-{booking.BookingCode}";
            depositPayment.GatewayTransactionNo = $"SIM-{booking.BookingId}-{Guid.NewGuid():N}";
            depositPayment.GatewayResponseCode = responseCode;
            depositPayment.PaidAt = outcome == "Success" ? now : null;
            depositPayment.Note = note;
        }

        if (outcome == "Success")
        {
            booking.BookingStatus = "Confirmed";
            booking.ConfirmedAt = now;
            booking.UpdatedAt = now;

            var reservedVoucher = await db.BookingVouchers
                .FirstOrDefaultAsync(bv => bv.BookingId == booking.BookingId && bv.RedemptionStatus == "Reserved", ct);
            if (reservedVoucher != null)
            {
                reservedVoucher.RedemptionStatus = "Applied";
                reservedVoucher.AppliedAt = now;
            }

            db.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingId = booking.BookingId,
                OldStatus = "PendingDeposit",
                NewStatus = "Confirmed",
                ChangedByUserId = customerId,
                ChangedAt = now,
                ChangeNote = "VNPay simulation: deposit payment confirmed (demo only)."
            });
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return outcome switch
        {
            "Success" => "Thanh toán mô phỏng thành công! Booking đã chuyển sang trạng thái Confirmed.",
            "Failed" => "Giao dịch mô phỏng thất bại. Booking vẫn ở trạng thái PendingDeposit; bạn có thể thử lại trước hạn cọc.",
            _ => "Bạn đã hủy giao dịch mô phỏng. Booking vẫn ở trạng thái PendingDeposit; bạn có thể thanh toán lại trước hạn cọc."
        };
    }
}
