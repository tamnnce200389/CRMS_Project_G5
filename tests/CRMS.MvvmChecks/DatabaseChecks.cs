using System.IO;
using System.Text.RegularExpressions;
using CRMS.Data;
using CRMS.Models;
using CRMS.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// All writes are confined to a new database whose random name is generated here.
// App.config and the application's existing CRMS database are never used.
internal static class DatabaseChecks
{
    internal static async Task Run(string sqlPath)
    {
        var name = "CRMS_MVVM_Test_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = Environment.GetEnvironmentVariable("CRMS_TEST_SERVER") ?? "localhost", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 10 }.ConnectionString;
        var connection = new SqlConnectionStringBuilder(master) { InitialCatalog = name }.ConnectionString;
        async Task Exec(string target, string sql) { await using var conn = new SqlConnection(target); await conn.OpenAsync(); await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 }; await cmd.ExecuteNonQueryAsync(); }
        CrmsContext Create() => new(new DbContextOptionsBuilder<CrmsContext>().UseSqlServer(connection).Options);
        var created = false;
        try
        {
            var schema = await File.ReadAllTextAsync(sqlPath); const string marker = "USE [CRMS];"; var boundary = schema.IndexOf(marker, StringComparison.Ordinal);
            if (boundary < 0) throw new InvalidOperationException("Schema boundary not found."); schema = schema[(boundary + marker.Length)..];
            if (Regex.IsMatch(schema, @"(?im)^\s*(USE\s|CREATE\s+DATABASE|DROP\s+DATABASE)")) throw new InvalidOperationException("Unexpected database switch.");
            schema = schema.Replace("DECLARE @IncludeDemoData bit = 0;", "DECLARE @IncludeDemoData bit = 1;");
            await Exec(master, $"CREATE DATABASE [{name}]"); created = true;
            foreach (var batch in Regex.Split(schema, @"(?im)^\s*GO\s*\r?$").Where(b => !string.IsNullOrWhiteSpace(b))) await Exec(connection, batch);
            long admin, staff, customer, other;
            using (var db = Create())
            {
                admin = await db.Users.Where(u => u.IsActive && u.UserRole == "Admin").Select(u => u.UserId).FirstAsync();
                staff = await db.Users.Where(u => u.IsActive && u.UserRole == "Staff").Select(u => u.UserId).FirstAsync();
                customer = await db.Users.Where(u => u.IsActive && u.UserRole == "Customer").Select(u => u.UserId).FirstAsync();
                other = await db.Users.Where(u => u.IsActive && u.UserRole == "Customer" && u.UserId != customer).Select(u => u.UserId).FirstAsync();
            }
            var session = new TestSession(admin, "Admin"); var accounts = new UserAccountService(Create, session); var drivers = new DriverService(Create, session); var query = new BookingQueryService(Create, session);
            var all = await accounts.GetUsersAsync(); var target = all.Single(u => u.UserId == customer);
            await Program.Reject(() => accounts.SetActiveAsync(all.Single(u => u.UserId == admin), false), "SQL: Admin cannot disable self");
            await Program.Reject(() => accounts.ChangeRoleAsync(target, "Owner"), "SQL: Invalid role rejected");
            await accounts.ChangeRoleAsync(target, "Staff"); using (var db = Create()) Program.Check((await db.Users.FindAsync(customer))?.UserRole == "Staff", "SQL: Authorized role update persisted");
            await Program.Reject(() => accounts.SetActiveAsync(target, false), "SQL: Stale row version cannot overwrite account");
            target = (await accounts.GetUsersAsync()).Single(u => u.UserId == customer); await accounts.ChangeRoleAsync(target, "Customer");
            session.Change(staff, "Admin"); await Program.Reject(() => accounts.GetUsersAsync(), "SQL: Fresh database role overrides forged session role");
            var list = await drivers.GetAllAsync(); Program.Check(list.Count > 0, "SQL: Staff may read drivers");
            await Program.Reject(() => drivers.AddAsync(new() { FullName = "Không được ghi" }), "SQL: Staff cannot write drivers even with forged session role");
            session.Change(admin, "Admin"); var driver = new Driver { FullName = "Tài xế MVVM", LicenseNumber = "MVVM-" + Guid.NewGuid().ToString("N")[..12], IsActive = true }; await drivers.AddAsync(driver);
            var original = (await drivers.GetAllAsync()).Single(d => d.DriverId == driver.DriverId);
            var edited = new Driver { DriverId = driver.DriverId, FullName = "Tài xế cập nhật", LicenseNumber = original.LicenseNumber, IsActive = true }; await drivers.UpdateAsync(edited, original);
            await Program.Reject(() => drivers.UpdateAsync(edited, original), "SQL: Concurrent driver edits rejected");
            await Program.Reject(() => drivers.AddAsync(new() { FullName = "Trùng bằng lái", LicenseNumber = original.LicenseNumber }), "SQL: Duplicate license rejected");
            Program.Check(await drivers.SetActiveAsync(driver.DriverId, false, true), "SQL: Admin can disable driver");
            await Program.Reject(() => drivers.SetActiveAsync(driver.DriverId, true, true), "SQL: Stale driver status rejected");
            session.Change(customer, "Customer"); var own = await accounts.GetProfileAsync(); var saved = await accounts.SaveProfileAsync(new("Hồ sơ kiểm thử", own.Phone, own.RowVersion)); Program.Check(saved.FullName == "Hồ sơ kiểm thử", "SQL: Profile saves with concurrency token");
            await Program.Reject(() => accounts.SaveProfileAsync(new("Hồ sơ cũ", own.Phone, own.RowVersion)), "SQL: Stale profile rejected");
            await Program.Reject(() => drivers.GetAllAsync(), "SQL: Customer cannot read driver administration");
            var trips = await query.GetMyBookingsAsync(); Program.Check(trips.Count > 0, "SQL: Customer bookings projected to display DTO");
            var code = trips[0].BookingCode; Program.Check(await query.ResolveBookingAsync(code) > 0, "SQL: Customer can resolve own booking code");
            session.Change(other, "Customer"); await Program.Reject(() => query.ResolveBookingAsync(code), "SQL: Customer cannot resolve another customer's booking");
            session.Change(staff, "Staff"); Program.Check(await query.ResolveBookingAsync(code) > 0, "SQL: Staff can resolve payment lookup");
            await Program.Reject(() => query.GetMyBookingsAsync(), "SQL: Staff cannot impersonate customer booking list");
            using (var db = Create()) { var user = await db.Users.FindAsync(customer); user!.IsActive = false; await db.SaveChangesAsync(); }
            session.Change(customer, "Customer"); await Program.Reject(() => accounts.GetProfileAsync(), "SQL: Disabled session cannot access profile");
        }
        finally
        {
            if (created)
            {
                SqlConnection.ClearAllPools();
                await Exec(master, $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];");
                Console.WriteLine("Removed isolated test database: " + name);
            }
        }
    }
}
