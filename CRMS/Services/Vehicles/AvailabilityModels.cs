namespace CRMS.Services.Vehicles;

public enum AvailabilityConflictType
{
    VehicleNotFound,
    VehicleSuspended,
    CategoryInactive,
    BookingInUse,
    BookingConfirmed,
    BookingPendingDeposit,
    Maintenance,
    CalendarBlock
}

public sealed record AvailabilityConflict(
    AvailabilityConflictType Type,
    string Title,
    string Reason,
    DateTime? StartAtLocal = null,
    DateTime? EndAtLocal = null,
    string? ReferenceCode = null)
{
    public bool HasReferenceCode => !string.IsNullOrWhiteSpace(ReferenceCode);
}

public sealed record BookingConflictSnapshot(
    string BookingCode,
    string BookingStatus,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    DateTime DepositDeadlineUtc);

public sealed record MaintenanceConflictSnapshot(
    long MaintenanceId,
    string MaintenanceStatus,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string Description);

public sealed class VehicleAvailabilityResult
{
    public long VehicleId { get; init; }
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public string LicensePlate { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public byte SeatCount { get; init; }
    public string Transmission { get; init; } = string.Empty;
    public string FuelType { get; init; } = string.Empty;
    public decimal DailyRate { get; init; }
    public string OperationalStatus { get; init; } = "Active";
    public string? PrimaryImageUrl { get; init; }
    public DateTime StartAtLocal { get; init; }
    public DateTime EndAtLocal { get; init; }
    public IReadOnlyList<AvailabilityConflict> Conflicts { get; init; } = Array.Empty<AvailabilityConflict>();

    public bool IsAvailable => Conflicts.Count == 0;
    public bool HasConflicts => Conflicts.Count > 0;
    public int ConflictCount => Conflicts.Count;
    public string DisplayName => $"{Brand} {Model}".Trim();
    public string PeriodText => $"{StartAtLocal:dd/MM/yyyy HH:mm} → {EndAtLocal:dd/MM/yyyy HH:mm}";
    public string AvailabilityLabel => IsAvailable ? "Khả dụng" : "Không khả dụng";

    public string SummaryReason =>
        IsAvailable
            ? "Sẵn sàng nhận lịch đặt trong khoảng giờ đã chọn."
            : Conflicts.Count == 1
                ? Conflicts[0].Reason
                : $"{Conflicts[0].Reason} (+{Conflicts.Count - 1} xung đột khác)";
}
