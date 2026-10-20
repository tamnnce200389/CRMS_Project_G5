
using CRMS.Data;
using CRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Repositories
{
    public class DriverRepository
    {
        public async Task<List<Driver>> GetAllAsync()
        {
            using var db = CrmsContextFactory.Create();

            return await db.Drivers
                .AsNoTracking()
                .OrderBy(d => d.DriverId)
                .ToListAsync();
        }
    }
}
