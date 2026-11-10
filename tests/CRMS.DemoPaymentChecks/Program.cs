using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using CRMS.Data;
using CRMS.Models;
using CRMS.Services;
using CRMS.Services.Payments;
using CRMS.Services.Vehicles;
using CRMS.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); passed++;
}
async Task Reject(Func<Task> action, string name)
{
    try { await action(); }
    catch (InvalidOperationException) { Check(true, name); return; }
    throw new Exception("FAIL accepted: " + name);
}
var options = new DemoPaymentOptions { Enabled = true, BankBin = "970436", BankName = "TEST BANK", AccountNumber = "0000000001", AccountName = "TEST RECEIVER" };
ReceiverConfigurationChecks.Run(Check);
var payload = VietQrCode.CreatePayload(options.BankBin, options.AccountNumber, "DEMO123456789");
Dictionary<string, string> Fields(string text)
{
    var result = new Dictionary<string, string>();
    for (int index = 0; index < text.Length;)
    {
        var tag = text.Substring(index, 2); int length = int.Parse(text.Substring(index + 2, 2));
        result.Add(tag, text.Substring(index + 4, length)); index += length + 4;
    }
    return result;
}
var qr = Fields(payload);
Check(qr["54"] == "1000" && qr["53"] == "704", "QR carries exactly 1,000 VND rather than the 2,000,000 VND simulated deposit");
Check(Fields(Fields(qr["38"])["01"])["01"] == "0000000001" && Fields(qr["62"])["08"] == "DEMO123456789", "QR preserves receiver leading zeros and the per-booking reconciliation content");
Check(VietQrCode.Crc16("123456789") == "29B1" && payload[^4..] == VietQrCode.Crc16(payload[..^4]), "EMV CRC16 matches the independent CCITT-FALSE check vector");
var booking = new Booking { BookingId = 1, BookingCode = "TEST-DEMO", IsDemo = true, TotalAmount = 4000000, DepositAmount = 2000000, BookingStatus = "Confirmed" };
var demo = new DemoPayment { BookingId = 1, SimulatedDepositAmount = 2000000, RequiredTransferAmount = 1000, ActualAmount = 1000, PaymentStatus = "Success", ConfirmedBooking = true };
var summary = PaymentRules.Summarize(booking, new[] { new Payment { BookingId = 1, PaymentType = "Deposit", PaymentStatus = "Success", Amount = 2000000 } }, null, demo);
Check(summary.IsDemo && summary.SimulatedDepositPaid == 2000000 && summary.NetPaid == 1000 && summary.SimulatedOutstandingBalance == 2000000 && summary.OutstandingBalance == 0, "Summary separates simulated deposit, actual receipt and simulated remaining balance");
booking.BookingStatus = "Cancelled";
summary = PaymentRules.Summarize(booking, Array.Empty<Payment>(), null, demo);
Check(summary.RefundableAmount == 1000 && summary.OutstandingBalance == 0, "Cancelled demo may refund only the actual 1,000 VND");
await Reject(() => Task.Run(() => PaymentRules.ValidateCashDeposit(booking, 2000000, DateTime.UtcNow)), "Demo cannot be confirmed as a real cash deposit");
var pending = new DemoPaymentSession(1, "TEST-DEMO", "PendingDeposit", 4000000, 2000000, 1000, 0, "DEMO123456789", options.BankBin, options.BankName,
    options.AccountNumber, options.AccountName, "Pending", false, DateTime.UtcNow.AddHours(1), null);
Check(pending.CanTransfer && !pending.IsSuccessful && !(pending with { Deadline = DateTime.UtcNow.AddSeconds(-1) }).CanTransfer, "QR eligibility stops at the deposit deadline");
var clock = DateTime.UtcNow;
Check(BookingStatusRules.EffectiveStatus("PendingDeposit", clock, clock) == "Expired" &&
    BookingStatusRules.EffectiveStatus("PendingDeposit", clock.AddSeconds(1), clock) == "PendingDeposit" &&
    BookingStatusRules.EffectiveStatus("Confirmed", clock.AddDays(-1), clock) == "Confirmed",
    "Only pending holds expire at the deadline; already-confirmed rentals keep their status");
var expiredTrip = new CRMS.Dtos.CustomerTrip("QA-PENDING-CASH", "Fixture", clock, clock.AddDays(1), 4000000,
    "PendingDeposit", IsDemo: true, DepositDeadlineUtc: clock.AddDays(-1));
Check(expiredTrip.StatusLabel == "Hết hạn đặt cọc" && expiredTrip.DemoPaymentAction == "Xem trạng thái demo",
    "Customer sample trips derive expired labels and stop inviting a new demo transfer");
var expiredSummary = new PaymentSummary(1, "QA-EXPIRED", "PendingDeposit", 4000000, 0, 2000000, 0, 0, 0, 0, false,
    DepositDeadlineUtc: clock.AddDays(-1));
Check(expiredSummary.EffectiveStatus == "Expired" && expiredSummary.OutstandingBalance == 0,
    "Payment summary derives an expired hold and never shows expired rent as a collectable balance");
CurrentUserService.Instance.SetUser(new User { UserId = 1, UserRole = "Customer", FullName = "Fixture" });
var fake = new FakeDemo(pending);
using (var vm = new DemoPaymentViewModel(1, fake, true))
{
    await vm.RefreshAsync();
    Check(vm.CanShowQr && vm.QrImage != null && !vm.IsSuccessful && vm.StatusTitle.Contains("Chờ Staff"), "Opening or refreshing a QR never marks a deposit as paid");
    fake.Session = pending with { PaymentStatus = "Success", ConfirmedBooking = true, ActualReceived = 1000, BookingStatus = "Confirmed" };
    await vm.RefreshAsync();
    Check(vm.IsSuccessful && !vm.CanShowQr && vm.QrImage == null && vm.StatusTitle == "Thanh toán demo thành công" && vm.BookingStatusLabel == "Đơn thuê: Đã xác nhận", "Customer refresh reflects only the Staff-confirmed success state");
    fake.Session = pending;
    await vm.RefreshAsync(); fake.Fail = true; await vm.RefreshAsync();
    Check(!vm.CanShowQr && vm.QrImage == null, "Failed status refresh hides the transfer QR rather than inviting another payment");
}
fake.Fail = false;
using (var vm = new DemoPaymentViewModel(1, fake, false))
{
    await vm.RefreshAsync(); Check(!vm.CanShowQr && vm.QrImage == null, "Demo-off mode never shows a payable QR");
}

await StaffConfirmationChecks.Run(Check);
if (!args.Contains("--integration")) { Console.WriteLine($"{passed} demo checks passed. Add --integration <CRMS.sql path> for isolated SQL checks."); return; }
var source = args.FirstOrDefault(a => a.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException("Pass CRMS.sql path.");
var dbName = "CRMS_DemoPay_Test_" + Guid.NewGuid().ToString("N");
var master = new SqlConnectionStringBuilder { DataSource = Environment.GetEnvironmentVariable("CRMS_TEST_SERVER") ?? "localhost", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 10 }.ConnectionString;
var connection = new SqlConnectionStringBuilder(master) { InitialCatalog = dbName }.ConnectionString;
CrmsContext Create() => new(new DbContextOptionsBuilder<CrmsContext>().UseSqlServer(connection).Options);
async Task Exec(string conn, string sql)
{
    await using var db = new SqlConnection(conn); await db.OpenAsync();
    await using var command = new SqlCommand(sql, db) { CommandTimeout = 90 }; await command.ExecuteNonQueryAsync();
}
bool created = false;
try
{
    await Exec(master, $"CREATE DATABASE [{dbName}]"); created = true;
    var sql = await File.ReadAllTextAsync(source); const string marker = "USE [CRMS];";
    int index = sql.IndexOf(marker, StringComparison.Ordinal); if (index < 0) throw new Exception("Missing schema boundary");
    sql = sql[(index + marker.Length)..];
    Check(sql.Contains("DECLARE @IncludeDemoData bit = 1;"), "Shared SQL enables sample data by default without teammates editing the file");
    if (Regex.IsMatch(sql, @"(?im)^\s*(USE\s|CREATE\s+DATABASE|DROP\s+DATABASE)")) throw new Exception("Unsafe schema switching");
    foreach (var batch in Regex.Split(sql, @"(?im)^\s*GO\s*\r?$").Where(b => !string.IsNullOrWhiteSpace(b))) await Exec(connection, batch);
    using (var db = Create())
    {
        Check(await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.tables WHERE object_id=OBJECT_ID(N'dbo.DemoPayments')").SingleAsync() == 1 &&
            !await db.Bookings.AnyAsync(b => b.IsDemo), "Full CRMS.sql alone creates the QR demo schema on a fresh database");
    }
    var legacyCounts = "SELECT CONCAT((SELECT COUNT(*) FROM dbo.Users),':',(SELECT COUNT(*) FROM dbo.Bookings),':',(SELECT COUNT(*) FROM dbo.Payments),':',(SELECT COALESCE(SUM(TotalAmount),0) FROM dbo.Bookings),':',(SELECT COALESCE(SUM(Amount),0) FROM dbo.Payments)) AS [Value]";
    var upgradeSql = sql.Replace("DECLARE @IncludeDemoData bit = 1;", "DECLARE @IncludeDemoData bit = 0;");
    string beforeUpgrade;
    using (var db = Create()) beforeUpgrade = await db.Database.SqlQueryRaw<string>(legacyCounts).SingleAsync();
    // Only this randomly named test database is downgraded to the previous schema.
    if (!dbName.StartsWith("CRMS_DemoPay_Test_", StringComparison.Ordinal)) throw new Exception("Invalid test database name");
    await Exec(connection, "DROP TABLE dbo.DemoPayments; ALTER TABLE dbo.Bookings DROP CONSTRAINT DF_Bookings_IsDemo; ALTER TABLE dbo.Bookings DROP COLUMN IsDemo;");
    foreach (var batch in Regex.Split(upgradeSql, @"(?im)^\s*GO\s*\r?$").Where(b => !string.IsNullOrWhiteSpace(b))) await Exec(connection, batch);
    using (var db = Create())
    {
        Check(await db.Database.SqlQueryRaw<string>(legacyCounts).SingleAsync() == beforeUpgrade && !await db.Bookings.AnyAsync(b => b.IsDemo) && !await db.DemoPayments.AnyAsync(),
            "Full CRMS.sql upgrades an existing database without changing users, bookings or payment totals");
    }
    User customer, other, staff, admin;
    long vehicle;
    using (var db = Create())
    {
        Check(!await db.Bookings.AnyAsync(b => b.IsDemo), "Migration is idempotent and preserves existing bookings as non-demo");
        customer = await db.Users.FirstAsync(u => u.UserRole == "Customer" && u.IsActive);
        other = await db.Users.FirstAsync(u => u.UserRole == "Customer" && u.UserId != customer.UserId && u.IsActive);
        staff = await db.Users.FirstAsync(u => u.UserRole == "Staff" && u.IsActive);
        admin = await db.Users.FirstAsync(u => u.UserRole == "Admin" && u.IsActive);
        vehicle = await db.Vehicles.Where(v => v.LicensePlate == "65A-12345").Select(v => v.VehicleId).SingleAsync();
        await db.Vehicles.Where(v => v.VehicleId == vehicle).ExecuteUpdateAsync(s => s.SetProperty(v => v.DailyRate, 4000000m));
    }
    long? actor = customer.UserId;
    var create = new CustomerBookingService(Create, () => actor, options);
    var payments = new DemoPaymentService(Create, () => actor, options);
    var realPayments = new PaymentService(Create, () => actor);
    int sequence = 0;
    BookingSelection Selection(bool isDemo = true)
    {
        var start = AvailabilityRules.ToVietnamLocal(DateTime.UtcNow).Date.AddDays(60 + sequence++ * 2).AddHours(9);
        return new(customer.UserId, vehicle, "SelfDrive", start, start.AddDays(1), ExpectedTotal: 4000000, IsDemo: isDemo);
    }
    var cancelledRequest = Guid.NewGuid();
    int identityReads = 0;
    var changingSession = new CustomerBookingService(Create, () => ++identityReads == 1 ? customer.UserId : other.UserId, options);
    await Reject(() => changingSession.CreateAsync(Selection(), cancelledRequest), "Session change rolls back booking creation and its pending demo receipt");
    using (var db = Create()) Check(!await db.Bookings.AnyAsync(b => b.RequestKey == cancelledRequest), "Failed creation leaves no orphan booking or reserved slot");
    var selection = Selection(); var request = Guid.NewGuid();
    var order = await create.CreateAsync(selection, request);
    Check(order is { IsDemo: true, TotalAmount: 4000000, DepositAmount: 2000000 }, "Create 4,000,000 VND demo order with a 2,000,000 VND simulated deposit");
    Check((await create.CreateAsync(selection, request)).BookingId == order.BookingId, "Retrying booking creation reuses the same demo order");
    using (var db = Create())
        Check(await db.DemoPayments.CountAsync(p => p.BookingId == order.BookingId) == 1 && !await db.Payments.AnyAsync(p => p.BookingId == order.BookingId), "Creation writes one pending demo receipt and no fictitious Payments entry");
    var session = await payments.GetSessionAsync(order.BookingId);
    Check(session.TransferAmount == 1000 && session.ActualReceived == 0 && session.CanTransfer && session.AccountNumber == options.AccountNumber, "QR session freezes the receiver and the 1,000 VND transfer amount");
    CurrentUserService.Instance.SetUser(customer);
    var tripSession = new FixedCustomerSession(customer.UserId);
    var tripsVm = new MyBookingsViewModel(new BookingQueryService(Create, tripSession), tripSession);
    await tripsVm.LoadAsync();
    var trip = tripsVm.Bookings.Single(t => t.BookingId == order.BookingId);
    Check(trip.IsDemo && trip.DepositMethod == "Cash" && trip.DepositDeadlineUtc == order.DepositDeadline && trip.DemoPaymentAction == "Thanh toán QR demo",
        "MVVM booking query retains demo action, booking ID and deposit deadline after merging dev");
    using var liveCustomer = new DemoPaymentViewModel(order.BookingId, new DemoPaymentService(Create, () => customer.UserId, options), true);
    await liveCustomer.RefreshAsync();
    Check(liveCustomer.LastRefreshSucceeded && !liveCustomer.IsSuccessful, "Real SQL: an open customer payment screen initially reads the pending order");
    actor = other.UserId;
    await Reject(() => payments.GetSessionAsync(order.BookingId), "Another customer cannot read the demo QR or receipt");
    actor = customer.UserId;
    await Reject(() => payments.ConfirmReceivedAsync(order.BookingId, 1000, "BANK-TEST-001", Guid.NewGuid()), "Customer cannot confirm their own demo payment");
    actor = staff.UserId;
    await Reject(() => payments.ConfirmReceivedAsync(order.BookingId, 2000000, "BANK-TEST-001", Guid.NewGuid()), "Staff cannot record the simulated deposit as actual money");
    await Reject(() => payments.ConfirmReceivedAsync(order.BookingId, 1000, "", Guid.NewGuid()), "Staff confirmation requires a bank reconciliation reference");
    await Reject(() => new DemoPaymentService(Create, () => actor, options with { Enabled = false }).ConfirmReceivedAsync(order.BookingId, 1000, "BANK-TEST-001", Guid.NewGuid()), "Demo confirmation is blocked when demo mode is off");
    var confirmKey = Guid.NewGuid();
    var receipt = await payments.ConfirmReceivedAsync(order.BookingId, 1000, "BANK-TEST-001", confirmKey);
    session = await payments.GetSessionAsync(order.BookingId);
    Check(!receipt.NeedsManualRefund && session.IsSuccessful && session.BookingStatus == "Confirmed" && session.ActualReceived == 1000, "Staff receipt confirmation atomically marks demo successful and booking Confirmed");
    tripsVm.UpdatePaymentSession(session);
    Check(tripsVm.Bookings.Single(t => t.BookingId == order.BookingId).StatusLabel == "Đã xác nhận" &&
        tripsVm.Bookings.Single(t => t.BookingId == order.BookingId).DemoPaymentAction == "Xem biên nhận demo",
        "MVVM trip list immediately receives the confirmed QR status without closing the payment screen");
    await liveCustomer.RefreshAsync(waitForCurrent: true);
    Check(liveCustomer.LastRefreshSucceeded && liveCustomer.IsSuccessful && liveCustomer.StatusCheckResult.Contains("Đặt cọc demo thành công") &&
        !liveCustomer.CanShowQr && liveCustomer.BookingStatusLabel == "Đơn thuê: Đã xác nhận",
        "Real SQL: manual check in the same customer screen reports success immediately after Staff commits");
    using (var db = Create())
        Check(!await db.Payments.AnyAsync(p => p.BookingId == order.BookingId) && await db.BookingStatusHistories.CountAsync(h => h.BookingId == order.BookingId && h.NewStatus == "Confirmed") == 1, "Success never inserts the simulated 2,000,000 VND into Payments or duplicates status history");
    Check((await payments.ConfirmReceivedAsync(order.BookingId, 1000, "BANK-TEST-001", confirmKey)).AlreadyRecorded, "Repeated Staff confirmation is idempotent");
    await Reject(() => payments.ConfirmReceivedAsync(order.BookingId, 1000, "ANOTHER-BANK-REF", Guid.NewGuid()), "Already-paid demo cannot accept a second bank transfer");
    var totals = await realPayments.GetPaymentSummary(order.BookingId);
    Check(totals.NetPaid == 1000 && totals.SimulatedDepositPaid == 2000000 && totals.SimulatedOutstandingBalance == 2000000 && totals.OutstandingBalance == 0, "Shared payment summary keeps actual and simulated balances distinct");
    Check(await realPayments.GetOutstandingBalance(order.BookingId) == 0, "Shared collection API never exposes the simulated remainder as money to collect");
    await Reject(() => realPayments.ConfirmCashDeposit(order.BookingId, 2000000, Guid.NewGuid()), "Real cash receipt route rejects demo booking");
    await Reject(() => realPayments.RecordFinalPayment(order.BookingId, 2000000, Guid.NewGuid()), "Real balance collection cannot charge the simulated remainder");
    actor = customer.UserId;
    var second = await create.CreateAsync(Selection(), Guid.NewGuid());
    actor = staff.UserId;
    await Reject(() => payments.ConfirmReceivedAsync(second.BookingId, 1000, "BANK-TEST-001", Guid.NewGuid()), "One bank transaction cannot confirm two different orders");
    int confirmReads = 0;
    var changingStaff = new DemoPaymentService(Create, () => ++confirmReads == 1 ? staff.UserId : other.UserId, options);
    await Reject(() => changingStaff.ConfirmReceivedAsync(second.BookingId, 1000, "BANK-SESSION-CHANGE", Guid.NewGuid()), "Session change prevents confirmation even after Staff authorization");
    Check((await payments.GetSessionAsync(second.BookingId)).PaymentStatus == "Pending", "Aborted Staff confirmation does not record an actual receipt");
    using (var db = Create())
        await db.Bookings.Where(b => b.BookingId == second.BookingId).ExecuteUpdateAsync(s => s.SetProperty(b => b.DepositDeadline, DateTime.UtcNow.AddMinutes(-1)));
    var late = await payments.ConfirmReceivedAsync(second.BookingId, 1000, "BANK-TEST-LATE", Guid.NewGuid());
    Check(late.NeedsManualRefund && (await payments.GetSessionAsync(second.BookingId)).BookingStatus == "Expired", "Late actual receipt is recorded for reconciliation and does not reactivate the booking");
    actor = customer.UserId;
    var concurrentOrder = await create.CreateAsync(Selection(), Guid.NewGuid());
    actor = staff.UserId;
    var duplicateResults = await Task.WhenAll(
        payments.ConfirmReceivedAsync(concurrentOrder.BookingId, 1000, "BANK-CONCURRENT", Guid.NewGuid()),
        payments.ConfirmReceivedAsync(concurrentOrder.BookingId, 1000, "BANK-CONCURRENT", Guid.NewGuid()));
    Check(duplicateResults.Count(r => r.AlreadyRecorded) == 1 && duplicateResults.Select(r => r.DemoPaymentId).Distinct().Count() == 1,
        "Two Staff windows confirming simultaneously still record only one actual receipt");
    await Exec(connection, $"UPDATE dbo.Bookings SET BookingStatus='Cancelled', UpdatedAt=SYSUTCDATETIME() WHERE BookingId={order.BookingId}");
    totals = await realPayments.GetPaymentSummary(order.BookingId);
    Check(totals.RefundableAmount == 1000, "Refund limit is actual 1,000 VND after cancellation");
    await Reject(() => realPayments.RecordManualRefund(order.BookingId, 2000000, "BankTransfer", "REFUND-EXCESS", Guid.NewGuid()), "Cannot refund the simulated 2,000,000 VND");
    await realPayments.RecordManualRefund(order.BookingId, 1000, "BankTransfer", "REFUND-ACTUAL-1000", Guid.NewGuid());
    Check((await realPayments.GetPaymentSummary(order.BookingId)).RefundableAmount == 0, "Actual refund closes the demo's refundable cash balance");
    CurrentUserService.Instance.SetUser(staff);
    await Exec(connection, "UPDATE dbo.Bookings SET DepositDeadline=DATEADD(day,-1,SYSUTCDATETIME()) WHERE BookingCode IN ('QA-PENDING-CASH','QA-PENDING-VNPAY')");
    var list = await new CashDepositService(Create).GetPendingDepositsAsync();
    Check(list.All(row => row.BookingId != order.BookingId && row.BookingId != second.BookingId), "Successful or reconciled demo receipts disappear from the Staff pending list");
    var archived = await new CashDepositService(Create).GetDepositsAsync(true);
    var sampleOrders = archived.Where(r => r.BookingCode is "QA-PENDING-CASH" or "QA-PENDING-VNPAY").ToList();
    Check(sampleOrders.Count == 2 && sampleOrders.All(r => !r.CanReceive && !r.IsDemo && r.StatusText == "Hết hạn đặt cọc") &&
        list.All(r => r.BookingCode is not ("QA-PENDING-CASH" or "QA-PENDING-VNPAY")),
        "Real SQL: expired Cash and VNPay samples appear only in the optional read-only list");
    using (var db = Create()) Check(await db.Bookings.CountAsync(b => (b.BookingCode == "QA-PENDING-CASH" || b.BookingCode == "QA-PENDING-VNPAY") && b.BookingStatus == "PendingDeposit") == 2,
        "Viewing sample expiry never rewrites booking history or changes legacy orders to QR demo");
    using (var db = Create())
    {
        var sampleId = await db.Bookings.Where(b => b.BookingCode == "QA-PENDING-CASH").Select(b => b.BookingId).SingleAsync();
        var sampleSummary = await realPayments.GetPaymentSummary(sampleId);
        Check(sampleSummary.EffectiveStatus == "Expired" && sampleSummary.OutstandingBalance == 0, "Real SQL payment summary respects the sample's actual deposit deadline");
    }
    actor = customer.UserId;
    var normal = await create.CreateAsync(Selection(false), Guid.NewGuid());
    actor = staff.UserId;
    await realPayments.ConfirmCashDeposit(normal.BookingId, normal.DepositAmount, Guid.NewGuid());
    Check((await realPayments.GetPaymentSummary(normal.BookingId)).NetPaid == 2000000, "Non-demo real cash deposits still use the full deposit amount");
    CurrentUserService.Instance.SetUser(admin);
    var today = AvailabilityRules.ToVietnamLocal(DateTime.UtcNow).Date;
    var report = await new ReportService(Create).GetRevenueReportAsync(today, today);
    var dashboard = await new DashboardService(Create).GetOverviewAsync(today, today);
    Check(report.Rows.All(row => row.BookingId != order.BookingId && row.BookingId != second.BookingId) && report.Rows.Any(row => row.BookingId == normal.BookingId), "Revenue report excludes demo receipts and their refunds, while retaining real receipts");
    using (var db = Create())
    {
        var net = await db.Payments.Where(p => !p.Booking.IsDemo && p.PaymentStatus == "Success" && p.PaidAt >= today.AddHours(-7) && p.PaidAt < today.AddDays(1).AddHours(-7))
            .SumAsync(p => p.PaymentType == "Refund" ? -p.Amount : p.Amount);
        Check(dashboard.NetCashCollected == net, "Admin financial dashboard excludes demo cash flows");
    }
    async Task<string> FinancialSnapshot()
    {
        using var db = Create();
        var bookings = await db.Bookings.AsNoTracking().OrderBy(b => b.BookingId)
            .Select(b => new { b.BookingId, b.BookingCode, b.IsDemo, b.BookingStatus, b.TotalAmount, b.DepositAmount, b.DepositDeadline, b.ConfirmedAt, b.UpdatedAt, b.RowVersion }).ToListAsync();
        var receipts = await db.DemoPayments.AsNoTracking().OrderBy(p => p.DemoPaymentId).ToListAsync();
        var payments = await db.Payments.AsNoTracking().OrderBy(p => p.PaymentId)
            .Select(p => new { p.PaymentId, p.BookingId, p.PaymentType, p.PaymentMethod, p.Amount, p.PaymentStatus, p.PaidAt, p.VerifiedByUserId, p.IdempotencyKey, p.Note }).ToListAsync();
        return System.Text.Json.JsonSerializer.Serialize(new { bookings, receipts, payments });
    }
    var beforeRerun = await FinancialSnapshot();
    foreach (var batch in Regex.Split(upgradeSql, @"(?im)^\s*GO\s*\r?$").Where(b => !string.IsNullOrWhiteSpace(b))) await Exec(connection, batch);
    Check(await FinancialSnapshot() == beforeRerun, "Re-running full CRMS.sql preserves existing booking states, QR receipts, real payments and refunds");
    foreach (var batch in Regex.Split(sql, @"(?im)^\s*GO\s*\r?$").Where(b => !string.IsNullOrWhiteSpace(b))) await Exec(connection, batch);
    Check(await FinancialSnapshot() == beforeRerun, "Re-running the default sample-enabled SQL creates no duplicate bookings or payments and preserves verified QR receipts");
    Console.WriteLine($"{passed} demo checks passed, including real SQL transactions.");
}
finally
{
    SqlConnection.ClearAllPools();
    if (created) await Exec(master, $"ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{dbName}]");
}

sealed class FakeDemo(DemoPaymentSession session) : IDemoPaymentService
{
    public DemoPaymentSession Session = session;
    public bool Fail;
    public Task<DemoPaymentSession> GetSessionAsync(long bookingId, CancellationToken ct = default)
        => Fail ? Task.FromException<DemoPaymentSession>(new InvalidOperationException("Test connection error")) : Task.FromResult(Session);
    public Task<DemoPaymentReceipt> ConfirmReceivedAsync(long bookingId, decimal amount, string reference, Guid key) => throw new NotSupportedException();
}

sealed class FixedCustomerSession(long id) : IUserSession
{
    public long? UserId => id;
    public string Role => "Customer";
    public long SessionVersion => 0;
}
