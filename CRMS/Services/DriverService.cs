using System.Data;
using CRMS.Data;
using CRMS.Models;
using Microsoft.EntityFrameworkCore;
namespace CRMS.Services;

public interface IDriverService
{
    Task<List<Driver>> GetAllAsync();
    Task AddAsync(Driver driver);
    Task UpdateAsync(Driver driver, Driver original);
    Task<bool> SetActiveAsync(long id, bool active, bool originalActive);
}
public sealed class DriverService : IDriverService
{
    private readonly Func<CrmsContext> _create;
    private readonly IUserSession _session;
    public DriverService() : this(CrmsContextFactory.Create, CurrentUserService.Instance) { }
    public DriverService(Func<CrmsContext> create, IUserSession session) { _create = create; _session = session; }
    private async Task Authorize(CrmsContext db, SessionStamp stamp, bool write)
    {
        stamp.Ensure(_session);
        if (!await db.Users.AnyAsync(u => u.UserId == stamp.UserId && u.IsActive && (u.UserRole == "Admin" || (!write && u.UserRole == "Staff")))) throw new InvalidOperationException("Bạn không có quyền thực hiện thao tác tài xế.");
        stamp.Ensure(_session);
    }
    public async Task<List<Driver>> GetAllAsync()
    {
        var stamp = SessionStamp.Capture(_session); using var db = _create(); await Authorize(db, stamp, false);
        var result = await db.Drivers.AsNoTracking().OrderBy(d => d.DriverId).ToListAsync(); stamp.Ensure(_session); return result;
    }
    private static void Validate(Driver d)
    {
        d.FullName = d.FullName.Trim(); d.Phone = string.IsNullOrWhiteSpace(d.Phone) ? null : d.Phone.Trim(); d.LicenseNumber = string.IsNullOrWhiteSpace(d.LicenseNumber) ? null : d.LicenseNumber.Trim(); d.Notes = string.IsNullOrWhiteSpace(d.Notes) ? null : d.Notes.Trim();
        if (d.FullName.Length is < 2 or > 120 || d.Phone?.Length > 20 || d.LicenseNumber?.Length > 40 || d.Notes?.Length > 1000) throw new ArgumentException("Thông tin tài xế vượt giới hạn: tên 2–120, điện thoại 20, bằng lái 40, ghi chú 1000 ký tự.");
    }
    private static bool Same(Driver a, Driver b) => a.FullName == b.FullName && a.Phone == b.Phone && a.LicenseNumber == b.LicenseNumber && a.Notes == b.Notes && a.IsActive == b.IsActive;
    public Task AddAsync(Driver driver) => SaveAsync(driver, null);
    public Task UpdateAsync(Driver driver, Driver original) => SaveAsync(driver, original);
    private async Task SaveAsync(Driver driver, Driver? original)
    {
        var stamp = SessionStamp.Capture(_session); Validate(driver);
        using var db = _create(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable); await Authorize(db, stamp, true);
        if (driver.LicenseNumber != null && await db.Drivers.AnyAsync(d => d.LicenseNumber == driver.LicenseNumber && d.DriverId != driver.DriverId)) throw new InvalidOperationException("Số bằng lái đã được sử dụng.");
        if (original is null)
        {
            if (driver.DriverId != 0) throw new ArgumentException("Thiếu bản gốc để cập nhật tài xế.");
            db.Drivers.Add(driver);
        }
        else
        {
            var row = await db.Drivers.SingleOrDefaultAsync(d => d.DriverId == driver.DriverId) ?? throw new InvalidOperationException("Không tìm thấy tài xế.");
            if (!Same(row, original)) throw new InvalidOperationException("Tài xế đã thay đổi ở nơi khác. Vui lòng tải lại.");
            row.FullName = driver.FullName; row.Phone = driver.Phone; row.LicenseNumber = driver.LicenseNumber; row.Notes = driver.Notes; row.IsActive = driver.IsActive;
        }
        stamp.Ensure(_session); await db.SaveChangesAsync(); stamp.Ensure(_session); await tx.CommitAsync();
    }
    public async Task<bool> SetActiveAsync(long id, bool active, bool originalActive)
    {
        var stamp = SessionStamp.Capture(_session); using var db = _create(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable); await Authorize(db, stamp, true);
        var row = await db.Drivers.SingleOrDefaultAsync(d => d.DriverId == id); if (row == null) return false;
        if (row.IsActive != originalActive) throw new InvalidOperationException("Trạng thái tài xế đã thay đổi. Vui lòng tải lại.");
        row.IsActive = active; stamp.Ensure(_session); await db.SaveChangesAsync(); stamp.Ensure(_session); await tx.CommitAsync(); return true;
    }
}
