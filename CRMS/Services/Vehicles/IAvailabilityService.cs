using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRMS.Services.Vehicles
{
    public interface IAvailabilityService
    {
        Task<HashSet<long>> GetAvailableVehicleIdsAsync(DateTime startAt, DateTime endAt);

        Task<bool> IsAvailableAsync(long vehicleId, DateTime startAt, DateTime endAt);

        Task<VehicleAvailabilityResult> CheckAvailabilityAsync(
            long vehicleId,
            DateTime startAt,
            DateTime endAt,
            bool requireFutureStart = true);

        Task<List<VehicleAvailabilityResult>> CheckFleetAvailabilityAsync(
            DateTime startAt,
            DateTime endAt,
            int? categoryId = null,
            long? vehicleId = null,
            bool includeSuspendedVehicles = true,
            bool requireFutureStart = false);
    }
}
