
using CRMS.Data;
using CRMS.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRMS.Services
{
    public class LongTermRateService
    {
        // =====================================
        // LAY CAC GOI THUE CUA MOT XE
        // =====================================
        public async Task<List<VehicleLongTermRate>>
            GetRatesByVehicleAsync(long vehicleId)
        {
            using var context =
                CrmsContextFactory.Create();

            return await context.VehicleLongTermRates
                .AsNoTracking()
                .Where(r =>
                    r.VehicleId == vehicleId &&
                    r.IsActive)
                .OrderBy(r => r.DurationMonths)
                .ToListAsync();
        }

        // =====================================
        // LAY GIA CUA NHIEU XE THEO SO THANG
        // =====================================
        public async Task<Dictionary<long, VehicleLongTermRate>>
            GetRatesByDurationAsync(
                IEnumerable<long> vehicleIds,
                int months)
        {
            // Kiem tra goi thue
            if (months != 1 &&
                months != 3 &&
                months != 6 &&
                months != 9 &&
                months != 12)
            {
                throw new ArgumentException(
                    "Thời hạn thuê không hợp lệ.");
            }

            var ids = vehicleIds.Distinct().ToList();

            if (ids.Count == 0)
            {
                return new Dictionary<long, VehicleLongTermRate>();
            }

            using var context =
                CrmsContextFactory.Create();

            return await context.VehicleLongTermRates
                .AsNoTracking()
                .Where(r =>
                    ids.Contains(r.VehicleId) &&
                    r.DurationMonths == months &&
                    r.IsActive)
                .ToDictionaryAsync(r => r.VehicleId);
        }
    }
}
