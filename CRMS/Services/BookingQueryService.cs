using CRMS.Data;
using CRMS.Dtos;
using Microsoft.EntityFrameworkCore;
namespace CRMS.Services;

public interface IBookingQueryService
{
    Task<List<CustomerTrip>> GetMyBookingsAsync(CancellationToken token = default);
    Task<long> ResolveBookingAsync(string lookup, CancellationToken token = default);
}
public sealed class BookingQueryService : IBookingQueryService
{
    private readonly Func<CrmsContext> _create;
    private readonly IUserSession _session;
    public BookingQueryService() : this(CrmsContextFactory.Create, CurrentUserService.Instance) { }
    public BookingQueryService(Func<CrmsContext> create, IUserSession session) { _create = create; _session = session; }
    private async Task<string> Role(CrmsContext db, SessionStamp stamp, CancellationToken token)
    {
        stamp.Ensure(_session); var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == stamp.UserId && u.IsActive, token) ?? throw new InvalidOperationException("Tài khoản không còn hoạt động."); stamp.Ensure(_session); return user.UserRole;
    }
    public async Task<List<CustomerTrip>> GetMyBookingsAsync(CancellationToken token = default)
    {
        var stamp = SessionStamp.Capture(_session); using var db = _create(); if (await Role(db, stamp, token) != "Customer") throw new InvalidOperationException("Chức năng này dành cho khách hàng.");
        var rows = await db.Bookings.AsNoTracking().Where(b => b.CustomerId == stamp.UserId).OrderByDescending(b => b.CreatedAt)
            .Select(b => new CustomerTrip(b.BookingCode, b.Vehicle.Brand + " " + b.Vehicle.Model, b.StartAt, b.EndAt, b.TotalAmount, b.BookingStatus, b.RentalType, b.PriceUnit, b.RentalUnits)).ToListAsync(token);
        stamp.Ensure(_session); return rows;
    }
    public async Task<long> ResolveBookingAsync(string lookup, CancellationToken token = default)
    {
        var stamp = SessionStamp.Capture(_session); using var db = _create(); var role = await Role(db, stamp, token);
        if (role is not ("Admin" or "Staff" or "Customer")) throw new InvalidOperationException("Bạn không có quyền tra cứu.");
        var query = db.Bookings.AsNoTracking().Where(b => role != "Customer" || b.CustomerId == stamp.UserId);
        lookup = lookup.Trim(); long? id = long.TryParse(lookup, out var numeric) ? await query.Where(b => b.BookingId == numeric).Select(b => (long?)b.BookingId).FirstOrDefaultAsync(token) : await query.Where(b => b.BookingCode == lookup).Select(b => (long?)b.BookingId).FirstOrDefaultAsync(token);
        stamp.Ensure(_session); return id ?? throw new InvalidOperationException("Không tìm thấy mã đơn thuê.");
    }
}
