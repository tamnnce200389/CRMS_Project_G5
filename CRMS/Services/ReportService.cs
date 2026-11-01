
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRMS.Data;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Services
{
    // =====================================
    // 1. MOT DONG BAO CAO THEO BOOKING
    // =====================================

    public class RevenueReportRow
    {
        public long BookingId { get; set; }

        public string BookingCode { get; set; } = "";

        public string VehicleName { get; set; } = "";

        public string LicensePlate { get; set; } = "";

        public string BookingStatus { get; set; } = "";

        // Tong gia tri don thue sau giam gia
        // Chi de tham khao, KHONG phai tien da thu
        public decimal BookingTotal { get; set; }

        public decimal DepositPaid { get; set; }

        public decimal BalancePaid { get; set; }

        public decimal RefundPaid { get; set; }

        // Co the am neu trong ky hoan tien
        // nhieu hon so tien thu duoc trong ky
        public decimal NetCollected =>
            DepositPaid + BalancePaid - RefundPaid;

        public int TransactionCount { get; set; }

        public DateTime LastPaymentAtVN { get; set; }
    }

    // =====================================
    // 2. KET QUA BAO CAO
    // =====================================

    public class RevenueReportResult
    {
        public DateTime FromDateVN { get; set; }

        public DateTime ToDateVN { get; set; }

        public List<RevenueReportRow> Rows { get; set; }
            = new();

        public int TotalBookings => Rows.Count;

        public int TotalTransactions =>
            Rows.Sum(x => x.TransactionCount);

        public decimal TotalDeposits =>
            Rows.Sum(x => x.DepositPaid);

        public decimal TotalBalances =>
            Rows.Sum(x => x.BalancePaid);

        public decimal TotalRefunds =>
            Rows.Sum(x => x.RefundPaid);

        public decimal NetRevenue =>
            TotalDeposits + TotalBalances - TotalRefunds;
    }

    // =====================================
    // 3. REPORT SERVICE
    // =====================================

    public class ReportService
    {
        // =====================================
        // LAY BAO CAO DOANH THU
        // =====================================

        public async Task<RevenueReportResult>
            GetRevenueReportAsync(
                DateTime fromDateVN,
                DateTime toDateVN)
        {
            if (fromDateVN.Date > toDateVN.Date)
            {
                throw new ArgumentException(
                    "Ngày bắt đầu không được sau ngày kết thúc.");
            }

            if (toDateVN.Date == DateTime.MaxValue.Date)
            {
                throw new ArgumentException(
                    "Ngày kết thúc không hợp lệ.");
            }

            var session = CurrentUserService.Instance;

            if (!session.IsAdmin || !session.UserId.HasValue)
            {
                throw new UnauthorizedAccessException(
                    "Chỉ Admin mới được xem báo cáo doanh thu.");
            }

            long adminId = session.UserId.Value;

            using var db = CrmsContextFactory.Create();

            // =====================================
            // KIEM TRA QUYEN TREN DATABASE
            // =====================================

            bool authorized = await db.Users
                .AsNoTracking()
                .AnyAsync(u =>
                    u.UserId == adminId &&
                    u.UserRole == "Admin" &&
                    u.IsActive);

            if (!authorized)
            {
                throw new UnauthorizedAccessException(
                    "Tài khoản không có quyền xem báo cáo.");
            }

            // =====================================
            // CHUYEN NGAY VIET NAM SANG UTC
            // =====================================

            var vietnamZone =
                TimeZoneInfo.FindSystemTimeZoneById(
                    "SE Asia Standard Time");

            DateTime fromUtc =
                TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(
                        fromDateVN.Date,
                        DateTimeKind.Unspecified),
                    vietnamZone);

            DateTime endUtcExclusive =
                TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(
                        toDateVN.Date.AddDays(1),
                        DateTimeKind.Unspecified),
                    vietnamZone);

            // =====================================
            // TRUY VAN PAYMENT SUCCESS
            // =====================================
            // Join truc tiep de khong phu thuoc
            // navigation Booking.Payment 1-1 cu
            //
            // Chi lay giao dich da thanh cong,
            // co PaidAt trong khoang ngay.
            // =====================================

            var transactions = await (
                from payment in db.Payments.AsNoTracking()

                join booking in db.Bookings.AsNoTracking()
                    on payment.BookingId equals booking.BookingId

                join vehicle in db.Vehicles.AsNoTracking()
                    on booking.VehicleId equals vehicle.VehicleId

                where
                    payment.PaymentStatus == "Success" &&
                    payment.PaidAt.HasValue &&
                    payment.PaidAt.Value >= fromUtc &&
                    payment.PaidAt.Value < endUtcExclusive &&
                    (
                        payment.PaymentType == "Deposit" ||
                        payment.PaymentType == "Balance" ||
                        payment.PaymentType == "Refund"
                    )

                select new
                {
                    booking.BookingId,
                    booking.BookingCode,
                    booking.BookingStatus,

                    BookingTotal = booking.TotalAmount,

                    VehicleName =
                        vehicle.Brand + " " + vehicle.Model,

                    vehicle.LicensePlate,

                    payment.PaymentId,
                    payment.PaymentType,
                    payment.Amount,

                    PaidAtUtc = payment.PaidAt.Value
                }
            ).ToListAsync();

            // =====================================
            // GOM PAYMENT THEO BOOKING
            // =====================================

            var rows = transactions
                .GroupBy(t => new
                {
                    t.BookingId,
                    t.BookingCode,
                    t.BookingStatus,
                    t.BookingTotal,
                    t.VehicleName,
                    t.LicensePlate
                })
                .Select(g => new RevenueReportRow
                {
                    BookingId = g.Key.BookingId,

                    BookingCode = g.Key.BookingCode,

                    BookingStatus = g.Key.BookingStatus,

                    BookingTotal = g.Key.BookingTotal,

                    VehicleName = g.Key.VehicleName,

                    LicensePlate = g.Key.LicensePlate,

                    DepositPaid = g
                        .Where(p => p.PaymentType == "Deposit")
                        .Sum(p => p.Amount),

                    BalancePaid = g
                        .Where(p => p.PaymentType == "Balance")
                        .Sum(p => p.Amount),

                    RefundPaid = g
                        .Where(p => p.PaymentType == "Refund")
                        .Sum(p => p.Amount),

                    TransactionCount = g.Count(),

                    // PaidAt duoc luu theo UTC
                    LastPaymentAtVN =
                        g.Max(p => p.PaidAtUtc).AddHours(7)
                })
                .OrderByDescending(x => x.LastPaymentAtVN)
                .ThenByDescending(x => x.BookingId)
                .ToList();

            // =====================================
            // KIEM TRA PHIEN DANG NHAP
            // =====================================

            if (session.UserId != adminId || !session.IsAdmin)
            {
                throw new UnauthorizedAccessException(
                    "Phiên đăng nhập đã thay đổi.");
            }

            return new RevenueReportResult
            {
                FromDateVN = fromDateVN.Date,
                ToDateVN = toDateVN.Date,
                Rows = rows
            };
        }

        // =====================================
        // 4. TAO NOI DUNG CSV
        // =====================================
        // Su dung chinh danh sach Rows
        // dang hien thi tren DataGrid.
        //
        // ViewModel se chon duong dan va luu file.
        // Service nay khong sua database.
        // =====================================

        public string BuildCsv(RevenueReportResult report)
        {
            var sb = new StringBuilder();

            sb.AppendLine(
                "BookingId,MaDon,BienSo,Xe,TrangThai," +
                "TongTienDon,TienCocDaThu,TienConLaiDaThu," +
                "TienDaHoan,ThucThuRong,SoGiaoDich," +
                "GiaoDichGanNhatVN");

            foreach (var row in report.Rows)
            {
                sb.AppendLine(string.Join(",",
                    row.BookingId.ToString(),

                    CsvText(row.BookingCode),

                    CsvText(row.LicensePlate),

                    CsvText(row.VehicleName),

                    CsvText(row.BookingStatus),

                    Money(row.BookingTotal),

                    Money(row.DepositPaid),

                    Money(row.BalancePaid),

                    Money(row.RefundPaid),

                    Money(row.NetCollected),

                    row.TransactionCount.ToString(),

                    CsvText(row.LastPaymentAtVN
                        .ToString("dd/MM/yyyy HH:mm"))
                ));
            }

            // Dong tong ket
            sb.AppendLine(string.Join(",",
                "",
                "TONG CONG",
                "",
                "",
                "",
                "",
                Money(report.TotalDeposits),
                Money(report.TotalBalances),
                Money(report.TotalRefunds),
                Money(report.NetRevenue),
                report.TotalTransactions.ToString(),
                ""
            ));

            return sb.ToString();
        }

        // So tien CSV: khong dung dau phan cach nghin
        private static string Money(decimal amount)
        {
            return amount.ToString(
                "0.00",
                CultureInfo.InvariantCulture);
        }

        // Escape CSV va tranh Excel tu thuc thi
        // noi dung dang cong thuc tu du lieu text
        private static string CsvText(string? value)
        {
            string text = value ?? "";

            string trimmed = text.TrimStart();

            if (trimmed.Length > 0 &&
                "=+-@".Contains(trimmed[0]))
            {
                text = "'" + text;
            }

            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
    }
}
