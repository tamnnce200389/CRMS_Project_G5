namespace CRMS.Services.Vehicles;

public interface IVehicleService
{
    Task<List<VehicleListItem>> GetVehiclesAsync(VehicleFilter? filter = null);
    Task<VehicleDetailsDto?> GetVehicleDetailsAsync(long vehicleId);
    Task<long> SaveVehicleAsync(VehicleUpsertRequest request);
    Task SetVehicleStatusAsync(long vehicleId, string operationalStatus);

    Task<List<CategoryListItem>> GetCategoriesAsync(bool includeInactive = true);
    Task<int> SaveCategoryAsync(CategoryUpsertRequest request);
    Task SetCategoryActiveAsync(int categoryId, bool isActive);
}
