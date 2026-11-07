namespace CRMS.Services.Vehicles;

public interface IMaintenanceService
{
    Task<List<MaintenanceListItem>> GetMaintenanceRecordsAsync(MaintenanceFilter? filter = null);

    Task<long> ScheduleMaintenanceAsync(MaintenanceScheduleRequest request);

    Task UpdateMaintenanceAsync(MaintenanceUpdateRequest request);

    Task ChangeStatusAsync(
        long maintenanceId,
        string nextStatus,
        decimal? finalCost = null,
        string? updatedDescription = null);
}
