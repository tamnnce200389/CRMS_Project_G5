using CRMS.Data;
using CRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Repositories
{
    public class VehicleRepository
    {
        public async Task<List<Vehicle>> GetAllAsync()
        {
            using var db = CrmsContextFactory.Create();

            return await db.Vehicles
                .AsNoTracking()
                .Include(v => v.Category)
                .Include(v => v.VehicleImages)
                .OrderBy(v => v.VehicleId)
                .ToListAsync();
        }
    }
}
