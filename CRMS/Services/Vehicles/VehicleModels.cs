using System.IO;

namespace CRMS.Services.Vehicles;

public sealed record VehicleFilter(
    string? Keyword = null,
    int? CategoryId = null,
    string? OperationalStatus = null,
    decimal? MinDailyRate = null,
    decimal? MaxDailyRate = null,
    bool OnlyActiveCategories = false);

public sealed record VehicleImageDto(
    long ImageId,
    string ImageUrl,
    bool IsPrimary,
    int SortOrder)
{
    public string Caption
    {
        get
        {
            if (ImageUrl.StartsWith("pack://application:,,,/CRMS;component/Assets/Vehicles/", StringComparison.Ordinal))
                return Path.GetFileNameWithoutExtension(ImageUrl).EndsWith("-interior", StringComparison.Ordinal) ? "Nội thất" : "Ngoại thất";
            return IsPrimary ? "Ảnh đại diện" : "Ảnh chi tiết";
        }
    }
}

public sealed class VehicleListItem
{
    public long VehicleId { get; init; }
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = "";
    public string LicensePlate { get; init; } = "";
    public string Brand { get; init; } = "";
    public string Model { get; init; } = "";
    public short ManufactureYear { get; init; }
    public byte SeatCount { get; init; }
    public string Transmission { get; init; } = "";
    public string FuelType { get; init; } = "";
    public decimal DailyRate { get; init; }
    public int OdometerKm { get; init; }
    public string OperationalStatus { get; init; } = "";
    public string? Description { get; init; }
    public string? PrimaryImageUrl { get; init; }
    public int ImageCount { get; init; }
    public bool IsActive => OperationalStatus == "Active";
}

public sealed class VehicleDetailsDto
{
    public long VehicleId { get; init; }
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = "";
    public string LicensePlate { get; init; } = "";
    public string Brand { get; init; } = "";
    public string Model { get; init; } = "";
    public short ManufactureYear { get; init; }
    public byte SeatCount { get; init; }
    public string Transmission { get; init; } = "";
    public string FuelType { get; init; } = "";
    public decimal DailyRate { get; init; }
    public int OdometerKm { get; init; }
    public string OperationalStatus { get; init; } = "";
    public string? Description { get; init; }
    public IReadOnlyList<VehicleImageDto> Images { get; init; } = Array.Empty<VehicleImageDto>();
}

public sealed record VehicleImageInput(
    string ImageUrl,
    bool IsPrimary = false,
    int SortOrder = 0);

public sealed record VehicleUpsertRequest(
    long? VehicleId,
    int CategoryId,
    string LicensePlate,
    string Brand,
    string Model,
    short ManufactureYear,
    byte SeatCount,
    string Transmission,
    string FuelType,
    decimal DailyRate,
    int OdometerKm,
    string OperationalStatus,
    string? Description,
    IReadOnlyList<VehicleImageInput>? Images = null);

public sealed class CategoryListItem
{
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = "";
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public int TotalVehicles { get; init; }
    public int ActiveVehicles { get; init; }
}

public sealed record CategoryUpsertRequest(
    int? CategoryId,
    string CategoryName,
    string? Description,
    bool IsActive = true);
