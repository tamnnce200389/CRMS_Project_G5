using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CRMS.Data;
using CRMS.Helpers;
using CRMS.Models;
using CRMS.Services.Vehicles;
using CRMS.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

if (args.Length >= 2 && args[0] == "--render")
{
    RenderChecks.Run(args[1]);
    return;
}

var passed = 0;

void Assert(bool ok, string name)
{
    if (!ok) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}

void Reject(Action action, string name)
{
    try
    {
        action();
    }
    catch (InvalidOperationException)
    {
        Assert(true, name);
        return;
    }
    throw new Exception("FAIL (accepted): " + name);
}

async Task RejectAsync(Func<Task> action, string name)
{
    try
    {
        await action();
    }
    catch (InvalidOperationException)
    {
        Assert(true, name);
        return;
    }
    throw new Exception("FAIL (accepted): " + name);
}

// =========================================================
// 1. VIETNAMESE LABEL CONVERTER CHECKS
// =========================================================
var labelConverter = new VietnameseLabelConverter();
Assert((string)labelConverter.Convert("Suspended", typeof(string), null!, CultureInfo.InvariantCulture) == "Tạm ngưng",
    "Converter maps Suspended to Vietnamese label");
Assert((string)labelConverter.Convert("Gasoline", typeof(string), null!, CultureInfo.InvariantCulture) == "Xăng" &&
       (string)labelConverter.Convert("Diesel", typeof(string), null!, CultureInfo.InvariantCulture) == "Dầu Diesel" &&
       (string)labelConverter.Convert("Electric", typeof(string), null!, CultureInfo.InvariantCulture) == "Điện" &&
       (string)labelConverter.Convert("Hybrid", typeof(string), null!, CultureInfo.InvariantCulture) == "Hybrid",
    "Converter maps all CK_Vehicles_Fuel values");

// =========================================================
// 2. PURE VEHICLE RULES CHECKS (NO DATABASE CONNECTION)
// =========================================================
Assert(VehicleRules.NormalizeLicensePlate("  65a-123.45 ") == "65A-123.45",
    "License plate trimmed and uppercased");
Reject(() => VehicleRules.NormalizeLicensePlate("  "), "Reject empty license plate");
Reject(() => VehicleRules.NormalizeLicensePlate("AB"), "Reject license plate shorter than 3 chars");
Reject(() => VehicleRules.NormalizeLicensePlate(new string('A', 21)), "Reject license plate longer than 20 chars");
Reject(() => VehicleRules.NormalizeLicensePlate("65A-12345@"), "Reject license plate with special symbols");

var validReq = new VehicleUpsertRequest(
    VehicleId: null,
    CategoryId: 1,
    LicensePlate: " 51g-888.99 ",
    Brand: " Toyota ",
    Model: " Camry ",
    ManufactureYear: 2024,
    SeatCount: 5,
    Transmission: "Automatic",
    FuelType: "Hybrid",
    DailyRate: 1500000m,
    OdometerKm: 12000,
    OperationalStatus: "Active",
    Description: " Xe sedan hang D ",
    Images: new[]
    {
        new VehicleImageInput("https://placehold.co/960x540/png?text=Side", IsPrimary: false, SortOrder: 5),
        new VehicleImageInput("https://placehold.co/960x540/png?text=Front", IsPrimary: false, SortOrder: 1)
    });

var normalizedReq = VehicleRules.ValidateVehicle(validReq);
Assert(normalizedReq.LicensePlate == "51G-888.99" &&
       normalizedReq.Brand == "Toyota" &&
       normalizedReq.Model == "Camry" &&
       normalizedReq.Images!.Count == 2 &&
       normalizedReq.Images[0].IsPrimary &&
       !normalizedReq.Images[1].IsPrimary &&
       normalizedReq.Images[0].SortOrder == 0 &&
       normalizedReq.Images[1].SortOrder == 1,
    "Valid vehicle normalized and promotes first image when none marked primary");

Reject(() => VehicleRules.ValidateVehicle(validReq with { CategoryId = 0 }), "Reject invalid CategoryId");
Reject(() => VehicleRules.ValidateVehicle(validReq with { Brand = " " }), "Reject empty Brand");
Reject(() => VehicleRules.ValidateVehicle(validReq with { Model = " " }), "Reject empty Model");
Reject(() => VehicleRules.ValidateVehicle(validReq with { ManufactureYear = 1989 }), "Reject ManufactureYear < 1990");
Reject(() => VehicleRules.ValidateVehicle(validReq with { ManufactureYear = 2101 }), "Reject ManufactureYear > 2100");
Reject(() => VehicleRules.ValidateVehicle(validReq with { SeatCount = 0 }), "Reject SeatCount < 1");
Reject(() => VehicleRules.ValidateVehicle(validReq with { SeatCount = 31 }), "Reject SeatCount > 30");
Reject(() => VehicleRules.ValidateVehicle(validReq with { Transmission = "CVT" }), "Reject unsupported Transmission");
Reject(() => VehicleRules.ValidateVehicle(validReq with { FuelType = "Hydrogen" }), "Reject unsupported FuelType");
Reject(() => VehicleRules.ValidateVehicle(validReq with { DailyRate = 0m }), "Reject zero DailyRate");
Reject(() => VehicleRules.ValidateVehicle(validReq with { DailyRate = -500000m }), "Reject negative DailyRate");
Reject(() => VehicleRules.ValidateVehicle(validReq with { DailyRate = 500000.125m }), "Reject DailyRate with > 2 decimals");
Reject(() => VehicleRules.ValidateVehicle(validReq with { OdometerKm = -1 }), "Reject negative OdometerKm");
Reject(() => VehicleRules.ValidateVehicle(validReq with { OdometerKm = 11999 }, currentOdometerKm: 12000),
    "Reject decreasing OdometerKm on edit");
Reject(() => VehicleRules.ValidateVehicle(validReq with { OperationalStatus = "Maintenance" }),
    "Reject OperationalStatus outside CK_Vehicles_Status");

// Image normalization edge cases
var tempImg = Path.Combine(Path.GetTempPath(), $"crms_f06_{Guid.NewGuid():N}.png");
try
{
    await File.WriteAllBytesAsync(tempImg, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
    var multiPrimary = VehicleRules.NormalizeImages(new[]
    {
        new VehicleImageInput(tempImg, IsPrimary: true, SortOrder: 10),
        new VehicleImageInput("https://example.com/a.jpg", IsPrimary: true, SortOrder: 20),
        new VehicleImageInput("https://example.com/a.jpg", IsPrimary: false, SortOrder: 30)
    });
    Assert(multiPrimary.Count == 2 &&
           multiPrimary[0].ImageUrl.StartsWith("file:///", StringComparison.OrdinalIgnoreCase) &&
           multiPrimary[0].IsPrimary &&
           !multiPrimary[1].IsPrimary &&
           multiPrimary[0].SortOrder == 0 &&
           multiPrimary[1].SortOrder == 1,
        "NormalizeImages converts local file to file:/// URI, deduplicates URLs, and enforces single primary");
}
finally
{
    if (File.Exists(tempImg)) File.Delete(tempImg);
}

Reject(() => VehicleRules.NormalizeImages(new[] { new VehicleImageInput("ftp://example.com/car.png") }),
    "Reject non-http/https/file image scheme");
Reject(() => VehicleRules.NormalizeImages(new[] { new VehicleImageInput("relative/nonexistent.png") }),
    "Reject relative or missing local image path");

var validCat = VehicleRules.ValidateCategory(new CategoryUpsertRequest(null, "  Coupe  ", "  Xe 2 cua  ", true));
Assert(validCat.CategoryName == "Coupe" && validCat.Description == "Xe 2 cua", "ValidateCategory trims fields");
Reject(() => VehicleRules.ValidateCategory(new CategoryUpsertRequest(null, "   ", null, true)),
    "Reject empty CategoryName");
Reject(() => VehicleRules.ValidateCategory(new CategoryUpsertRequest(null, new string('X', 81), null, true)),
    "Reject CategoryName > 80 chars");

// =========================================================
// 3. VIEWMODEL MVVM UNIT CHECKS (IN-MEMORY FAKE SERVICE)
// =========================================================
var fakeService = new FakeVehicleService();
var listVm = new VehicleViewModel(fakeService);
await listVm.LoadVehiclesAsync();
Assert(listVm.TotalVehicles == 2 && listVm.ActiveCount == 1 && listVm.SuspendedCount == 1,
    "VehicleViewModel loads items and computes KPI counts");
listVm.SearchText = "Everest";
Assert(listVm.FilteredCount == 1, "VehicleViewModel filters by keyword");
listVm.SearchText = "";
listVm.SelectedStatus = "Suspended";
Assert(listVm.FilteredCount == 1, "VehicleViewModel filters by OperationalStatus compatible with StaffVehiclePage");

var editVm = new VehicleEditViewModel(fakeService);
await editVm.InitializeForCreateAsync();
editVm.AddUrlInput = "https://placehold.co/960x540/png?text=One";
editVm.AddImageUrlCommand.Execute(null);
editVm.AddUrlInput = "https://placehold.co/960x540/png?text=Two";
editVm.AddImageUrlCommand.Execute(null);
Assert(editVm.Images.Count == 2 && editVm.Images[0].IsPrimary && !editVm.Images[1].IsPrimary,
    "VehicleEditViewModel manages primary flag when adding images");
editVm.SetPrimaryImageCommand.Execute(editVm.Images[1]);
Assert(!editVm.Images[0].IsPrimary && editVm.Images[1].IsPrimary,
    "VehicleEditViewModel switches primary image");

if (!args.Contains("--integration"))
{
    Console.WriteLine($"{passed} checks passed. Add --integration <absolute CRMS.sql path> for isolated SQL tests.");
    return;
}

// =========================================================
// 4. ISOLATED SQL SERVER INTEGRATION TESTS (TEMP DATABASE)
// =========================================================
var sourcePath = args.FirstOrDefault(a => a.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
    ?? throw new ArgumentException("Supply the path to CRMS.sql.");
var server = Environment.GetEnvironmentVariable("CRMS_TEST_SERVER") ?? "localhost";
var dbName = "CRMS_TV2_Test_" + Guid.NewGuid().ToString("N");
var master = new SqlConnectionStringBuilder
{
    DataSource = server,
    InitialCatalog = "master",
    IntegratedSecurity = true,
    TrustServerCertificate = true,
    ConnectTimeout = 10
}.ConnectionString;
var connectionString = new SqlConnectionStringBuilder(master) { InitialCatalog = dbName }.ConnectionString;
var created = false;

async Task Exec(string connection, string sql)
{
    await using var conn = new SqlConnection(connection);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 90 };
    await cmd.ExecuteNonQueryAsync();
}

CrmsContext Create() => new(new DbContextOptionsBuilder<CrmsContext>().UseSqlServer(connectionString).Options);

try
{
    await Exec(master, $"CREATE DATABASE [{dbName}]");
    created = true;
    var schema = await File.ReadAllTextAsync(sourcePath);
    var marker = "USE [CRMS];";
    var index = schema.IndexOf(marker, StringComparison.Ordinal);
    if (index < 0) throw new Exception("Cannot locate schema boundary.");
    schema = schema[(index + marker.Length)..];
    if (Regex.IsMatch(schema, @"(?im)^\s*(USE\s|CREATE\s+DATABASE|DROP\s+DATABASE)"))
        throw new Exception("Unexpected database switching in schema.");
    foreach (var batch in Regex.Split(schema, @"(?im)^\s*GO\s*\r?$").Where(b => !string.IsNullOrWhiteSpace(b)))
        await Exec(connectionString, batch);

    long adminId, staffId, customerId;
    using (var db = Create())
    {
        adminId = await db.Users.Where(u => u.UserRole == "Admin" && u.IsActive).Select(u => u.UserId).FirstAsync();
        staffId = await db.Users.Where(u => u.UserRole == "Staff" && u.IsActive).Select(u => u.UserId).FirstAsync();
        customerId = await db.Users.Where(u => u.UserRole == "Customer" && u.IsActive).Select(u => u.UserId).FirstAsync();

        // Verify 1:N VehicleImages EF mapping loads seeded 2-image vehicles cleanly
        var vWithImgs = await db.Vehicles.Include(v => v.VehicleImages).FirstAsync(v => v.LicensePlate == "65A-12345");
        Assert(vWithImgs.VehicleImages.Count == 2, "EF Core 1:N VehicleImages loads multiple seeded images per vehicle");
    }

    long? actor = null;
    var service = new VehicleService(Create, () => actor);

    // 4.1 Role enforcement (Guest / Customer / Staff rejected)
    var catCreate = new CategoryUpsertRequest(null, "Minivan Luxury", "Xe gia dinh cao cap", true);
    await RejectAsync(() => service.SaveCategoryAsync(catCreate), "Guest cannot create category");
    actor = customerId;
    await RejectAsync(() => service.SaveCategoryAsync(catCreate), "Customer cannot create category");
    actor = staffId;
    await RejectAsync(() => service.SaveCategoryAsync(catCreate), "Staff cannot create category");

    // 4.2 Admin Category CRUD & duplicate name protection
    actor = adminId;
    var newCatId = await service.SaveCategoryAsync(catCreate);
    Assert(newCatId > 0, "Admin creates new VehicleCategory");
    await RejectAsync(() => service.SaveCategoryAsync(new CategoryUpsertRequest(null, "minivan luxury", "Trung ten", true)),
        "Duplicate CategoryName rejected");

    // 4.3 Admin Vehicle Create + Multi-Image + Auto Long-Term Rates Sync
    var createVehicleReq = new VehicleUpsertRequest(
        VehicleId: null,
        CategoryId: newCatId,
        LicensePlate: "65a-999.88",
        Brand: "Kia",
        Model: "Carnival Signature",
        ManufactureYear: 2025,
        SeatCount: 7,
        Transmission: "Automatic",
        FuelType: "Diesel",
        DailyRate: 2000000m,
        OdometerKm: 500,
        OperationalStatus: "Active",
        Description: "Xe test F06",
        Images: new[]
        {
            new VehicleImageInput("https://placehold.co/960x540/png?text=Carnival+1", IsPrimary: false, SortOrder: 0),
            new VehicleImageInput("https://placehold.co/960x540/png?text=Carnival+2", IsPrimary: true, SortOrder: 1),
            new VehicleImageInput("https://placehold.co/960x540/png?text=Carnival+3", IsPrimary: false, SortOrder: 2)
        });

    actor = staffId;
    await RejectAsync(() => service.SaveVehicleAsync(createVehicleReq), "Staff cannot create vehicle");

    actor = adminId;
    var newVehicleId = await service.SaveVehicleAsync(createVehicleReq);
    var details = await service.GetVehicleDetailsAsync(newVehicleId);
    Assert(details != null &&
           details.LicensePlate == "65A-999.88" &&
           details.Images.Count == 3 &&
           details.Images.Count(i => i.IsPrimary) == 1 &&
           details.Images.Single(i => i.IsPrimary).ImageUrl.EndsWith("Carnival+2", StringComparison.Ordinal),
        "Vehicle created with 3 images and exact 1 primary image");

    await using (var conn = new SqlConnection(connectionString))
    {
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(
            "SELECT COUNT(*), MIN(BaseMonthlyRate), MAX(BaseMonthlyRate) FROM dbo.VehicleLongTermRates WHERE VehicleId = @id", conn);
        cmd.Parameters.AddWithValue("@id", newVehicleId);
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        Assert(reader.GetInt32(0) == 5 && reader.GetDecimal(1) == 60000000m && reader.GetDecimal(2) == 60000000m,
            "5 VehicleLongTermRates packages synced automatically on vehicle creation");
    }

    // 4.4 Duplicate license plate protection
    await RejectAsync(() => service.SaveVehicleAsync(createVehicleReq with { VehicleId = null }),
        "Duplicate license plate rejected on create");
    await RejectAsync(() => service.SaveVehicleAsync(createVehicleReq with { VehicleId = newVehicleId, LicensePlate = "65A-12345" }),
        "Duplicate license plate with another existing vehicle rejected on edit");

    // 4.5 Edit vehicle: Odometer non-decrease, switch Primary image, update DailyRate -> sync LongTermRates
    await RejectAsync(() => service.SaveVehicleAsync(createVehicleReq with { VehicleId = newVehicleId, OdometerKm = 499 }),
        "Decreasing OdometerKm rejected in DB update");

    await service.SaveVehicleAsync(createVehicleReq with
    {
        VehicleId = newVehicleId,
        DailyRate = 2200000m,
        OdometerKm = 850,
        Images = new[]
        {
            new VehicleImageInput("https://placehold.co/960x540/png?text=Carnival+3", IsPrimary: true, SortOrder: 0),
            new VehicleImageInput("https://placehold.co/960x540/png?text=Carnival+4", IsPrimary: false, SortOrder: 1)
        }
    });

    var updatedDetails = await service.GetVehicleDetailsAsync(newVehicleId);
    Assert(updatedDetails != null &&
           updatedDetails.DailyRate == 2200000m &&
           updatedDetails.OdometerKm == 850 &&
           updatedDetails.Images.Count == 2 &&
           updatedDetails.Images.Single(i => i.IsPrimary).ImageUrl.EndsWith("Carnival+3", StringComparison.Ordinal),
        "Primary image switched cleanly without UX_Images_OnePrimary violation");

    await using (var conn = new SqlConnection(connectionString))
    {
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(
            "SELECT MIN(BaseMonthlyRate) FROM dbo.VehicleLongTermRates WHERE VehicleId = @id", conn);
        cmd.Parameters.AddWithValue("@id", newVehicleId);
        var rate = (decimal)(await cmd.ExecuteScalarAsync())!;
        Assert(rate == 66000000m, "VehicleLongTermRates BaseMonthlyRate updated when DailyRate changes");
    }

    // 4.6 Active-vehicle guard on Category deactivation
    await RejectAsync(() => service.SetCategoryActiveAsync(newCatId, false),
        "Cannot deactivate category while it still has Active vehicles");

    // 4.7 Active-booking guard on Vehicle suspension
    long bookingId;
    using (var db = Create())
    {
        var b = new Booking
        {
            BookingCode = "TV2-" + Guid.NewGuid().ToString("N")[..16],
            RequestKey = Guid.NewGuid(),
            CustomerId = customerId,
            VehicleId = newVehicleId,
            RentalType = "SelfDrive",
            DepositMethod = "Cash",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(2),
            RentalDays = 1,
            VehicleDailyRate = 2200000m,
            VehicleAmount = 2200000m,
            DiscountAmount = 0m,
            TotalAmount = 2200000m,
            DepositAmount = 1100000m,
            DepositDeadline = DateTime.UtcNow.AddHours(2),
            BookingStatus = "PendingDeposit",
            CreatedAt = DateTime.UtcNow
        };
        db.Bookings.Add(b);
        await db.SaveChangesAsync();
        bookingId = b.BookingId;
    }

    await RejectAsync(() => service.SetVehicleStatusAsync(newVehicleId, "Suspended"),
        "Cannot suspend vehicle with unexpired PendingDeposit booking");

    using (var db = Create())
    {
        var b = await db.Bookings.FindAsync(bookingId);
        b!.BookingStatus = "Confirmed";
        await db.SaveChangesAsync();
    }
    await RejectAsync(() => service.SetVehicleStatusAsync(newVehicleId, "Suspended"),
        "Cannot suspend vehicle with Confirmed booking");

    using (var db = Create())
    {
        var b = await db.Bookings.FindAsync(bookingId);
        b!.BookingStatus = "InUse";
        await db.SaveChangesAsync();
    }
    await RejectAsync(() => service.SetVehicleStatusAsync(newVehicleId, "Suspended"),
        "Cannot suspend vehicle with InUse booking");

    using (var db = Create())
    {
        var b = await db.Bookings.FindAsync(bookingId);
        b!.BookingStatus = "Completed";
        await db.SaveChangesAsync();
    }
    await service.SetVehicleStatusAsync(newVehicleId, "Suspended");
    Assert((await service.GetVehicleDetailsAsync(newVehicleId))!.OperationalStatus == "Suspended",
        "Vehicle suspended once bookings are Completed/Cancelled/Expired");

    // 4.8 Category deactivation succeeds once no Active vehicles remain, and blocks reactivating vehicle
    await service.SetCategoryActiveAsync(newCatId, false);
    await RejectAsync(() => service.SetVehicleStatusAsync(newVehicleId, "Active"),
        "Cannot reactivate vehicle while its category is inactive");
    await service.SetCategoryActiveAsync(newCatId, true);
    await service.SetVehicleStatusAsync(newVehicleId, "Active");
    Assert((await service.GetVehicleDetailsAsync(newVehicleId))!.OperationalStatus == "Active",
        "Vehicle reactivated after category is active again");

    // 4.9 Filter contract for TV1
    var filtered = await service.GetVehiclesAsync(new VehicleFilter(
        Keyword: "Carnival",
        CategoryId: newCatId,
        OperationalStatus: "Active",
        MinDailyRate: 2000000m,
        MaxDailyRate: 2500000m,
        OnlyActiveCategories: true));
    Assert(filtered.Count == 1 && filtered[0].VehicleId == newVehicleId,
        "GetVehiclesAsync filter contract returns expected vehicle for TV1");

    Console.WriteLine($"ALL {passed} CHECKS PASSED (unit + SQL integration).");
}
finally
{
    if (created)
    {
        SqlConnection.ClearAllPools();
        await Exec(master, $"IF DB_ID(N'{dbName}') IS NOT NULL BEGIN ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{dbName}]; END");
    }
}

sealed class FakeVehicleService : IVehicleService
{
    private readonly List<CategoryListItem> _categories = new()
    {
        new CategoryListItem { CategoryId = 1, CategoryName = "SUV", IsActive = true, TotalVehicles = 2, ActiveVehicles = 1 }
    };

    private readonly List<VehicleListItem> _vehicles = new()
    {
        new VehicleListItem
        {
            VehicleId = 1, CategoryId = 1, CategoryName = "SUV", LicensePlate = "65A-67890",
            Brand = "Ford", Model = "Everest", ManufactureYear = 2024, SeatCount = 7,
            Transmission = "Automatic", FuelType = "Diesel", DailyRate = 1550000m,
            OdometerKm = 14000, OperationalStatus = "Active", ImageCount = 2
        },
        new VehicleListItem
        {
            VehicleId = 2, CategoryId = 1, CategoryName = "SUV", LicensePlate = "DEMO-CRMS-026",
            Brand = "Toyota", Model = "Camry Hybrid", ManufactureYear = 2024, SeatCount = 5,
            Transmission = "Automatic", FuelType = "Hybrid", DailyRate = 1750000m,
            OdometerKm = 11500, OperationalStatus = "Suspended", ImageCount = 0
        }
    };

    public Task<List<VehicleListItem>> GetVehiclesAsync(VehicleFilter? filter = null) =>
        Task.FromResult(_vehicles.ToList());

    public Task<VehicleDetailsDto?> GetVehicleDetailsAsync(long vehicleId) =>
        Task.FromResult<VehicleDetailsDto?>(null);

    public Task<long> SaveVehicleAsync(VehicleUpsertRequest request) => Task.FromResult(1L);
    public Task SetVehicleStatusAsync(long vehicleId, string operationalStatus) => Task.CompletedTask;
    public Task<List<CategoryListItem>> GetCategoriesAsync(bool includeInactive = true) =>
        Task.FromResult(_categories.ToList());
    public Task<int> SaveCategoryAsync(CategoryUpsertRequest request) => Task.FromResult(1);
    public Task SetCategoryActiveAsync(int categoryId, bool isActive) => Task.CompletedTask;
}
