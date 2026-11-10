namespace CRMS.Services.Vehicles;

public sealed class MaintenanceListItem
{
    public long MaintenanceId { get; init; }
    public long VehicleId { get; init; }
    public string LicensePlate { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public long CreatedByUserId { get; init; }
    public string CreatedByName { get; init; } = string.Empty;
    public DateTime StartAtLocal { get; init; }
    public DateTime EndAtLocal { get; init; }
    public string MaintenanceStatus { get; init; } = "Scheduled";
    public string Description { get; init; } = string.Empty;
    public decimal Cost { get; init; }

    public string ReferenceCode => $"BT-#{MaintenanceId}";
    public string VehicleDisplayName => $"{Brand} {Model}".Trim();
    public string PeriodText => $"{StartAtLocal:dd/MM/yyyy HH:mm} → {EndAtLocal:dd/MM/yyyy HH:mm}";

    public bool IsScheduled => string.Equals(MaintenanceStatus, "Scheduled", StringComparison.OrdinalIgnoreCase);
    public bool IsInProgress => string.Equals(MaintenanceStatus, "InProgress", StringComparison.OrdinalIgnoreCase);
    public bool IsCompleted => string.Equals(MaintenanceStatus, "Completed", StringComparison.OrdinalIgnoreCase);
    public bool IsCancelled => string.Equals(MaintenanceStatus, "Cancelled", StringComparison.OrdinalIgnoreCase);

    public bool IsEditable => IsScheduled || IsInProgress;
    public bool CanStart => IsScheduled;
    public bool CanComplete => IsScheduled || IsInProgress;
    public bool CanCancel => IsScheduled || IsInProgress;

    public override string ToString() => $"{ReferenceCode} · {LicensePlate}";
}

public sealed record MaintenanceFilter(
    string? Keyword = null,
    long? VehicleId = null,
    string? MaintenanceStatus = null,
    int MaxRecords = 500);

public sealed record MaintenanceScheduleRequest(
    long VehicleId,
    DateTime StartAtLocal,
    DateTime EndAtLocal,
    string Description,
    decimal Cost = 0m,
    bool StartImmediately = false);

public sealed record MaintenanceUpdateRequest(
    long MaintenanceId,
    DateTime StartAtLocal,
    DateTime EndAtLocal,
    string Description,
    decimal Cost,
    string MaintenanceStatus);
