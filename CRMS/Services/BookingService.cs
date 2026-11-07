using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CRMS.Data;
using CRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Services;

public sealed class BookingListItem
{
    public long BookingId { get; init; }
    public string BookingCode { get; init; } = "";
    public string VehicleName { get; init; } = "";
    public string LicensePlate { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public DateTime StartAt { get; init; }
    public DateTime EndAt { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal DepositAmount { get; init; }
    public DateTime DepositDeadline { get; init; }
    public string BookingStatus { get; init; } = "";
    public string RentalType { get; init; } = "";
    public string DepositMethod { get; init; } = "";
    public string DepositMethodDisplay => DepositMethod == "Cash" ? "Tiền mặt" : "VNPay";
    public string? DriverName { get; init; }
    public string? PaymentStatus { get; init; }
    public bool CanPayDeposit => BookingStatus == "PendingDeposit" && DepositMethod == "VNPay";
    public bool CanConfirmCashDeposit => BookingStatus == "PendingDeposit" && DepositMethod == "Cash";
    public bool CanConfirmRefund => PaymentStatus == "Chờ hoàn cọc";
    public bool CanCancel => BookingStatus is "PendingDeposit" or "Confirmed";
}

public sealed class CreateBookingRequest
{
    public required long CustomerId { get; init; }
    public required long VehicleId { get; init; }
    public required string RentalType { get; init; } // SelfDrive / WithDriver
    public required string DepositMethod { get; init; } // Cash / VNPay
    public required DateTime StartAtUtc { get; init; }
    public required DateTime EndAtUtc { get; init; }
    public required Guid RequestKey { get; init; }
    public string? VoucherCode { get; init; }
    public int? LongTermMonths { get; init; }
}

public sealed class VoucherOption
{
    public string VoucherCode { get; init; } = "";
    public string DisplayText { get; init; } = "";
}

public sealed class VoucherPreview
{
    public string VoucherCode { get; init; } = "";
    public string DiscountType { get; init; } = "";
    public decimal DiscountValue { get; init; }
    public decimal GrossAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal DepositAmount { get; init; }
}

public sealed class PricingPolicyInfo
{
    public decimal DepositPercent { get; init; } = 50m;
    public decimal DriverDailyFee { get; init; } = 350000m;
    public int OnlineHoldMinutes { get; init; } = 30;
    public int CashHoldHours { get; init; } = 24;
}

public sealed class PaymentSummaryItem
{
    public long BookingId { get; init; }
    public string BookingCode { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string VehicleName { get; init; } = "";
    public string LicensePlate { get; init; } = "";
    public string RentalType { get; init; } = "";
    public DateTime StartAt { get; init; }
    public DateTime EndAt { get; init; }
    public decimal GrossAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal DepositAmount { get; init; }
    public decimal PaidDepositAmount { get; init; }
    public decimal RemainingAmount { get; init; }
    public string DepositMethod { get; init; } = "";
    public string DepositMethodDisplay => DepositMethod == "Cash" ? "Tiền mặt" : "VNPay";
    public string BookingStatus { get; init; } = "";
    public string PaymentStatus { get; init; } = "";
    public string GatewayTxnRef { get; init; } = "";
    public DateTime? LastPaymentAt { get; init; }
    public string Note { get; init; } = "";
    public bool CanConfirmCashDeposit => BookingStatus == "PendingDeposit" && DepositMethod == "Cash";
    public bool CanConfirmRefund => PaymentStatus == "Chờ hoàn cọc";
}

public sealed class PaymentSummaryReport
{
    public int TotalBookingsCount { get; init; }
    public decimal TotalValidBookingAmount { get; init; }
    public decimal TotalDepositCollected { get; init; }
    public decimal TotalDepositPending { get; init; }
    public decimal TotalRefundPending { get; init; }
    public decimal TotalRefundCompleted { get; init; }
    public List<PaymentSummaryItem> Items { get; init; } = new();
}

public sealed class BookingService
{
    private static DateTime ToLocal(DateTime dt) =>
        dt.Kind == DateTimeKind.Local
            ? dt
            : DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime();

    private static DateTime EnsureUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Local
            ? dt.ToUniversalTime()
            : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    public async Task<PricingPolicyInfo> GetPricingPolicyAsync(CancellationToken ct = default)
    {
        await using var db = CrmsContextFactory.Create();
        var policy = await db.RentalPolicies.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PolicyId == 1, ct);
        if (policy == null)
            return new PricingPolicyInfo();

        return new PricingPolicyInfo
        {
            DepositPercent = policy.DepositPercent,
            DriverDailyFee = policy.DriverDailyFee,
            OnlineHoldMinutes = policy.OnlineHoldMinutes,
            CashHoldHours = policy.CashHoldHours
        };
    }

    public async Task<List<VoucherOption>> GetAvailableVouchersAsync(
        long customerId, long vehicleId, string rentalType,
        DateTime startAtUtc, DateTime endAtUtc, int? longTermMonths = null, CancellationToken ct = default)
    {
        await using var db = CrmsContextFactory.Create();
        await ExpirePendingBookingsSafeAsync(db, ct);
        var gross = await GetGrossAmountAsync(db, vehicleId, rentalType, startAtUtc, endAtUtc, longTermMonths, ct);
        var now = DateTime.UtcNow;

        var vouchers = await db.Vouchers.AsNoTracking()
            .Where(v => v.IsActive && v.StartsAt <= now && v.EndsAt > now
                && v.MinimumOrderAmount <= gross)
            .OrderBy(v => v.EndsAt)
            .ToListAsync(ct);

        var usage = await GetVoucherUsageAsync(db, vouchers.Select(v => v.VoucherId).ToList(), customerId, ct);
        var result = new List<VoucherOption>();

        foreach (var voucher in vouchers)
        {
            usage.TryGetValue(voucher.VoucherId, out var counts);
            if ((voucher.UsageLimit.HasValue && counts.Global >= voucher.UsageLimit.Value)
                || counts.Customer >= voucher.PerCustomerLimit)
                continue;

            var discount = CalculateDiscount(voucher.DiscountType, voucher.DiscountValue,
                voucher.MaxDiscountAmount, gross);
            if (discount <= 0 || discount >= gross) continue;

            var description = voucher.DiscountType == "Percent"
                ? $"giảm {voucher.DiscountValue:0.##}%"
                : $"giảm {voucher.DiscountValue:N0} đ";
            if (voucher.MaxDiscountAmount.HasValue)
                description += $" (tối đa {voucher.MaxDiscountAmount.Value:N0} đ)";

            result.Add(new VoucherOption
            {
                VoucherCode = voucher.VoucherCode,
                DisplayText = $"{voucher.VoucherCode} — {description} — tiết kiệm {discount:N0} đ"
            });
        }

        return result;
    }

    public async Task<VoucherPreview> PreviewVoucherAsync(
        long customerId, long vehicleId, string rentalType,
        DateTime startAtUtc, DateTime endAtUtc, string voucherCode,
        int? longTermMonths = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(voucherCode))
            throw new ArgumentException("Vui lòng nhập hoặc chọn mã Voucher.");

        await using var db = CrmsContextFactory.Create();
        await ExpirePendingBookingsSafeAsync(db, ct);
        var gross = await GetGrossAmountAsync(db, vehicleId, rentalType, startAtUtc, endAtUtc, longTermMonths, ct);
        var now = DateTime.UtcNow;
        var code = voucherCode.Trim();

        var voucher = await db.Vouchers.AsNoTracking()
            .FirstOrDefaultAsync(v => v.VoucherCode == code, ct);

        if (voucher == null || !voucher.IsActive || voucher.StartsAt > now || voucher.EndsAt <= now)
            throw new InvalidOperationException("Voucher không tồn tại, chưa đến thời gian áp dụng hoặc đã hết hạn.");
        if (gross < voucher.MinimumOrderAmount)
            throw new InvalidOperationException($"Đơn hàng chưa đạt giá trị tối thiểu {voucher.MinimumOrderAmount:N0} đ.");

        var usage = await GetVoucherUsageAsync(db, new List<long> { voucher.VoucherId }, customerId, ct);
        usage.TryGetValue(voucher.VoucherId, out var counts);

        if ((voucher.UsageLimit.HasValue && counts.Global >= voucher.UsageLimit.Value)
            || counts.Customer >= voucher.PerCustomerLimit)
            throw new InvalidOperationException("Voucher đã hết lượt sử dụng hoặc bạn đã đạt giới hạn sử dụng.");

        var discount = CalculateDiscount(voucher.DiscountType, voucher.DiscountValue,
            voucher.MaxDiscountAmount, gross);
        if (discount <= 0 || discount >= gross)
            throw new InvalidOperationException("Voucher không hợp lệ vì số tiền giảm phải nhỏ hơn tổng tiền thuê.");

        var policy = await db.RentalPolicies.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PolicyId == 1, ct)
            ?? throw new InvalidOperationException("Không tìm thấy chính sách đặt cọc.");

        var total = gross - discount;
        var deposit = Math.Round(total * policy.DepositPercent / 100m, 2, MidpointRounding.AwayFromZero);

        return new VoucherPreview
        {
            VoucherCode = voucher.VoucherCode,
            DiscountType = voucher.DiscountType,
            DiscountValue = voucher.DiscountValue,
            GrossAmount = gross,
            DiscountAmount = discount,
            TotalAmount = total,
            DepositAmount = deposit
        };
    }

    private static async Task<decimal> GetGrossAmountAsync(
        CrmsContext db, long vehicleId, string rentalType,
        DateTime startAtUtc, DateTime endAtUtc, int? longTermMonths, CancellationToken ct)
    {
        if (startAtUtc.Kind != DateTimeKind.Utc || endAtUtc.Kind != DateTimeKind.Utc
            || startAtUtc <= DateTime.UtcNow || endAtUtc <= startAtUtc)
            throw new ArgumentException("Ngày giờ thuê không hợp lệ. Vui lòng chọn thời gian trong tương lai.");

        if (rentalType is not ("SelfDrive" or "WithDriver"))
            throw new ArgumentException("Hình thức thuê không hợp lệ.");

        var vehicle = await db.Vehicles.AsNoTracking()
            .FirstOrDefaultAsync(v => v.VehicleId == vehicleId
                && v.OperationalStatus == "Active" && v.Category.IsActive, ct)
            ?? throw new InvalidOperationException("Xe không tồn tại hoặc hiện không khả dụng.");

        var policy = await db.RentalPolicies.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PolicyId == 1, ct)
            ?? throw new InvalidOperationException("Không tìm thấy chính sách thuê xe.");

        var seconds = (endAtUtc - startAtUtc).TotalSeconds;
        var days = (int)Math.Ceiling(seconds / 86400d);
        if (days <= 0 || days > 370)
            throw new ArgumentException("Thời gian thuê phải từ 1 đến 370 ngày (tối đa gói 12 tháng).");

        decimal vehicleAmount = vehicle.DailyRate * days;
        if (longTermMonths.HasValue && longTermMonths.Value > 0)
        {
            byte monthsByte = (byte)Math.Clamp(longTermMonths.Value, 1, 24);
            var ltRate = await db.VehicleLongTermRates.AsNoTracking()
                .FirstOrDefaultAsync(r => r.VehicleId == vehicleId && r.DurationMonths == monthsByte, ct);
            if (ltRate != null)
            {
                vehicleAmount = ltRate.MonthlyRate * monthsByte;
            }
        }

        var driverRate = rentalType == "WithDriver" ? policy.DriverDailyFee : 0m;
        return vehicleAmount + (driverRate * days);
    }

    private static async Task<Dictionary<long, (int Global, int Customer)>> GetVoucherUsageAsync(
        CrmsContext db, List<long> voucherIds, long customerId, CancellationToken ct)
    {
        if (voucherIds.Count == 0)
            return new Dictionary<long, (int Global, int Customer)>();

        var rows = await db.BookingVouchers.AsNoTracking()
            .Where(bv => voucherIds.Contains(bv.VoucherId)
                && (bv.RedemptionStatus == "Reserved" || bv.RedemptionStatus == "Applied"))
            .Select(bv => new { bv.VoucherId, bv.Booking.CustomerId })
            .ToListAsync(ct);

        return rows.GroupBy(x => x.VoucherId).ToDictionary(
            g => g.Key,
            g => (g.Count(), g.Count(x => x.CustomerId == customerId)));
    }

    private static decimal CalculateDiscount(
        string discountType, decimal discountValue, decimal? maxDiscountAmount, decimal gross)
    {
        var discount = discountType == "Percent"
            ? Math.Round(gross * discountValue / 100m, 2, MidpointRounding.AwayFromZero)
            : discountValue;

        if (maxDiscountAmount.HasValue && discount > maxDiscountAmount.Value)
            discount = maxDiscountAmount.Value;

        return discount;
    }

    /// <summary>
    /// Dọn các Booking quá hạn đặt cọc trực tiếp qua EF Core (gán đầy đủ ChangedAt)
    /// để không bị phụ thuộc vào DEFAULT constraint của SQL Server.
    /// </summary>
    private static async Task ExpirePendingBookingsSafeAsync(CrmsContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var expiredBookings = await db.Bookings
            .Include(b => b.BookingVoucher)
            .Where(b => b.BookingStatus == "PendingDeposit" && b.DepositDeadline <= now)
            .ToListAsync(ct);

        if (expiredBookings.Count == 0)
            return;

        foreach (var b in expiredBookings)
        {
            b.BookingStatus = "Expired";
            b.UpdatedAt = now;

            if (b.BookingVoucher != null && b.BookingVoucher.RedemptionStatus == "Reserved")
            {
                b.BookingVoucher.RedemptionStatus = "Released";
            }

            db.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingId = b.BookingId,
                OldStatus = "PendingDeposit",
                NewStatus = "Expired",
                ChangedByUserId = null,
                ChangedAt = now,
                ChangeNote = "Deposit deadline elapsed"
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> SupportsBookingVoucherAsync(CancellationToken ct = default)
    {
        await using var db = CrmsContextFactory.Create();
        var result = await db.Database.SqlQueryRaw<int>(
            "SELECT CASE WHEN OBJECT_ID(N'dbo.Vouchers', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.BookingVouchers', N'U') IS NOT NULL THEN 1 ELSE 0 END AS [Value]")
            .SingleAsync(ct);
        return result == 1;
    }

    public async Task<long> CreateAsync(CreateBookingRequest request, CancellationToken ct = default)
    {
        if (request.CustomerId <= 0 || request.VehicleId <= 0)
            throw new ArgumentException("Vui lòng đăng nhập và chọn xe hợp lệ.");
        if (request.StartAtUtc.Kind != DateTimeKind.Utc || request.EndAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Thời gian đặt xe phải được chuyển sang UTC trước khi lưu.");
        if (request.StartAtUtc <= DateTime.UtcNow || request.EndAtUtc <= request.StartAtUtc)
            throw new ArgumentException("Ngày nhận/trả xe không hợp lệ (phải ở trong tương lai).");
        if (request.RentalType is not ("SelfDrive" or "WithDriver"))
            throw new ArgumentException("Hình thức thuê không hợp lệ.");
        if (request.DepositMethod is not ("Cash" or "VNPay"))
            throw new ArgumentException("Phương thức cọc không hợp lệ.");
        if (request.RequestKey == Guid.Empty)
            throw new ArgumentException("RequestKey không được rỗng.");

        var voucherCode = string.IsNullOrWhiteSpace(request.VoucherCode)
            ? null : request.VoucherCode.Trim();

        await using var db = CrmsContextFactory.Create();
        await ExpirePendingBookingsSafeAsync(db, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);

        try
        {
            // Chống double-click/retry: cùng RequestKey chỉ tạo một Booking.
            var existing = await db.Bookings.AsNoTracking()
                .FirstOrDefaultAsync(b => b.RequestKey == request.RequestKey, ct);
            if (existing != null)
            {
                if (existing.CustomerId != request.CustomerId
                    || existing.VehicleId != request.VehicleId
                    || existing.StartAt != request.StartAtUtc
                    || existing.EndAt != request.EndAtUtc
                    || existing.RentalType != request.RentalType
                    || existing.DepositMethod != request.DepositMethod)
                    throw new InvalidOperationException(
                        "RequestKey đã được dùng cho một yêu cầu Booking khác. Vui lòng tải lại màn hình.");

                await transaction.CommitAsync(ct);
                return existing.BookingId;
            }

            // Khóa theo xe, tránh hai yêu cầu đồng thời cùng đặt một xe.
            var lockResource = $"CRMS_VEHICLE_{request.VehicleId}";
            await db.Database.ExecuteSqlInterpolatedAsync($@"
                DECLARE @LockResult INT;
                EXEC @LockResult = sys.sp_getapplock
                    @Resource = {lockResource},
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = 10000;
                IF @LockResult < 0
                    THROW 51003, 'Could not acquire vehicle reservation lock.', 1;", ct);

            var now = DateTime.UtcNow;

            var customerIsActive = await db.Users.AsNoTracking().AnyAsync(
                u => u.UserId == request.CustomerId
                     && u.UserRole == "Customer" && u.IsActive, ct);
            if (!customerIsActive)
                throw new InvalidOperationException("Tài khoản Customer không tồn tại hoặc đã bị khóa.");

            var vehicle = await db.Vehicles.AsNoTracking()
                .FirstOrDefaultAsync(v => v.VehicleId == request.VehicleId
                    && v.OperationalStatus == "Active" && v.Category.IsActive, ct);
            if (vehicle == null)
                throw new InvalidOperationException("Xe không tồn tại, đã ngừng hoạt động hoặc không khả dụng.");

            var hasBookingConflict = await db.Bookings.AsNoTracking().AnyAsync(b =>
                b.VehicleId == request.VehicleId
                && (
                    (b.BookingStatus == "InUse" && b.StartAt < request.EndAtUtc)
                    || (b.BookingStatus == "Confirmed"
                        && b.StartAt < request.EndAtUtc && b.EndAt > request.StartAtUtc)
                    || (b.BookingStatus == "PendingDeposit"
                        && b.DepositDeadline > now
                        && b.StartAt < request.EndAtUtc && b.EndAt > request.StartAtUtc)
                ), ct);
            if (hasBookingConflict)
                throw new InvalidOperationException("Xe đã có Booking trong khoảng thời gian bạn chọn. Vui lòng chọn ngày khác.");

            var hasMaintenanceConflict = await db.MaintenanceRecords.AsNoTracking().AnyAsync(m =>
                m.VehicleId == request.VehicleId
                && m.StartAt < request.EndAtUtc && m.EndAt > request.StartAtUtc
                && (m.MaintenanceStatus == "Scheduled" || m.MaintenanceStatus == "InProgress"), ct);
            if (hasMaintenanceConflict)
                throw new InvalidOperationException("Xe đang được bảo dưỡng trong khoảng thời gian bạn chọn.");

            var policy = await db.RentalPolicies.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PolicyId == 1, ct);
            if (policy == null)
                throw new InvalidOperationException("Không tìm thấy chính sách thuê xe (RentalPolicy).");

            var rentalDays = (int)Math.Ceiling((request.EndAtUtc - request.StartAtUtc).TotalSeconds / 86400d);
            if (rentalDays <= 0 || rentalDays > 370)
                throw new ArgumentException("Thời gian thuê phải từ 1 đến 370 ngày (tối đa gói 12 tháng).");

            var vehicleDailyRate = vehicle.DailyRate;
            var driverDailyRate = request.RentalType == "WithDriver" ? policy.DriverDailyFee : 0m;
            var vehicleAmount = vehicleDailyRate * rentalDays;

            if (request.LongTermMonths.HasValue && request.LongTermMonths.Value > 0)
            {
                byte monthsByte = (byte)Math.Clamp(request.LongTermMonths.Value, 1, 24);
                var ltRate = await db.VehicleLongTermRates.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.VehicleId == request.VehicleId && r.DurationMonths == monthsByte, ct);
                if (ltRate != null)
                {
                    vehicleAmount = ltRate.MonthlyRate * monthsByte;
                }
            }

            var driverAmount = driverDailyRate * rentalDays;
            var grossAmount = vehicleAmount + driverAmount;

            // Kiểm tra và tính Voucher trực tiếp trong cùng Serializable transaction
            // (gán tường minh RedemptionStatus = "Reserved" và CreatedAt = now để không lỗi NULL khi DB thiếu DEFAULT constraint).
            Voucher? appliedVoucher = null;
            decimal discountAmount = 0m;

            if (voucherCode != null)
            {
                appliedVoucher = await db.Vouchers
                    .FirstOrDefaultAsync(v => v.VoucherCode == voucherCode && v.IsActive
                        && v.StartsAt <= now && v.EndsAt > now, ct)
                    ?? throw new InvalidOperationException("Voucher không tồn tại, chưa đến thời gian áp dụng hoặc đã hết hạn.");

                if (grossAmount < appliedVoucher.MinimumOrderAmount)
                    throw new InvalidOperationException($"Đơn hàng chưa đạt giá trị tối thiểu {appliedVoucher.MinimumOrderAmount:N0} đ để dùng mã {voucherCode}.");

                var usage = await GetVoucherUsageAsync(db, new List<long> { appliedVoucher.VoucherId }, request.CustomerId, ct);
                usage.TryGetValue(appliedVoucher.VoucherId, out var counts);

                if ((appliedVoucher.UsageLimit.HasValue && counts.Global >= appliedVoucher.UsageLimit.Value)
                    || counts.Customer >= appliedVoucher.PerCustomerLimit)
                    throw new InvalidOperationException("Voucher đã hết lượt sử dụng hoặc bạn đã đạt giới hạn sử dụng.");

                discountAmount = CalculateDiscount(
                    appliedVoucher.DiscountType,
                    appliedVoucher.DiscountValue,
                    appliedVoucher.MaxDiscountAmount,
                    grossAmount);

                if (discountAmount <= 0 || discountAmount >= grossAmount)
                    throw new InvalidOperationException("Số tiền giảm của Voucher không hợp lệ so với giá trị đơn hàng.");
            }

            var totalAmount = grossAmount - discountAmount;
            var depositAmount = Math.Round(
                totalAmount * policy.DepositPercent / 100m, 2, MidpointRounding.AwayFromZero);
            var depositDeadline = request.DepositMethod == "VNPay"
                ? now.AddMinutes(policy.OnlineHoldMinutes)
                : now.AddHours(policy.CashHoldHours);

            var booking = new Booking
            {
                BookingCode = $"CR-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..23],
                RequestKey = request.RequestKey,
                CustomerId = request.CustomerId,
                VehicleId = request.VehicleId,
                DriverId = null,
                RentalType = request.RentalType,
                DepositMethod = request.DepositMethod,
                StartAt = request.StartAtUtc,
                EndAt = request.EndAtUtc,
                RentalDays = rentalDays,
                VehicleDailyRate = vehicleDailyRate,
                DriverDailyRate = driverDailyRate,
                VehicleAmount = vehicleAmount,
                DriverAmount = driverAmount,
                DiscountAmount = discountAmount,
                TotalAmount = totalAmount,
                DepositAmount = depositAmount,
                DepositDeadline = depositDeadline,
                BookingStatus = "PendingDeposit",
                CreatedAt = now
            };

            db.Bookings.Add(booking);
            await db.SaveChangesAsync(ct);

            db.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingId = booking.BookingId,
                OldStatus = null,
                NewStatus = "PendingDeposit",
                ChangedByUserId = request.CustomerId,
                ChangedAt = now,
                ChangeNote = request.LongTermMonths.HasValue
                    ? $"Reservation created (Long-term {request.LongTermMonths.Value} months)"
                    : "Reservation created"
            });

            if (appliedVoucher != null)
            {
                db.BookingVouchers.Add(new BookingVoucher
                {
                    BookingId = booking.BookingId,
                    VoucherId = appliedVoucher.VoucherId,
                    DiscountSnapshot = discountAmount,
                    RedemptionStatus = "Reserved",
                    CreatedAt = now,
                    AppliedAt = null
                });
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return booking.BookingId;
        }
        catch
        {
            try { await transaction.RollbackAsync(ct); }
            catch { }
            throw;
        }
    }

    public async Task<PaymentSummaryReport> GetPaymentSummaryAsync(CancellationToken ct = default)
    {
        await using var db = CrmsContextFactory.Create();
        await ExpirePendingBookingsSafeAsync(db, ct);

        var bookings = await Query(db)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);

        var items = new List<PaymentSummaryItem>(bookings.Count);
        decimal totalValidBookingAmount = 0m;
        decimal totalDepositCollected = 0m;
        decimal totalDepositPending = 0m;
        decimal totalRefundPending = 0m;
        decimal totalRefundCompleted = 0m;

        foreach (var b in bookings)
        {
            var deposit = b.Payments
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault(p => p.PaymentType == "Deposit");
            var refund = b.Payments
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault(p => p.PaymentType == "Refund");

            bool depositPaid = deposit?.PaymentStatus == "Success";
            decimal paidDeposit = depositPaid ? deposit!.Amount : 0m;

            if (b.BookingStatus is not ("Cancelled" or "Expired"))
            {
                totalValidBookingAmount += b.TotalAmount;
            }

            if (depositPaid)
            {
                totalDepositCollected += deposit!.Amount;
            }
            else if (b.BookingStatus == "PendingDeposit")
            {
                totalDepositPending += b.DepositAmount;
            }

            if (refund != null)
            {
                if (refund.PaymentStatus == "Pending")
                    totalRefundPending += refund.Amount;
                else if (refund.PaymentStatus == "Success")
                    totalRefundCompleted += refund.Amount;
            }

            string paymentStatus;
            if (refund != null)
            {
                paymentStatus = refund.PaymentStatus == "Pending"
                    ? "Chờ hoàn cọc"
                    : refund.PaymentStatus == "Success"
                        ? "Đã hoàn cọc"
                        : $"Hoàn tiền: {refund.PaymentStatus}";
            }
            else if (deposit != null)
            {
                paymentStatus = deposit.PaymentStatus == "Success"
                    ? "Đã thu cọc"
                    : deposit.PaymentStatus == "Pending"
                        ? "Chờ thanh toán"
                        : deposit.PaymentStatus;
            }
            else
            {
                paymentStatus = b.BookingStatus == "PendingDeposit"
                    ? "Chưa thanh toán"
                    : "Không phát sinh";
            }

            decimal remaining = 0m;
            if (b.BookingStatus is not ("Cancelled" or "Expired"))
            {
                remaining = Math.Max(0m, b.TotalAmount - paidDeposit);
            }

            var latestPayment = b.Payments
                .OrderByDescending(p => p.PaidAt ?? p.CreatedAt)
                .FirstOrDefault();

            DateTime? lastTime = latestPayment != null
                ? ToLocal(latestPayment.PaidAt ?? latestPayment.CreatedAt)
                : null;

            items.Add(new PaymentSummaryItem
            {
                BookingId = b.BookingId,
                BookingCode = b.BookingCode,
                CustomerName = b.Customer.FullName,
                VehicleName = b.Vehicle.Brand + " " + b.Vehicle.Model,
                LicensePlate = b.Vehicle.LicensePlate,
                RentalType = b.RentalType == "WithDriver" ? "Có tài xế" : "Tự lái",
                StartAt = ToLocal(b.StartAt),
                EndAt = ToLocal(b.EndAt),
                GrossAmount = b.VehicleAmount + b.DriverAmount,
                DiscountAmount = b.DiscountAmount,
                TotalAmount = b.TotalAmount,
                DepositAmount = b.DepositAmount,
                PaidDepositAmount = paidDeposit,
                RemainingAmount = remaining,
                DepositMethod = b.DepositMethod,
                BookingStatus = b.BookingStatus,
                PaymentStatus = paymentStatus,
                GatewayTxnRef = deposit?.GatewayTxnRef ?? (b.DepositMethod == "Cash" ? "Tại quầy (Cash)" : "—"),
                LastPaymentAt = lastTime,
                Note = refund?.Note ?? deposit?.Note ?? ""
            });
        }

        return new PaymentSummaryReport
        {
            TotalBookingsCount = bookings.Count,
            TotalValidBookingAmount = totalValidBookingAmount,
            TotalDepositCollected = totalDepositCollected,
            TotalDepositPending = totalDepositPending,
            TotalRefundPending = totalRefundPending,
            TotalRefundCompleted = totalRefundCompleted,
            Items = items
        };
    }

    public async Task<List<BookingListItem>> GetForCustomerAsync(long customerId, CancellationToken ct = default)
    {
        await using var db = CrmsContextFactory.Create();
        await ExpirePendingBookingsSafeAsync(db, ct);
        var bookings = await Query(db)
            .Where(b => b.CustomerId == customerId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);
        return bookings.Select(MapToListItem).ToList();
    }

    public async Task<List<BookingListItem>> GetForStaffAsync(string? status = null, CancellationToken ct = default)
    {
        await using var db = CrmsContextFactory.Create();
        await ExpirePendingBookingsSafeAsync(db, ct);
        var query = Query(db);
        if (!string.IsNullOrWhiteSpace(status) && status != "Tất cả")
            query = query.Where(b => b.BookingStatus == status);
        var bookings = await query.OrderByDescending(b => b.CreatedAt).ToListAsync(ct);
        return bookings.Select(MapToListItem).ToList();
    }

    /// <summary>
    /// Staff/Admin xác nhận đã thu cọc tiền mặt (Cash) cho Booking đang ở trạng thái PendingDeposit.
    /// Gán đầy đủ các cột NOT NULL qua EF Core để không phụ thuộc DEFAULT constraint của SQL Server.
    /// </summary>
    public async Task ConfirmCashDepositAsync(
        long bookingId, long staffUserId, CancellationToken ct = default)
    {
        if (bookingId <= 0 || staffUserId <= 0)
            throw new ArgumentException("Thông tin Booking hoặc nhân viên xác nhận không hợp lệ.");

        await using var db = CrmsContextFactory.Create();
        await ExpirePendingBookingsSafeAsync(db, ct);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var actorIsStaff = await db.Users.AsNoTracking().AnyAsync(u =>
                u.UserId == staffUserId && u.IsActive
                && (u.UserRole == "Staff" || u.UserRole == "Admin"), ct);
            if (!actorIsStaff)
                throw new InvalidOperationException("Chỉ Staff/Admin đang hoạt động mới được xác nhận tiền cọc.");

            var booking = await db.Bookings
                .Include(b => b.Payments)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId, ct)
                ?? throw new InvalidOperationException("Không tìm thấy Booking.");

            if (booking.DepositMethod != "Cash")
                throw new InvalidOperationException("Chỉ đơn đặt cọc bằng Tiền mặt (Cash) mới xác nhận tại quầy.");

            var depositPayment = booking.Payments
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault(p => p.PaymentType == "Deposit");

            if (booking.BookingStatus == "Confirmed" && depositPayment?.PaymentStatus == "Success")
            {
                await tx.CommitAsync(ct);
                return;
            }

            if (booking.BookingStatus != "PendingDeposit")
                throw new InvalidOperationException(
                    $"Chỉ đơn đang ở trạng thái PendingDeposit mới được xác nhận cọc (trạng thái hiện tại: {booking.BookingStatus}).");

            var now = DateTime.UtcNow;
            if (EnsureUtc(booking.DepositDeadline) <= now)
                throw new InvalidOperationException("Đơn đặt xe này đã quá hạn nộp tiền cọc.");

            if (depositPayment == null)
            {
                depositPayment = new Payment
                {
                    BookingId = booking.BookingId,
                    PaymentType = "Deposit",
                    PaymentMethod = "Cash",
                    Amount = booking.DepositAmount,
                    PaymentStatus = "Success",
                    IdempotencyKey = Guid.NewGuid(),
                    VerifiedByUserId = staffUserId,
                    PaidAt = now,
                    CreatedAt = now,
                    Note = "Cash deposit confirmed by Staff/Admin."
                };
                db.Payments.Add(depositPayment);
            }
            else
            {
                depositPayment.Amount = booking.DepositAmount;
                depositPayment.PaymentStatus = "Success";
                depositPayment.VerifiedByUserId = staffUserId;
                depositPayment.PaidAt = now;
                depositPayment.Note = "Cash deposit confirmed by Staff/Admin.";
            }

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
                ChangedByUserId = staffUserId,
                ChangedAt = now,
                ChangeNote = "Cash deposit confirmed"
            });

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            try { await tx.RollbackAsync(ct); } catch { }
            throw;
        }
    }

    public async Task<decimal> ApplyVoucherAsync(long bookingId, long customerId, string voucherCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(voucherCode))
            throw new ArgumentException("Vui lòng nhập mã voucher.");

        var code = voucherCode.Trim();
        await using var db = CrmsContextFactory.Create();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var now = DateTime.UtcNow;
            var booking = await db.Bookings
                .Include(b => b.Payments)
                .Include(b => b.BookingVoucher)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customerId, ct)
                ?? throw new InvalidOperationException("Không tìm thấy Booking hợp lệ.");

            if (booking.BookingStatus != "PendingDeposit" || EnsureUtc(booking.DepositDeadline) <= now)
                throw new InvalidOperationException("Booking không còn trong thời hạn áp dụng Voucher.");
            if (booking.Payments.Any())
                throw new InvalidOperationException("Không thể áp dụng Voucher sau khi đã phát sinh giao dịch thanh toán.");
            if (booking.BookingVoucher != null)
                throw new InvalidOperationException("Booking này đã được áp dụng một mã Voucher.");

            var gross = booking.VehicleAmount + booking.DriverAmount;
            var voucher = await db.Vouchers
                .FirstOrDefaultAsync(v => v.VoucherCode == code && v.IsActive && v.StartsAt <= now && v.EndsAt > now, ct)
                ?? throw new InvalidOperationException("Voucher không hợp lệ hoặc đã hết hạn.");

            if (gross < voucher.MinimumOrderAmount)
                throw new InvalidOperationException($"Đơn hàng chưa đạt mức tối thiểu {voucher.MinimumOrderAmount:N0} đ.");

            var usage = await GetVoucherUsageAsync(db, new List<long> { voucher.VoucherId }, customerId, ct);
            usage.TryGetValue(voucher.VoucherId, out var counts);
            if ((voucher.UsageLimit.HasValue && counts.Global >= voucher.UsageLimit.Value)
                || counts.Customer >= voucher.PerCustomerLimit)
                throw new InvalidOperationException("Voucher đã hết lượt sử dụng.");

            var discount = CalculateDiscount(voucher.DiscountType, voucher.DiscountValue, voucher.MaxDiscountAmount, gross);
            if (discount <= 0 || discount >= gross)
                throw new InvalidOperationException("Số tiền giảm của Voucher không hợp lệ.");

            var policy = await db.RentalPolicies.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PolicyId == 1, ct)
                ?? throw new InvalidOperationException("Không tìm thấy chính sách đặt cọc.");

            booking.DiscountAmount = discount;
            booking.TotalAmount = gross - discount;
            booking.DepositAmount = Math.Round(booking.TotalAmount * policy.DepositPercent / 100m, 2, MidpointRounding.AwayFromZero);
            booking.UpdatedAt = now;

            db.BookingVouchers.Add(new BookingVoucher
            {
                BookingId = booking.BookingId,
                VoucherId = voucher.VoucherId,
                DiscountSnapshot = discount,
                RedemptionStatus = "Reserved",
                CreatedAt = now,
                AppliedAt = null
            });

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return discount;
        }
        catch
        {
            try { await tx.RollbackAsync(ct); } catch { }
            throw;
        }
    }

    public async Task<List<Driver>> GetActiveDriversAsync(CancellationToken ct = default)
    {
        await using var db = CrmsContextFactory.Create();
        return await db.Drivers.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.FullName)
            .ToListAsync(ct);
    }

    public async Task AssignDriverAsync(
        long bookingId, long driverId, long staffUserId, CancellationToken ct = default)
    {
        if (bookingId <= 0 || driverId <= 0 || staffUserId <= 0)
            throw new ArgumentException("Vui lòng chọn Booking, tài xế và tài khoản Staff/Admin hợp lệ.");

        await using var db = CrmsContextFactory.Create();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var actorIsStaff = await db.Users.AsNoTracking().AnyAsync(u =>
                u.UserId == staffUserId && u.IsActive
                && (u.UserRole == "Staff" || u.UserRole == "Admin"), ct);
            if (!actorIsStaff)
                throw new InvalidOperationException("Chỉ Staff/Admin đang hoạt động mới được phân công tài xế.");

            var booking = await db.Bookings.FirstOrDefaultAsync(b => b.BookingId == bookingId, ct)
                ?? throw new InvalidOperationException("Không tìm thấy Booking.");

            if (booking.RentalType != "WithDriver" || booking.BookingStatus != "Confirmed")
                throw new InvalidOperationException(
                    "Chỉ Booking Có tài xế đã thanh toán cọc và ở trạng thái Confirmed mới được phân công.");

            var driver = await db.Drivers.FirstOrDefaultAsync(d => d.DriverId == driverId && d.IsActive, ct)
                ?? throw new InvalidOperationException("Tài xế không tồn tại hoặc đã ngừng hoạt động.");

            var lockResource = $"CRMS_DRIVER_{driverId}";
            await db.Database.ExecuteSqlInterpolatedAsync($@"
                DECLARE @LockResult INT;
                EXEC @LockResult = sys.sp_getapplock
                    @Resource = {lockResource},
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = 10000;
                IF @LockResult < 0
                    THROW 51004, 'Could not acquire driver assignment lock.', 1;", ct);

            var conflict = await db.Bookings.AsNoTracking().AnyAsync(b =>
                b.BookingId != bookingId
                && b.DriverId == driverId
                && ((b.BookingStatus == "Confirmed"
                     && b.StartAt < booking.EndAt && b.EndAt > booking.StartAt)
                    || (b.BookingStatus == "InUse" && b.StartAt < booking.EndAt)), ct);

            if (conflict)
                throw new InvalidOperationException("Tài xế đã được phân công cho Booking khác trong khoảng thời gian này.");

            booking.DriverId = driver.DriverId;
            booking.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            try { await tx.RollbackAsync(ct); } catch { }
            throw;
        }
    }

    /// <summary>
    /// Staff/Admin xác nhận đã hoàn trả tiền cọc cho đơn đã hủy (chuyển Refund từ Pending sang Success).
    /// </summary>
    public async Task ConfirmRefundAsync(
        long bookingId, long staffUserId, CancellationToken ct = default)
    {
        if (bookingId <= 0 || staffUserId <= 0)
            throw new ArgumentException("Thông tin Booking hoặc nhân viên xác nhận không hợp lệ.");

        await using var db = CrmsContextFactory.Create();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var actorIsStaff = await db.Users.AsNoTracking().AnyAsync(u =>
                u.UserId == staffUserId && u.IsActive
                && (u.UserRole == "Staff" || u.UserRole == "Admin"), ct);
            if (!actorIsStaff)
                throw new InvalidOperationException("Chỉ Staff/Admin đang hoạt động mới được xác nhận hoàn tiền cọc.");

            var refundPayment = await db.Payments
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync(p => p.BookingId == bookingId && p.PaymentType == "Refund", ct)
                ?? throw new InvalidOperationException("Đơn này không có khoản hoàn cọc nào đang chờ xử lý.");

            if (refundPayment.PaymentStatus == "Success")
            {
                await tx.CommitAsync(ct);
                return;
            }

            if (refundPayment.PaymentStatus != "Pending")
                throw new InvalidOperationException($"Khoản hoàn cọc đang ở trạng thái {refundPayment.PaymentStatus}, không thể xác nhận.");

            var now = DateTime.UtcNow;
            refundPayment.PaymentStatus = "Success";
            refundPayment.PaidAt = now;
            refundPayment.VerifiedByUserId = staffUserId;
            refundPayment.Note = "Manual deposit refund confirmed by Staff/Admin.";

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            try { await tx.RollbackAsync(ct); } catch { }
            throw;
        }
    }

    public async Task CancelAsync(long bookingId, long actorUserId, string? reason, CancellationToken ct = default)
    {
        if (bookingId <= 0 || actorUserId <= 0)
            throw new ArgumentException("Thông tin hủy đơn không hợp lệ.");

        await using var db = CrmsContextFactory.Create();
        await ExpirePendingBookingsSafeAsync(db, ct);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var booking = await db.Bookings
                .Include(b => b.Payments)
                .Include(b => b.BookingVoucher)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId, ct)
                ?? throw new InvalidOperationException("Không tìm thấy Booking cần hủy.");

            var authorized = await db.Users.AsNoTracking().AnyAsync(u =>
                u.UserId == actorUserId && u.IsActive
                && (u.UserId == booking.CustomerId || u.UserRole == "Staff" || u.UserRole == "Admin"), ct);
            if (!authorized)
                throw new InvalidOperationException("Bạn không có quyền hủy Booking này.");

            if (booking.BookingStatus == "Cancelled")
            {
                await tx.CommitAsync(ct);
                return;
            }

            if (booking.BookingStatus is not ("PendingDeposit" or "Confirmed"))
                throw new InvalidOperationException("Chỉ được hủy các đơn đang ở trạng thái PendingDeposit hoặc Confirmed.");

            // Đơn chỉ được phép hủy khi đang ở trạng thái PendingDeposit hoặc Confirmed (chưa sang InUse).
            // Không truy vấn bảng VehicleHandovers để tránh lỗi lệch cột FuelPercentOut trên các bản CSDL cũ.

            var now = DateTime.UtcNow;
            var oldStatus = booking.BookingStatus;

            booking.BookingStatus = "Cancelled";
            booking.CancelledAt = now;
            booking.CancelReason = reason;
            booking.UpdatedAt = now;

            if (booking.BookingVoucher != null &&
                (booking.BookingVoucher.RedemptionStatus == "Reserved" || booking.BookingVoucher.RedemptionStatus == "Applied"))
            {
                booking.BookingVoucher.RedemptionStatus = "Released";
            }

            db.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingId = booking.BookingId,
                OldStatus = oldStatus,
                NewStatus = "Cancelled",
                ChangedByUserId = actorUserId,
                ChangedAt = now,
                ChangeNote = reason ?? "Booking cancelled"
            });

            var paidAmount = booking.Payments
                .Where(p => p.PaymentStatus == "Success")
                .Sum(p => p.PaymentType == "Refund" ? -p.Amount : p.Amount);

            var hasPendingRefund = booking.Payments
                .Any(p => p.PaymentType == "Refund" && p.PaymentStatus == "Pending");

            if (paidAmount > 0 && !hasPendingRefund)
            {
                db.Payments.Add(new Payment
                {
                    BookingId = booking.BookingId,
                    PaymentType = "Refund",
                    PaymentMethod = "BankTransfer",
                    Amount = paidAmount,
                    PaymentStatus = "Pending",
                    IdempotencyKey = Guid.NewGuid(),
                    CreatedAt = now,
                    Note = "Manual refund pending after cancellation"
                });
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            try { await tx.RollbackAsync(ct); } catch { }
            throw;
        }
    }

    private static IQueryable<Booking> Query(CrmsContext db) => db.Bookings.AsNoTracking()
        .Include(b => b.Vehicle)
        .Include(b => b.Customer)
        .Include(b => b.Driver)
        .Include(b => b.Payments);

    private static BookingListItem MapToListItem(Booking b)
    {
        var refund = b.Payments
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefault(p => p.PaymentType == "Refund");
        var deposit = b.Payments
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefault(p => p.PaymentType == "Deposit");

        string? paymentStatus;
        if (refund != null)
        {
            paymentStatus = refund.PaymentStatus == "Pending"
                ? "Chờ hoàn cọc"
                : refund.PaymentStatus == "Success"
                    ? "Đã hoàn cọc"
                    : $"Hoàn tiền: {refund.PaymentStatus}";
        }
        else if (deposit != null)
        {
            paymentStatus = deposit.PaymentStatus;
        }
        else
        {
            paymentStatus = b.BookingStatus == "PendingDeposit" ? "Chưa thanh toán" : null;
        }

        return new BookingListItem
        {
            BookingId = b.BookingId,
            BookingCode = b.BookingCode,
            VehicleName = b.Vehicle.Brand + " " + b.Vehicle.Model,
            LicensePlate = b.Vehicle.LicensePlate,
            CustomerName = b.Customer.FullName,
            StartAt = ToLocal(b.StartAt),
            EndAt = ToLocal(b.EndAt),
            TotalAmount = b.TotalAmount,
            DepositAmount = b.DepositAmount,
            DepositDeadline = ToLocal(b.DepositDeadline),
            BookingStatus = b.BookingStatus,
            RentalType = b.RentalType == "WithDriver" ? "Có tài xế" : "Tự lái",
            DepositMethod = b.DepositMethod,
            DriverName = b.Driver != null ? b.Driver.FullName
                : b.RentalType == "SelfDrive" ? "Tự lái" : "Đang chờ phân công",
            PaymentStatus = paymentStatus
        };
    }
}