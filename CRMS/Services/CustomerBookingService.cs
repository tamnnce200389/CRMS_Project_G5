using System.Data;
using CRMS.Data;
using CRMS.Services.Vehicles;
using CRMS.Services.Payments;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace CRMS.Services;
public sealed record BookingCreated(long BookingId, string BookingCode, decimal TotalAmount,
    decimal DepositAmount, DateTime DepositDeadline, bool IsDemo = false);
public class CustomerBookingService
{
    private readonly Func<CrmsContext> _create;
    private readonly Func<long?> _actor;
    private readonly DemoPaymentOptions? _demoOptions;
    public CustomerBookingService() : this(CrmsContextFactory.Create, () => CurrentUserService.Instance.UserId) { }
    public CustomerBookingService(Func<CrmsContext> create, Func<long?> actor, DemoPaymentOptions? demoOptions = null)
    { _create = create; _actor = actor; _demoOptions = demoOptions; }
    public virtual async Task<BookingCreated> CreateAsync(BookingSelection selection, Guid requestKey)
    {
        if (_actor() != selection.CustomerId || selection.CustomerId <= 0)
            throw new InvalidOperationException("Phiên đăng nhập đã thay đổi. Vui lòng mở lại xác nhận thuê xe.");
        if (selection.VehicleId <= 0 || requestKey == Guid.Empty) throw new ArgumentException("Thông tin đặt xe không hợp lệ.");
        var (start, end, _, _) = AvailabilityRules.ValidatePeriod(selection.StartAt, selection.EndAt, DateTime.UtcNow, true);
        if (!(selection.RentalMode == "SelfDrive" && selection.PriceUnit is "Day" or "Hour") &&
            !(selection.RentalMode == "WithDriver" && selection.PriceUnit == "Day") &&
            !(selection.RentalMode == "LongTerm" && selection.PriceUnit == "Month"))
            throw new ArgumentException("Hình thức thuê và đơn vị giá không phù hợp.");
        if (selection.PriceUnit == "Month" && (selection.DurationMonths is not (1 or 3 or 6 or 9 or 12) || selection.StartAt.AddMonths(selection.DurationMonths.Value) != selection.EndAt))
            throw new ArgumentException("Khoảng thuê phải đúng số tháng của gói.");
        if (selection.PriceUnit != "Month" && selection.DurationMonths.HasValue) throw new ArgumentException("Số tháng chỉ dùng cho gói dài hạn.");
        if (selection.ExpectedTotal is <= 0) throw new ArgumentException("Giá thuê không hợp lệ.");
        if (selection.DepositMethod != "Cash") throw new ArgumentException("Phương thức đặt cọc không được hỗ trợ.");
        var demoOptions = selection.IsDemo ? _demoOptions ?? DemoPaymentOptions.Load() : null;
        demoOptions?.ValidateReceiver();
        using var db = _create();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var existing = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(b => b.RequestKey == requestKey);
        if (existing != null && existing.IsDemo != selection.IsDemo)
            throw new InvalidOperationException("Không thể đổi chế độ thanh toán cho yêu cầu đã tạo. Vui lòng mở lại xác nhận thuê xe.");
        var connection = (SqlConnection)db.Database.GetDbConnection();
        using var command = new SqlCommand("dbo.usp_CreateBooking", connection, (SqlTransaction)transaction.GetDbTransaction()) { CommandType = CommandType.StoredProcedure };
        command.Parameters.AddWithValue("@CustomerId", selection.CustomerId);
        command.Parameters.AddWithValue("@VehicleId", selection.VehicleId);
        command.Parameters.Add("@RentalType", SqlDbType.VarChar, 12).Value = selection.RentalMode;
        // Reuse the existing reservation hold policy. IsDemo and DemoPayments identify
        // the QR simulation; this does not create a Cash receipt in Payments.
        command.Parameters.Add("@DepositMethod", SqlDbType.VarChar, 5).Value = selection.DepositMethod;
        command.Parameters.Add("@StartAt", SqlDbType.DateTime2).Value = start;
        command.Parameters.Add("@EndAt", SqlDbType.DateTime2).Value = end;
        command.Parameters.Add("@RequestKey", SqlDbType.UniqueIdentifier).Value = requestKey;
        command.Parameters.Add("@PriceUnit", SqlDbType.VarChar, 5).Value = selection.PriceUnit;
        command.Parameters.Add("@DurationMonths", SqlDbType.TinyInt).Value = (object?)selection.DurationMonths ?? DBNull.Value;
        if (selection.ExpectedTotal.HasValue)
        {
            var expected = command.Parameters.Add("@ExpectedTotal", SqlDbType.Decimal);
            expected.Precision = 18; expected.Scale = 2; expected.Value = selection.ExpectedTotal.Value;
        }
        var output = command.Parameters.Add("@NewBookingId", SqlDbType.BigInt); output.Direction = ParameterDirection.Output;
        await command.ExecuteNonQueryAsync();
        var row = await db.Bookings.SingleAsync(b => b.BookingId == Convert.ToInt64(output.Value));
        if (selection.IsDemo)
        {
            row.IsDemo = true;
            if (!await db.DemoPayments.AnyAsync(p => p.BookingId == row.BookingId))
                db.DemoPayments.Add(DemoPaymentService.NewPending(row, demoOptions!));
            await db.SaveChangesAsync();
        }
        if (_actor() != selection.CustomerId) throw new InvalidOperationException("Phiên đăng nhập đã thay đổi. Hãy đăng nhập lại để xem đơn đã tạo.");
        await transaction.CommitAsync();
        return new(row.BookingId, row.BookingCode, row.TotalAmount, row.DepositAmount, row.DepositDeadline, row.IsDemo);
    }
    public async Task<(long BookingId, string BookingCode, decimal TotalAmount, decimal DepositAmount)> CreateBookingAsync(
        long customerId, long vehicleId, string rentalType, DateTime startAtLocal, DateTime endAtLocal)
    {
        var result = await CreateAsync(new(customerId, vehicleId, rentalType, startAtLocal, endAtLocal), Guid.NewGuid());
        return (result.BookingId, result.BookingCode, result.TotalAmount, result.DepositAmount);
    }
}
