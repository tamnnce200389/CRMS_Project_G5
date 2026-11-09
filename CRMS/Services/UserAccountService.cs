using System.Data;
using CRMS.Data;
using CRMS.Dtos;
using CRMS.Models;
using Microsoft.EntityFrameworkCore;
namespace CRMS.Services;

public interface IUserAccountService
{
    Task<UserAccountDto> GetProfileAsync(CancellationToken token = default);
    Task<UserAccountDto> SaveProfileAsync(ProfileUpdateRequest request, CancellationToken token = default);
    Task<List<UserAccountDto>> GetUsersAsync(CancellationToken token = default);
    Task ChangeRoleAsync(UserAccountDto target, string role, CancellationToken token = default);
    Task SetActiveAsync(UserAccountDto target, bool active, CancellationToken token = default);
}
public sealed class UserAccountService : IUserAccountService
{
    private readonly Func<CrmsContext> _create;
    private readonly IUserSession _session;
    public UserAccountService() : this(CrmsContextFactory.Create, CurrentUserService.Instance) { }
    public UserAccountService(Func<CrmsContext> create, IUserSession session) { _create = create; _session = session; }
    private static UserAccountDto Dto(User u) => new(u.UserId, u.FullName, u.Email, u.Phone, u.UserRole, u.IsActive, u.CreatedAt, u.RowVersion.ToArray());
    private static async Task SaveAsync(CrmsContext db, CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException ex)
        { throw new InvalidOperationException("Thông tin đã thay đổi ở nơi khác. Vui lòng tải lại trước khi lưu.", ex); }
    }
    private async Task<User> Authorize(CrmsContext db, SessionStamp stamp, bool admin, CancellationToken token)
    {
        stamp.Ensure(_session);
        var actor = await db.Users.SingleOrDefaultAsync(u => u.UserId == stamp.UserId, token);
        if (actor is null || !actor.IsActive || (admin && actor.UserRole != "Admin")) throw new InvalidOperationException("Bạn không có quyền thực hiện thao tác này.");
        stamp.Ensure(_session); return actor;
    }
    public async Task<UserAccountDto> GetProfileAsync(CancellationToken token = default)
    {
        var stamp = SessionStamp.Capture(_session); using var db = _create();
        var user = await Authorize(db, stamp, false, token); stamp.Ensure(_session); return Dto(user);
    }
    public async Task<UserAccountDto> SaveProfileAsync(ProfileUpdateRequest request, CancellationToken token = default)
    {
        var stamp = SessionStamp.Capture(_session);
        var name = request.FullName.Trim(); var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        if (name.Length is < 2 or > 120 || phone?.Length > 20) throw new ArgumentException("Họ tên cần 2–120 ký tự; số điện thoại tối đa 20 ký tự.");
        if (request.RowVersion is not { Length: > 0 }) throw new ArgumentException("Vui lòng tải lại hồ sơ trước khi lưu.");
        using var db = _create(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var user = await Authorize(db, stamp, false, token);
        db.Entry(user).Property(u => u.RowVersion).OriginalValue = request.RowVersion;
        user.FullName = name; user.Phone = phone; user.UpdatedAt = DateTime.UtcNow;
        stamp.Ensure(_session); await SaveAsync(db, token); stamp.Ensure(_session); await tx.CommitAsync(token);
        return Dto(user);
    }
    public async Task<List<UserAccountDto>> GetUsersAsync(CancellationToken token = default)
    {
        var stamp = SessionStamp.Capture(_session); using var db = _create(); await Authorize(db, stamp, true, token);
        var rows = await db.Users.AsNoTracking().OrderBy(u => u.UserId).Select(u => new UserAccountDto(u.UserId, u.FullName, u.Email, u.Phone, u.UserRole, u.IsActive, u.CreatedAt, u.RowVersion)).ToListAsync(token);
        stamp.Ensure(_session); return rows;
    }
    public Task ChangeRoleAsync(UserAccountDto target, string role, CancellationToken token = default)
    {
        if (role is not ("Customer" or "Staff" or "Admin")) throw new ArgumentException("Vai trò không hợp lệ.");
        return UpdateAccount(target, role, null, token);
    }
    public Task SetActiveAsync(UserAccountDto target, bool active, CancellationToken token = default) => UpdateAccount(target, null, active, token);
    private async Task UpdateAccount(UserAccountDto target, string? role, bool? active, CancellationToken token)
    {
        var stamp = SessionStamp.Capture(_session);
        if (target.UserId == stamp.UserId) throw new InvalidOperationException("Không thể tự khóa hoặc tự đổi vai trò.");
        using var db = _create(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        await Authorize(db, stamp, true, token);
        var user = await db.Users.SingleOrDefaultAsync(u => u.UserId == target.UserId, token) ?? throw new InvalidOperationException("Không tìm thấy tài khoản.");
        db.Entry(user).Property(u => u.RowVersion).OriginalValue = target.RowVersion;
        if (role != null) user.UserRole = role; if (active.HasValue) user.IsActive = active.Value; user.UpdatedAt = DateTime.UtcNow;
        stamp.Ensure(_session); await SaveAsync(db, token); stamp.Ensure(_session); await tx.CommitAsync(token);
    }
}
