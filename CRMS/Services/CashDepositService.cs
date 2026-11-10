using CRMS.Data;
using Microsoft.EntityFrameworkCore;
using CRMS.Services.Payments;
using CRMS.Services.Vehicles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRMS.Services
{
    public class CashDepositService : ICashDepositService
    {
        private readonly Func<CrmsContext> _create;
        public CashDepositService() : this(CrmsContextFactory.Create) { }
        public CashDepositService(Func<CrmsContext> create) => _create = create;
        public Task<List<CashDepositItem>> GetPendingDepositsAsync() => GetDepositsAsync(false);

        public async Task<List<CashDepositItem>> GetDepositsAsync(bool includeUnavailable)
        {
            using var db = _create();

            var userId = CurrentUserService.Instance.UserId;

            if (!userId.HasValue)
                throw new InvalidOperationException(
                    "Vui lòng đăng nhập bằng tài khoản Staff hoặc Admin.");

            bool hasPermission = await db.Users
                .AsNoTracking()
                .AnyAsync(u =>
                    u.UserId == userId.Value &&
                    u.IsActive &&
                    (u.UserRole == "Staff" || u.UserRole == "Admin"));

            if (!hasPermission)
                throw new InvalidOperationException(
                    "Bạn không có quyền xem danh sách thu cọc.");

            var items = await db.Bookings
                .AsNoTracking()
                .Where(b =>
                    (!b.IsDemo && (b.BookingStatus == "PendingDeposit" || (includeUnavailable && b.BookingStatus == "Expired")) &&
                    (includeUnavailable || (b.DepositMethod == "Cash" && b.DepositDeadline > DateTime.UtcNow)) &&
                    !db.Payments.Any(p =>
                        p.BookingId == b.BookingId &&
                        p.PaymentType == "Deposit" &&
                        p.PaymentStatus == "Success")) ||
                    (b.IsDemo && (b.BookingStatus == "PendingDeposit" || b.BookingStatus == "Cancelled" || b.BookingStatus == "Expired") &&
                        db.DemoPayments.Any(p => p.BookingId == b.BookingId && p.PaymentStatus == "Pending")))
                .OrderBy(b => b.DepositDeadline)
                .Select(b => new CashDepositItem
                {
                    BookingId = b.BookingId,
                    BookingCode = b.BookingCode,
                    CustomerName = b.Customer.FullName,
                    DepositAmount = b.DepositAmount,
                    DepositDeadline = b.DepositDeadline,
                    IsDemo = b.IsDemo,
                    DepositMethod = b.DepositMethod,
                    BookingStatus = b.BookingStatus,
                    ActualAmount = b.IsDemo ? DemoPaymentOptions.TransferAmount : b.DepositAmount,
                    TransferContent = b.IsDemo ? db.DemoPayments.Where(p => p.BookingId == b.BookingId).Select(p => p.TransferContent).FirstOrDefault() ?? "" : ""
                })
                .ToListAsync();

            // DB lưu UTC; giao diện hiển thị giờ địa phương.
            foreach (var item in items)
            {
                item.DepositDeadline = AvailabilityRules.ToVietnamLocal(item.DepositDeadline);
            }

            return items;
        }
    }

    public class CashDepositItem : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public void RefreshDeadline()
        {
            foreach (var name in new[] { nameof(IsExpired), nameof(CanReceive), nameof(Explanation), nameof(ActualAmountLabel), nameof(StatusText) })
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
        public long BookingId { get; set; }
        public string BookingCode { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public decimal DepositAmount { get; set; }
        public DateTime DepositDeadline { get; set; }
        public bool IsDemo { get; set; }
        public string DepositMethod { get; set; } = "Cash";
        public string BookingStatus { get; set; } = "PendingDeposit";
        public decimal ActualAmount { get; set; }
        public string TransferContent { get; set; } = "";
        public string ModeText => IsDemo ? "QR DEMO" : DepositMethod == "Cash" ? "Tiền mặt" : "VNPay cũ";
        public bool IsExpired => BookingStatus == "Expired" ||
            (BookingStatus == "PendingDeposit" && DepositDeadline <= AvailabilityRules.ToVietnamLocal(DateTime.UtcNow));
        public bool CanReceive => IsDemo ? BookingStatus is "PendingDeposit" or "Cancelled" or "Expired"
            : DepositMethod == "Cash" && BookingStatus == "PendingDeposit" && !IsExpired;
        public string Explanation => IsDemo && (IsExpired || BookingStatus == "Cancelled")
            ? "Đơn đã hết hạn/hủy. Chỉ ghi nhận khoản 1.000đ đã thực nhận để đối soát hoàn tiền; không xác nhận lại đơn."
            : IsExpired ? "Đơn đã hết hạn đặt cọc. Không nhận thêm tiền hoặc xác nhận đơn này; khách cần tạo đơn mới."
            : DepositMethod != "Cash" && !IsDemo ? "Đơn VNPay thuộc luồng cũ, không xác nhận bằng tiền mặt hoặc QR demo. Khách cần tạo đơn QR demo mới."
            : "";
        public string DepositAmountLabel => Money(DepositAmount);
        public string ActualAmountLabel => CanReceive ? Money(IsDemo ? ActualAmount : DepositAmount) : "—";
        private static string Money(decimal value) => value.ToString(value == decimal.Truncate(value) ? "N0" : "N2", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + "đ";
        public string StatusText => IsDemo ? IsExpired || BookingStatus == "Cancelled"
            ? "Demo hủy/hết hạn · đối soát" : "Chờ nhận tiền demo"
            : IsExpired ? "Hết hạn đặt cọc" : DepositMethod == "Cash" ? "Chờ cọc tiền mặt" : "VNPay cũ · chỉ xem";
    }
}
