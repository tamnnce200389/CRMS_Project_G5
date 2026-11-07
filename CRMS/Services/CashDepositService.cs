using CRMS.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRMS.Services
{
    public class CashDepositService
    {
        public async Task<List<CashDepositItem>> GetPendingDepositsAsync()
        {
            using var db = CrmsContextFactory.Create();

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
                    b.BookingStatus == "PendingDeposit" &&
                    b.DepositMethod == "Cash" &&
                    b.DepositDeadline > DateTime.UtcNow &&
                    !db.Payments.Any(p =>
                        p.BookingId == b.BookingId &&
                        p.PaymentType == "Deposit" &&
                        p.PaymentStatus == "Success"))
                .OrderBy(b => b.DepositDeadline)
                .Select(b => new CashDepositItem
                {
                    BookingId = b.BookingId,
                    BookingCode = b.BookingCode,
                    CustomerName = b.Customer.FullName,
                    DepositAmount = b.DepositAmount,
                    DepositDeadline = b.DepositDeadline
                })
                .ToListAsync();

            // DB lưu UTC; giao diện hiển thị giờ địa phương.
            foreach (var item in items)
            {
                item.DepositDeadline = DateTime.SpecifyKind(
                    item.DepositDeadline,
                    DateTimeKind.Utc
                ).ToLocalTime();
            }

            return items;
        }
    }

    public class CashDepositItem
    {
        public long BookingId { get; set; }
        public string BookingCode { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public decimal DepositAmount { get; set; }
        public DateTime DepositDeadline { get; set; }
        public string StatusText => "Chờ cọc tiền mặt";
    }
}