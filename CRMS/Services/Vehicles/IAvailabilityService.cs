namespace CRMS.Services.Vehicles;

public interface IAvailabilityService
{
    Task<HashSet<long>> GetAvailableVehicleIdsAsync(DateTime startAt, DateTime endAt);

    Task<bool> IsAvailableAsync(long vehicleId, DateTime startAt, DateTime endAt);

    /// <summary>
    /// F07 contract for TV1 and TV6: checks a single vehicle over half-open [startAt, endAt)
    /// and returns availability status together with detailed unavailability reasons.
    /// </summary>
    Task<VehicleAvailabilityResult> CheckAvailabilityAsync(
        long vehicleId,
        DateTime startAt,
        DateTime endAt,
        bool requireFutureStart = true);

    /// <summary>
    /// Batch availability check for Admin/Staff AvailabilityPage across all or filtered vehicles.
    /// </summary>
    Task<List<VehicleAvailabilityResult>> CheckFleetAvailabilityAsync(
        DateTime startAt,
        DateTime endAt,
        int? categoryId = null,
        long? vehicleId = null,
        bool includeSuspendedVehicles = true,
        bool requireFutureStart = false);
}
