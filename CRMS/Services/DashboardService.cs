
using System;
using System.Linq;
using System.Threading.Tasks;
using CRMS.Data;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Services
{
    // =====================================
    // STAFF DASHBOARD - CHI SO VAN HANH
    // =====================================
    // Khong chua thong tin tien / doanh thu

    public class StaffDashboardSnapshot
    {
        // Booking trong khoang ngay
        public int TotalBookings { get; set; }

        public int PendingDeposit { get; set; }
        public int Confirmed { get; set; }
        public int InUse { get; set; }
        public int Returned { get; set; }
        public int Completed { get; set; }
        public int Cancelled { get; set; }
        public int Expired { get; set; }

        // Tinh trang doi xe hien tai
        public int TotalVehicles { get; set; }
        public int VehiclesInUse { get; set; }
        public int VehiclesInMaintenance { get; set; }
    }

    // =====================================
    // ADMIN DASHBOARD - VAN HANH + TAI CHINH
    // =====================================
    // Ke thua chi so van hanh tu Staff
    // Giu ten DashboardSnapshot cho Admin VM cu

    public class DashboardSnapshot : StaffDashboardSnapshot
    {
        public decimal DepositCollected { get; set; }
        public decimal BalanceCollected { get; set; }
        public decimal RefundedAmount { get; set; }

        public decimal NetCashCollected =>
            DepositCollected
            + BalanceCollected
            - RefundedAmount;
    }

    // =====================================
    // DASHBOARD SERVICE DUNG CHUNG
    // =====================================

    public class DashboardService
    {
        // =====================================
        // 1. ADMIN DASHBOARD
        // =====================================
        // Chi Admin duoc xem tai chinh
        // Giu ten method cu

        public async Task<DashboardSnapshot> GetOverviewAsync(
            DateTime fromDateVN,
            DateTime toDateVN)
        {
            var (fromUtc, endUtcExclusive) =
                GetUtcRange(fromDateVN, toDateVN);

            using var db = CrmsContextFactory.Create();

            var (actorId, role) =
                await AuthorizeAsync(
                    db,
                    adminOnly: true);

            // Lay chi so van hanh dung chung
            StaffDashboardSnapshot operations =
                await LoadOperationalStatsAsync(
                    db,
                    fromUtc,
                    endUtcExclusive);

            // =====================================
            // PAYMENT SUCCESS - ADMIN ONLY
            // =====================================

            var paymentTotals = await db.Payments
                .AsNoTracking()
                .Where(p =>
                    p.PaymentStatus == "Success" &&
                    p.PaidAt.HasValue &&
                    p.PaidAt.Value >= fromUtc &&
                    p.PaidAt.Value < endUtcExclusive &&
                    (
                        p.PaymentType == "Deposit" ||
                        p.PaymentType == "Balance" ||
                        p.PaymentType == "Refund"
                    ))
                .GroupBy(p => p.PaymentType)
                .Select(g => new
                {
                    Type = g.Key,
                    Amount = g.Sum(p => p.Amount)
                })
                .ToListAsync();

            decimal SumPayment(string type)
            {
                return paymentTotals
                    .Where(p => p.Type == type)
                    .Sum(p => p.Amount);
            }

            // Kiem tra phien khong thay doi
            CheckSession(actorId, role);

            // Tra ve ca van hanh va tai chinh
            return new DashboardSnapshot
            {
                TotalBookings = operations.TotalBookings,

                PendingDeposit = operations.PendingDeposit,
                Confirmed = operations.Confirmed,
                InUse = operations.InUse,
                Returned = operations.Returned,
                Completed = operations.Completed,
                Cancelled = operations.Cancelled,
                Expired = operations.Expired,

                TotalVehicles = operations.TotalVehicles,
                VehiclesInUse = operations.VehiclesInUse,
                VehiclesInMaintenance =
                    operations.VehiclesInMaintenance,

                DepositCollected = SumPayment("Deposit"),
                BalanceCollected = SumPayment("Balance"),
                RefundedAmount = SumPayment("Refund")
            };
        }

        // =====================================
        // 2. STAFF DASHBOARD
        // =====================================
        // Staff / Admin xem duoc chi so van hanh
        // KHONG doc Payments

        public async Task<StaffDashboardSnapshot>
            GetStaffOverviewAsync(
                DateTime fromDateVN,
                DateTime toDateVN)
        {
            var (fromUtc, endUtcExclusive) =
                GetUtcRange(fromDateVN, toDateVN);

            using var db = CrmsContextFactory.Create();

            var (actorId, role) =
                await AuthorizeAsync(
                    db,
                    adminOnly: false);

            // Chi lay du lieu van hanh
            StaffDashboardSnapshot result =
                await LoadOperationalStatsAsync(
                    db,
                    fromUtc,
                    endUtcExclusive);

            CheckSession(actorId, role);

            return result;
        }

        // =====================================
        // 3. CHUYEN NGAY VIET NAM SANG UTC
        // =====================================

        private static (
            DateTime FromUtc,
            DateTime EndUtcExclusive)
            GetUtcRange(
                DateTime fromDateVN,
                DateTime toDateVN)
        {
            if (toDateVN.Date < fromDateVN.Date)
            {
                throw new ArgumentException(
                    "Ngày kết thúc phải lớn hơn " +
                    "hoặc bằng ngày bắt đầu.");
            }

            if (toDateVN.Date == DateTime.MaxValue.Date)
            {
                throw new ArgumentException(
                    "Ngày kết thúc không hợp lệ.");
            }

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

            return (fromUtc, endUtcExclusive);
        }

        // =====================================
        // 4. PHAN QUYEN ADMIN / STAFF
        // =====================================

        private static async Task<(long UserId, string Role)>
            AuthorizeAsync(
                CrmsContext db,
                bool adminOnly)
        {
            var session = CurrentUserService.Instance;

            if (!session.UserId.HasValue)
            {
                throw new UnauthorizedAccessException(
                    "Vui lòng đăng nhập.");
            }

            long actorId = session.UserId.Value;
            string sessionRole = session.Role;

            // Kiem tra role thuc te trong SQL Server
            string? databaseRole = await db.Users
                .AsNoTracking()
                .Where(u =>
                    u.UserId == actorId &&
                    u.IsActive)
                .Select(u => u.UserRole)
                .SingleOrDefaultAsync();

            if (databaseRole == null ||
                databaseRole != sessionRole)
            {
                throw new UnauthorizedAccessException(
                    "Phiên đăng nhập không hợp lệ " +
                    "hoặc tài khoản đã bị khóa.");
            }

            // Admin xem tai chinh
            // Staff / Admin xem van hanh
            bool allowed = adminOnly
                ? databaseRole == "Admin"
                : databaseRole == "Admin" ||
                  databaseRole == "Staff";

            if (!allowed)
            {
                throw new UnauthorizedAccessException(
                    adminOnly
                        ? "Chỉ Admin mới được xem thống kê tài chính."
                        : "Chỉ Staff/Admin mới được xem chỉ số vận hành.");
            }

            return (actorId, databaseRole);
        }

        // =====================================
        // 5. KIEM TRA PHIEN DANG NHAP
        // =====================================

        private static void CheckSession(
            long userId,
            string role)
        {
            var session = CurrentUserService.Instance;

            if (session.UserId != userId ||
                session.Role != role)
            {
                throw new UnauthorizedAccessException(
                    "Phiên đăng nhập đã thay đổi. " +
                    "Vui lòng tải lại Dashboard.");
            }
        }

        // =====================================
        // 6. THONG KE VAN HANH DUNG CHUNG
        // =====================================

        private static async Task<StaffDashboardSnapshot>
            LoadOperationalStatsAsync(
                CrmsContext db,
                DateTime fromUtc,
                DateTime endUtcExclusive)
        {
            // =====================================
            // BOOKING THEO TRANG THAI
            // =====================================

            var bookingCounts = await db.Bookings
                .AsNoTracking()
                .Where(b =>
                    b.CreatedAt >= fromUtc &&
                    b.CreatedAt < endUtcExclusive)
                .GroupBy(b => b.BookingStatus)
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            int CountStatus(string status)
            {
                return bookingCounts
                    .Where(b => b.Status == status)
                    .Sum(b => b.Count);
            }

            // =====================================
            // TINH TRANG XE HIEN TAI
            // =====================================

            DateTime nowUtc = DateTime.UtcNow;

            // Tong so xe
            int totalVehicles = await db.Vehicles
                .AsNoTracking()
                .CountAsync();

            // Xe dang duoc su dung
            int vehiclesInUse = await db.Bookings
                .AsNoTracking()
                .Where(b =>
                    b.BookingStatus == "InUse")
                .Select(b => b.VehicleId)
                .Distinct()
                .CountAsync();

            // Xe co lich bao tri tai thoi diem hien tai
            int vehiclesInMaintenance =
                await db.MaintenanceRecords
                    .AsNoTracking()
                    .Where(m =>
                        (
                            m.MaintenanceStatus == "Scheduled" ||
                            m.MaintenanceStatus == "InProgress"
                        ) &&
                        m.StartAt <= nowUtc &&
                        m.EndAt > nowUtc)
                    .Select(m => m.VehicleId)
                    .Distinct()
                    .CountAsync();

            // =====================================
            // TRA VE CHI SO VAN HANH
            // =====================================

            return new StaffDashboardSnapshot
            {
                TotalBookings =
                    bookingCounts.Sum(b => b.Count),

                PendingDeposit =
                    CountStatus("PendingDeposit"),

                Confirmed =
                    CountStatus("Confirmed"),

                InUse =
                    CountStatus("InUse"),

                Returned =
                    CountStatus("Returned"),

                Completed =
                    CountStatus("Completed"),

                Cancelled =
                    CountStatus("Cancelled"),

                Expired =
                    CountStatus("Expired"),

                TotalVehicles = totalVehicles,

                VehiclesInUse = vehiclesInUse,

                VehiclesInMaintenance =
                    vehiclesInMaintenance
            };
        }
    }
}
