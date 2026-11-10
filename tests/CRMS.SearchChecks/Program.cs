using System.IO;
using System.Text.RegularExpressions;
using CRMS.Data;
using CRMS.Models;
using CRMS.Services;
using CRMS.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}
void Reject(CarSearchFilter filter, string name)
{
    try { filter.Validate(); }
    catch (ArgumentException) { Check(true, name); return; }
    throw new Exception("FAIL accepted: " + name);
}

var region = new SearchCity { CityId = 1, CityName = "Cần Thơ", IsActive = true };
var otherRegion = new SearchCity { CityId = 2, CityName = "TP. Hồ Chí Minh", IsActive = true };
var source = new CaptureSearch();
var vm = new CarSearchViewModel(source);
vm.Cities.Add(region); vm.Cities.Add(otherRegion);
vm.SelectedCity = region;
vm.SelectedBrand = "Toyota";
await vm.ToggleDeliveryAsync();
Check(vm.DeliveryOnly && source.Last is { DeliveryOnly: true, CityId: 1, Latitude: null, Longitude: null }, "One click filters the initial city without selecting an address");
Check(vm.SelectedDeliveryLocation == null && vm.AppliedCriteria.Any(c => c.Label == "Giao nhận tận nơi: Cần Thơ"), "Applied chip names the initial city rather than a sample address");
await vm.ToggleDeliveryAsync();
Check(!vm.DeliveryOnly && source.Last is { DeliveryOnly: false, CityId: 1, Brand: "Toyota" }, "Second click removes delivery while preserving region and other criteria");
vm.SelectedDeliveryLocation = new RentalLocation { LocationId = 9, CityId = 2, DisplayName = "Old address", Latitude = 10.8m, Longitude = 106.7m };
vm.SelectedCity = region;
await vm.ToggleDeliveryAsync();
Check(source.Last is { CityId: 1, Latitude: null, Longitude: null } && vm.SelectedCity == region, "Stale address coordinates cannot override the current region or quote a delivery fee");
vm.SelectedCity = otherRegion;
await vm.SearchAsync();
Check(source.Last?.CityId == 2 && vm.AppliedCriteria.Any(c => c.Label.EndsWith("TP. Hồ Chí Minh")), "Changing the search region updates delivery coverage and the applied chip");
vm.AppliedCriteria.Single(c => c.Label.StartsWith("Giao nhận tận nơi:")).Remove!();
await vm.SearchAsync();
Check(!vm.DeliveryOnly && vm.SelectedCity == otherRegion && vm.SelectedBrand == "Toyota", "Removing the delivery chip keeps the search context");
vm.SelectedCity = new SearchCity { CityId = 0, CityName = "Tất cả địa điểm" };
var calls = source.Calls;
await vm.ToggleDeliveryAsync();
Check(!vm.DeliveryOnly && source.Calls == calls && vm.StatusMessage.Contains("Chọn khu vực"), "Missing region does not silently search all cities or open an address picker");
new CarSearchFilter { DeliveryOnly = true, CityId = 1 }.Validate();
Check(true, "Regional delivery accepts a city without coordinates");
Reject(new() { DeliveryOnly = true }, "Regional delivery rejects a missing city");
Reject(new() { DeliveryOnly = true, CityId = 0 }, "Regional delivery rejects the all-regions placeholder");
Reject(new() { FreeDeliveryOnly = true, CityId = 1 }, "Address-specific free delivery still requires valid coordinates");

if (!args.Contains("--integration"))
{
    Console.WriteLine($"{passed} checks passed. Add --integration <absolute CRMS.sql path> for isolated SQL checks.");
    return;
}

var schemaPath = args.FirstOrDefault(a => a.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
    ?? throw new ArgumentException("Pass the absolute CRMS.sql path.");
var databaseName = "CRMS_Search_Test_" + Guid.NewGuid().ToString("N");
var master = new SqlConnectionStringBuilder
{
    DataSource = Environment.GetEnvironmentVariable("CRMS_TEST_SERVER") ?? "localhost",
    InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 10
}.ConnectionString;
var connection = new SqlConnectionStringBuilder(master) { InitialCatalog = databaseName }.ConnectionString;
CrmsContext Create() => new(new DbContextOptionsBuilder<CrmsContext>().UseSqlServer(connection).Options);
async Task Exec(string connectionString, string sql)
{
    await using var db = new SqlConnection(connectionString); await db.OpenAsync();
    await using var cmd = new SqlCommand(sql, db) { CommandTimeout = 90 };
    await cmd.ExecuteNonQueryAsync();
}
bool created = false;
try
{
    await Exec(master, $"CREATE DATABASE [{databaseName}]"); created = true;
    var sql = await File.ReadAllTextAsync(schemaPath);
    const string marker = "USE [CRMS];";
    var index = sql.IndexOf(marker, StringComparison.Ordinal);
    if (index < 0) throw new Exception("Missing schema boundary.");
    sql = sql[(index + marker.Length)..].Replace("DECLARE @IncludeDemoData bit = 0;", "DECLARE @IncludeDemoData bit = 1;");
    if (Regex.IsMatch(sql, @"(?im)^\s*(USE\s|CREATE\s+DATABASE|DROP\s+DATABASE)")) throw new Exception("Unexpected database switching in schema.");
    foreach (var batch in Regex.Split(sql, @"(?im)^\s*GO\s*\r?$").Where(b => !string.IsNullOrWhiteSpace(b))) await Exec(connection, batch);

    int city, otherCity;
    long[] vehicles;
    long foreignVehicle;
    using (var db = Create())
    {
        var catalog = await db.SearchCatalogRows.Where(c => c.RentalMode == "SelfDrive" && c.SearchUnitRate.HasValue)
            .Select(c => new { c.VehicleId, c.CityId }).Distinct().ToListAsync();
        var group = catalog.GroupBy(c => c.CityId).First(g => g.Count() >= 3);
        city = group.Key; vehicles = group.Take(3).Select(c => c.VehicleId).ToArray();
        var foreign = catalog.First(c => c.CityId != city);
        otherCity = foreign.CityId; foreignVehicle = foreign.VehicleId;
        await db.VehicleDeliveryAreas.ExecuteUpdateAsync(s => s.SetProperty(a => a.IsActive, false));
        if (!await db.VehicleRentalModes.AnyAsync(m => m.VehicleId == vehicles[2] && m.RentalMode == "WithDriver"))
        {
            db.VehicleRentalModes.Add(new VehicleRentalMode { VehicleId = vehicles[2], RentalMode = "WithDriver", IsEnabled = false });
            await db.SaveChangesAsync();
        }
        async Task SetArea(long vehicle, string mode, int areaCity, bool active)
        {
            var area = await db.VehicleDeliveryAreas.SingleOrDefaultAsync(a => a.VehicleId == vehicle && a.RentalMode == mode && a.CityId == areaCity);
            if (area == null)
            {
                area = new VehicleDeliveryArea { VehicleId = vehicle, RentalMode = mode, CityId = areaCity };
                db.VehicleDeliveryAreas.Add(area);
            }
            area.FeeModel = "Flat"; area.FlatFee = 150000; area.FeePerKm = null; area.MinimumFee = null;
            area.ServiceRadiusKm = 20; area.FreeWithinKm = 0; area.IsActive = active; area.UpdatedAt = DateTime.UtcNow;
        }
        await SetArea(vehicles[0], "SelfDrive", city, true);
        await SetArea(vehicles[1], "SelfDrive", otherCity, true);
        await SetArea(vehicles[2], "WithDriver", city, true);
        await SetArea(foreignVehicle, "SelfDrive", city, true);
        await db.SaveChangesAsync();
    }
    var service = new SearchService(Create);
    var filter = new CarSearchFilter { RentalMode = "SelfDrive", CityId = city, DeliveryOnly = true };
    var rows = await service.SearchAsync(filter);
    Check(rows.Select(c => c.VehicleId).SequenceEqual(new[] { vehicles[0] }), "SQL excludes other delivery cities, wrong rental modes, inactive areas and vehicles outside the selected region");
    Check(rows.All(c => c.SupportsDelivery && c.DeliveryFee == null), "City-level results show delivery support without inventing an address-specific fee");
    var facets = await service.GetSelfDriveFacetsAsync(filter);
    Check(facets.Types.Sum(t => t.Count) == 1, "Category and brand dialogs use the same delivery region filter");
    var all = await service.SearchAsync(filter with { DeliveryOnly = false });
    Check(vehicles.All(id => all.Any(c => c.VehicleId == id)), "Turning delivery off restores vehicles excluded only by delivery coverage");
    using (var db = Create())
        await db.VehicleDeliveryAreas.Where(a => a.VehicleId == vehicles[0] && a.CityId == city && a.RentalMode == "SelfDrive")
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsActive, false));
    Check((await service.SearchAsync(filter)).Count == 0, "No active service in the selected region produces empty results rather than a wider search");
    Console.WriteLine($"{passed} search checks passed, including real SQL coverage checks.");
}
finally
{
    SqlConnection.ClearAllPools();
    if (created) await Exec(master, $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]");
}

sealed class CaptureSearch : SearchService
{
    public CarSearchFilter? Last;
    public int Calls;
    public override Task<List<CarSearchItem>> SearchAsync(CarSearchFilter filter, CancellationToken token = default)
    {
        filter.Validate(); Last = filter; Calls++;
        return Task.FromResult(new List<CarSearchItem>());
    }
}
