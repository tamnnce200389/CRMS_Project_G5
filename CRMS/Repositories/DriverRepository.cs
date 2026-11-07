
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CRMS.Data;
using CRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Repositories
{
    public class DriverRepository
    {
        // Lấy danh sách tài xế
        public async Task<List<Driver>> GetAllAsync()
        {
            using var db = CrmsContextFactory.Create();

            return await db.Drivers
                .AsNoTracking()
                .OrderBy(d => d.DriverId)
                .ToListAsync();
        }

        // Tìm tài xế theo ID
        public async Task<Driver?> GetByIdAsync(long driverId)
        {
            using var db = CrmsContextFactory.Create();

            return await db.Drivers
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DriverId == driverId);
        }

        // Thêm tài xế
        public async Task AddAsync(Driver driver)
        {
            using var db = CrmsContextFactory.Create();

            await db.Drivers.AddAsync(driver);
            await db.SaveChangesAsync();
        }

        // Cập nhật thông tin tài xế
        public async Task UpdateAsync(Driver driver)
        {
            using var db = CrmsContextFactory.Create();

            var existing = await db.Drivers
                .FirstOrDefaultAsync(d => d.DriverId == driver.DriverId);

            if (existing == null)
            {
                throw new InvalidOperationException(
                    "Không tìm thấy tài xế cần cập nhật.");
            }

            existing.FullName = driver.FullName;
            existing.Phone = driver.Phone;
            existing.LicenseNumber = driver.LicenseNumber;
            existing.IsActive = driver.IsActive;
            existing.Notes = driver.Notes;

            await db.SaveChangesAsync();
        }

        // Kích hoạt hoặc ngừng hoạt động tài xế
        public async Task<bool> SetActiveAsync(
            long driverId, bool isActive)
        {
            using var db = CrmsContextFactory.Create();

            var driver = await db.Drivers
                .FirstOrDefaultAsync(d => d.DriverId == driverId);

            if (driver == null)
            {
                return false;
            }

            driver.IsActive = isActive;

            await db.SaveChangesAsync();
            return true;
        }
    }
}
