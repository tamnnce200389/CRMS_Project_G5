using System.Data;
using CRMS.Data;
using CRMS.Services.Vehicles;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace CRMS.Services;
public sealed record BookingCreated(long BookingId, string BookingCode, decimal TotalAmount,
    decimal DepositAmount, DateTime DepositDeadline);
public class CustomerBookingService
{
    private readonly Func<CrmsContext> _create;
    private readonly Func<long?> _actor;
    public CustomerBookingService() : this(CrmsContextFactory.Create, () => CurrentUserService.Instance.UserId) { }
    public CustomerBookingService(Func<CrmsContext> create, Func<long?> actor) { _create = create; _actor = actor; }
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
        using var db = _create();
        var connection = (SqlConnection)db.Database.GetDbConnection();
        await connection.OpenAsync();
        using var command = new SqlCommand("dbo.usp_CreateBooking", connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.AddWithValue("@CustomerId", selection.CustomerId);
        command.Parameters.AddWithValue("@VehicleId", selection.VehicleId);
        command.Parameters.Add("@RentalType", SqlDbType.VarChar, 12).Value = selection.RentalMode;
        command.Parameters.Add("@DepositMethod", SqlDbType.VarChar, 5).Value = "Cash";
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
        var row = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == Convert.ToInt64(output.Value));
        if (_actor() != selection.CustomerId) throw new InvalidOperationException("Phiên đăng nhập đã thay đổi. Hãy đăng nhập lại để xem đơn đã tạo.");
        return new(row.BookingId, row.BookingCode, row.TotalAmount, row.DepositAmount, row.DepositDeadline);
    }
    public async Task<(long BookingId, string BookingCode, decimal TotalAmount, decimal DepositAmount)> CreateBookingAsync(
        long customerId, long vehicleId, string rentalType, DateTime startAtLocal, DateTime endAtLocal)
    {
        var result = await CreateAsync(new(customerId, vehicleId, rentalType, startAtLocal, endAtLocal), Guid.NewGuid());
        return (result.BookingId, result.BookingCode, result.TotalAmount, result.DepositAmount);
    }
}
