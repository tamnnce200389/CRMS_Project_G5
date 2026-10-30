using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using CRMS.Data;
using CRMS.Models;
using CRMS.Services.Payments;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

if (args.Length >= 2 && args[0] == "--render") { RenderChecks.Run(args[1]); return; }
var passed = 0;
void Assert(bool ok, string name)
{
    if (!ok) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); passed++;
}
void Reject(Action action, string name)
{
    try { action(); } catch (InvalidOperationException) { Assert(true, name); return; }
    throw new Exception("FAIL (accepted): " + name);
}
async Task RejectAsync(Func<Task> action, string name)
{
    try { await action(); } catch (InvalidOperationException) { Assert(true, name); return; }
    throw new Exception("FAIL (accepted): " + name);
}
var pureBooking = new Booking { BookingId=1, BookingCode="TEST", BookingStatus="Returned", TotalAmount=900000,
    DiscountAmount=100000, DepositAmount=450000, DepositMethod="Cash", DepositDeadline=DateTime.UtcNow.AddHours(1) };
var ledger = new List<Payment> {
    new() {BookingId=1, PaymentType="Deposit", PaymentStatus="Success", Amount=450000},
    new() {BookingId=1, PaymentType="Balance", PaymentStatus="Failed", Amount=999999},
    new() {BookingId=1, PaymentType="Refund", PaymentStatus="Pending", Amount=100000},
    new() {BookingId=2, PaymentType="Deposit", PaymentStatus="Success", Amount=999999}
};
var totals = PaymentRules.Summarize(pureBooking,ledger,new VehicleReturn { LateFee=50000, DamageFee=20000 });
Assert(totals.OutstandingBalance==520000 && totals.RentalTotal==900000,"Discount not subtracted twice; failed/pending/other booking payments excluded");
ledger.Add(new() { BookingId=1, PaymentType="Refund", PaymentStatus="Success", Amount=50000 });
Assert(PaymentRules.Summarize(pureBooking,ledger,null).NetPaid==400000,"Successful refund reduces net paid");
Reject(()=>PaymentRules.ValidateFinalPayment(totals,519999),"Reject stale/wrong final amount");
Reject(()=>PaymentRules.ValidateFinalPayment(totals with { HasReturn=false },520000),"Require return record");
Reject(()=>PaymentRules.ValidateRefund(totals,1,"Cash","APP-1"),"Reject refund on active rental");
pureBooking.BookingStatus="PendingDeposit";
PaymentRules.ValidateCashDeposit(pureBooking,450000,DateTime.UtcNow);
Assert(true,"50 percent of discounted total accepted");
Reject(()=>PaymentRules.ValidateCashDeposit(pureBooking,500000,DateTime.UtcNow),"Reject wrong deposit amount");
Reject(()=>PaymentRules.ValidateCashDeposit(pureBooking,450000,pureBooking.DepositDeadline),"Reject cash deposit exactly at deadline");
Reject(()=>PaymentRules.ValidateAmount(0.001m),"Reject excess monetary precision");

Assert((totals with { Status="Cancelled" }).OutstandingBalance==0,"Cancelled booking has no collectable rental balance");
Assert((totals with { Status="Expired" }).RefundableAmount==450000,"Expired booking exposes net funds for refund");
Reject(()=>PaymentRules.ValidateRefund(totals with { Status="Cancelled" },450001,"Cash","APP"),"Refund cannot exceed available funds");
Reject(()=>PaymentRules.ValidateRefund(totals with { Status="Cancelled" },1,"VNPay","APP"),"Manual refund excludes unsupported method");
Reject(()=>PaymentRules.ValidateRefund(totals with { Status="Cancelled" },1,"Cash"," "),"Manual refund requires approval reference");
Assert(!(totals with { Status="Returned", HasReturn=false, BalancesPaid=520000 }).CanComplete,"Missing return prevents completion at zero balance");
Assert((totals with { BalancesPaid=520000 }).CanComplete,"Returned fully paid booking may complete");

if (!args.Contains("--integration")) { Console.WriteLine($"{passed} checks passed. Add --integration <absolute CRMS.sql path> for isolated SQL tests."); return; }
var sourcePath = args.FirstOrDefault(a=>a.EndsWith(".sql",StringComparison.OrdinalIgnoreCase))
    ?? throw new ArgumentException("Supply the path to CRMS.sql.");
var server = Environment.GetEnvironmentVariable("CRMS_TEST_SERVER") ?? "localhost";
var dbName = "CRMS_TV3_Test_" + Guid.NewGuid().ToString("N");
var master = new SqlConnectionStringBuilder { DataSource=server,InitialCatalog="master",IntegratedSecurity=true,TrustServerCertificate=true,ConnectTimeout=10 }.ConnectionString;
var connectionString = new SqlConnectionStringBuilder(master) { InitialCatalog=dbName }.ConnectionString;
var created = false;
var keepDemo = false;
async Task Exec(string connection, string sql)
{
    await using var conn = new SqlConnection(connection); await conn.OpenAsync();
    await using var cmd = new SqlCommand(sql,conn) { CommandTimeout=90 }; await cmd.ExecuteNonQueryAsync();
}
CrmsContext Create() => new(new DbContextOptionsBuilder<CrmsContext>().UseSqlServer(connectionString).Options);
try
{
    // Unique name generated here; never use App.config or an existing application database.
    await Exec(master,$"CREATE DATABASE [{dbName}]"); created=true;
    var schema = await File.ReadAllTextAsync(sourcePath);
    var marker = "USE [CRMS];";
    var index = schema.IndexOf(marker,StringComparison.Ordinal);
    if(index<0) throw new Exception("Cannot locate schema boundary.");
    schema=schema[(index+marker.Length)..];
    if(Regex.IsMatch(schema,@"(?im)^\s*(USE\s|CREATE\s+DATABASE|DROP\s+DATABASE)")) throw new Exception("Unexpected database switching in schema.");
    foreach(var batch in Regex.Split(schema,@"(?im)^\s*GO\s*\r?$").Where(b=>!string.IsNullOrWhiteSpace(b))) await Exec(connectionString,batch);
    long staff, customer, otherCustomer, vehicle;
    using(var db=Create())
    {
        staff=await db.Users.Where(u=>u.UserRole=="Staff").Select(u=>u.UserId).FirstAsync();
        customer=await db.Users.Where(u=>u.UserRole=="Customer").OrderBy(u=>u.UserId).Select(u=>u.UserId).FirstAsync();
        otherCustomer=await db.Users.Where(u=>u.UserRole=="Customer" && u.UserId!=customer).Select(u=>u.UserId).FirstAsync();
        vehicle=await db.Vehicles.Select(v=>v.VehicleId).FirstAsync();
    }
    long? actor=staff;
    var service=new PaymentService(Create,()=>actor);
    async Task<long> Seed(string status="PendingDeposit", bool expired=false, bool returned=false)
    {
        using var db=Create();
        var b=new Booking { BookingCode="TV3-"+Guid.NewGuid().ToString("N")[..20], RequestKey=Guid.NewGuid(),
            CustomerId=customer,VehicleId=vehicle,RentalType="SelfDrive",DepositMethod="Cash",StartAt=DateTime.UtcNow.AddDays(2),
            EndAt=DateTime.UtcNow.AddDays(3),RentalDays=1,VehicleDailyRate=1000000,VehicleAmount=1000000,
            DiscountAmount=100000,TotalAmount=900000,DepositAmount=450000,DepositDeadline=DateTime.UtcNow.AddHours(expired?-1:1),
            BookingStatus=status,CreatedAt=DateTime.UtcNow };
        db.Bookings.Add(b); await db.SaveChangesAsync();
        if(returned)
        {
            db.VehicleReturns.Add(new VehicleReturn {BookingId=b.BookingId,StaffId=staff,ReturnedAt=DateTime.UtcNow,
                OdometerInKm=100,FuelPercentIn=50,LateFee=50000,DamageFee=20000}); await db.SaveChangesAsync();
        }
        return b.BookingId;
    }
    async Task AddSuccess(long id,decimal amount,string type="Deposit")
    {
        using var db=Create(); db.Payments.Add(new Payment {BookingId=id,Amount=amount,PaymentType=type,PaymentMethod="Cash",
            PaymentStatus="Success",CreatedAt=DateTime.UtcNow,PaidAt=DateTime.UtcNow,VerifiedByUserId=staff,IdempotencyKey=Guid.NewGuid()}); await db.SaveChangesAsync();
    }
    if(args.Contains("--demo"))
    {
        var pendingDemo=await Seed();
        var balanceDemo=await Seed("Returned",returned:true); await AddSuccess(balanceDemo,450000);
        var refundDemo=await Seed("Cancelled"); await AddSuccess(refundDemo,450000);
        using var demoDb=Create();
        foreach(var id in new[]{pendingDemo,balanceDemo,refundDemo})
        {
            var b=await demoDb.Bookings.FindAsync(id);
            Console.WriteLine($"DEMO: {b!.BookingCode} | {b.BookingStatus}");
        }
        keepDemo=true;
        Console.WriteLine($"Demo database retained: {dbName}");
        Console.WriteLine("Set Database in your local App.config to this database to try the UI. Restore it afterwards; do not commit local configuration.");
        return;
    }
    var cash=await Seed(); var key=Guid.NewGuid();
    actor=customer;
    await RejectAsync(()=>service.ConfirmCashDeposit(cash,450000,key),"Customer cannot confirm cash");
    await service.GetPaymentSummary(cash); Assert(true,"Customer can read own payment summary");
    actor=otherCustomer; await RejectAsync(()=>service.GetPaymentSummary(cash),"Customer cannot read another customer's payment summary");
    actor=null; await RejectAsync(()=>service.GetPaymentSummary(cash),"Guest cannot read payment summary");
    actor=staff;
    await RejectAsync(()=>service.ConfirmCashDeposit(cash,1,key),"Wrong cash amount rejected with no transaction written");
    var receipt=await service.ConfirmCashDeposit(cash,450000,key);
    var replay=await service.ConfirmCashDeposit(cash,450000,key);
    using(var db=Create()) Assert(replay.AlreadyRecorded && receipt.PaymentId==replay.PaymentId &&
        await db.Payments.CountAsync(p=>p.BookingId==cash)==1 &&
        await db.BookingStatusHistories.CountAsync(h=>h.BookingId==cash && h.NewStatus=="Confirmed")==1,
        "Duplicate cash retry: one payment and one status history");
    await RejectAsync(()=>service.ConfirmCashDeposit(cash,450000,Guid.NewGuid()),"Second request key cannot collect deposit twice");
    await RejectAsync(()=>service.ConfirmCashDeposit(cash,1,key),"Idempotency key cannot be reused with changed amount");
    var expired=await Seed(expired:true);
    await RejectAsync(()=>service.ConfirmCashDeposit(expired,450000,Guid.NewGuid()),"New cash collection on expired reservation rejected");
    // Test the existing procedure's late-payment behavior, independent of VNPay transport.
    long latePayment;
    using(var db=Create())
    {
        var p=new Payment {BookingId=expired,Amount=450000,PaymentType="Deposit",PaymentMethod="Cash",PaymentStatus="Pending",CreatedAt=DateTime.UtcNow};
        db.Payments.Add(p); await db.SaveChangesAsync(); latePayment=p.PaymentId;
        var flag=new SqlParameter("@Flag",SqlDbType.Bit){Direction=ParameterDirection.Output};
        await db.Database.ExecuteSqlRawAsync("EXEC dbo.usp_ConfirmDeposit @PaymentId=@Id,@VerifiedByUserId=@Staff,@NeedsManualRefund=@Flag OUTPUT",
            new SqlParameter("@Id",latePayment),new SqlParameter("@Staff",staff),flag);
        Assert((bool)flag.Value && (await db.Bookings.AsNoTracking().SingleAsync(b=>b.BookingId==expired)).BookingStatus=="Expired",
            "Late receipt is flagged for refund and does not revive booking");
    }
    var returned=await Seed("Returned",returned:true); await AddSuccess(returned,450000);
    await RejectAsync(()=>service.CompletePaidBooking(returned),"Cannot complete while balance outstanding");
    await RejectAsync(()=>service.RecordFinalPayment(returned,450000,Guid.NewGuid()),"Final payment must include return fees");
    var finalKey=Guid.NewGuid(); await service.RecordFinalPayment(returned,520000,finalKey);
    Assert((await service.GetPaymentSummary(returned)).Status=="Completed","Final payment completes returned booking");
    Assert((await service.RecordFinalPayment(returned,520000,finalKey)).AlreadyRecorded,"Final-payment retry creates no duplicate");
    var noReturn=await Seed("Returned"); await AddSuccess(noReturn,900000);
    await RejectAsync(()=>service.CompletePaidBooking(noReturn),"Missing return document blocks completion even when paid");
    var full=await Seed("Returned",returned:true); await AddSuccess(full,970000);
    await service.CompletePaidBooking(full); await service.CompletePaidBooking(full);
    Assert((await service.GetPaymentSummary(full)).Status=="Completed","Already paid booking completes without new payment; retry safe");
    var cancelled=await Seed("Cancelled"); await AddSuccess(cancelled,450000);
    var refundKey=Guid.NewGuid(); await service.RecordManualRefund(cancelled,200000,"Cash","APPROVED-1",refundKey);
    Assert((await service.RecordManualRefund(cancelled,200000,"Cash","APPROVED-1",refundKey)).AlreadyRecorded,"Refund retry is idempotent");
    await RejectAsync(()=>service.RecordManualRefund(cancelled,200000,"Cash","APPROVED-1",Guid.NewGuid()),"Same approval reference cannot refund twice");
    await RejectAsync(()=>service.RecordManualRefund(cancelled,300000,"Cash","APPROVED-2",Guid.NewGuid()),"Refund cannot exceed remaining net paid");
    await RejectAsync(()=>service.RecordManualRefund(cancelled,1,"Cash","",Guid.NewGuid()),"Refund requires reconciliation reference");
    await service.RecordManualRefund(cancelled,250000,"BankTransfer","APPROVED-2",Guid.NewGuid());
    Assert((await service.GetPaymentSummary(cancelled)).NetPaid==0,"Partial refunds reconcile to zero without editing original deposit");
    var concurrent=await Seed(); var sameKey=Guid.NewGuid();
    var concurrentResults=await Task.WhenAll(service.ConfirmCashDeposit(concurrent,450000,sameKey),service.ConfirmCashDeposit(concurrent,450000,sameKey));
    Assert(concurrentResults.Count(r=>!r.AlreadyRecorded)==1,"Concurrent cash requests with same key record once");
    var concurrentRefund=await Seed("Cancelled"); await AddSuccess(concurrentRefund,450000);
    async Task<bool> TryRefund(string reference)
    {
        try { await service.RecordManualRefund(concurrentRefund,300000,"Cash",reference,Guid.NewGuid()); return true; }
        catch(InvalidOperationException){return false;}
    }
    var refunds=await Task.WhenAll(TryRefund("R1"),TryRefund("R2"));
    Assert(refunds.Count(x=>x)==1 && (await service.GetPaymentSummary(concurrentRefund)).NetPaid==150000,"Concurrent refunds cannot over-refund");
    var session=await Seed(); var calls=0;
    var changingSession=new PaymentService(Create,()=>++calls==1?staff:customer);
    await RejectAsync(()=>changingSession.ConfirmCashDeposit(session,450000,Guid.NewGuid()),"Session change before commit rolls back");
    using(var db=Create()) Assert(!await db.Payments.AnyAsync(p=>p.BookingId==session) &&
        (await db.Bookings.FindAsync(session))!.BookingStatus=="PendingDeposit","Session-change rollback leaves booking and payments intact");
    Console.WriteLine($"SUCCESS: {passed} checks passed against isolated database {dbName}.");
}
finally
{
    if(created && !keepDemo)
    {
        SqlConnection.ClearAllPools();
        if(!Regex.IsMatch(dbName,@"^CRMS_TV3_Test_[a-f0-9]{32}$")) throw new Exception("Unsafe cleanup target.");
        await Exec(master,$"ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{dbName}];");
        Console.WriteLine("Removed isolated test database.");
    }
}
