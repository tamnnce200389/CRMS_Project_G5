
using System;
using System.Data;
using System.Threading.Tasks;
using CRMS.Data;
using CRMS.Services.Vehicles;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Services;

public sealed record BookingCreated(
    long BookingId,
    string BookingCode,
    decimal TotalAmount,
    decimal DepositAmount,
    DateTime DepositDeadline
);

public class CustomerBookingService
{
    private readonly Func<CrmsContext> _create;
    private readonly Func<long?> _actor;

    public CustomerBookingService()
        : this(
            CrmsContextFactory.Create,
            () => CurrentUserService.Instance.UserId)
    {
    }

    public CustomerBookingService(
        Func<CrmsContext> create,
        Func<long?> actor)
    {
        _create = create
            ?? throw new ArgumentNullException(nameof(create));

        _actor = actor
            ?? throw new ArgumentNullException(nameof(actor));
    }

    // F21 - Tao don thue xe
    public virtual async Task<BookingCreated> CreateAsync(
        BookingSelection selection,
        Guid requestKey)
    {
        if (selection == null)
        {
            throw new ArgumentNullException(nameof(selection));
        }

        if (_actor() != selection.CustomerId ||
            selection.CustomerId <= 0)
        {
            throw new InvalidOperationException(
                "Phiên đăng nhập đã thay đổi. " +
                "Vui lòng mở lại xác nhận thuê xe.");
        }

        if (selection.VehicleId <= 0 ||
            requestKey == Guid.Empty)
        {
            throw new ArgumentException(
                "Thông tin đặt xe không hợp lệ.");
        }

        var (start, end, _, _) =
            AvailabilityRules.ValidatePeriod(
                selection.StartAt,
                selection.EndAt,
                DateTime.UtcNow,
                true
            );

        bool validRentalMode =
            (selection.RentalMode == "SelfDrive" &&
             selection.PriceUnit is "Day" or "Hour")
            ||
            (selection.RentalMode == "WithDriver" &&
             selection.PriceUnit == "Day")
            ||
            (selection.RentalMode == "LongTerm" &&
             selection.PriceUnit == "Month");

        if (!validRentalMode)
        {
            throw new ArgumentException(
                "Hình thức thuê và đơn vị giá không phù hợp.");
        }

        if (selection.PriceUnit == "Month")
        {
            if (selection.DurationMonths is not
                (1 or 3 or 6 or 9 or 12))
            {
                throw new ArgumentException(
                    "Số tháng thuê không hợp lệ.");
            }

            if (selection.StartAt.AddMonths(
                    selection.DurationMonths.Value)
                != selection.EndAt)
            {
                throw new ArgumentException(
                    "Khoảng thuê phải đúng số tháng của gói.");
            }
        }
        else if (selection.DurationMonths.HasValue)
        {
            throw new ArgumentException(
                "Số tháng chỉ dùng cho gói dài hạn.");
        }

        if (selection.ExpectedTotal is <= 0)
        {
            throw new ArgumentException(
                "Giá thuê không hợp lệ.");
        }

        using var db = _create();

        var connection =
            (SqlConnection)db.Database.GetDbConnection();

        await connection.OpenAsync();

        using var command = new SqlCommand(
            "dbo.usp_CreateBooking",
            connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add(
            "@CustomerId",
            SqlDbType.BigInt
        ).Value = selection.CustomerId;

        command.Parameters.Add(
            "@VehicleId",
            SqlDbType.BigInt
        ).Value = selection.VehicleId;

        command.Parameters.Add(
            "@RentalType",
            SqlDbType.VarChar,
            12
        ).Value = selection.RentalMode;

        command.Parameters.Add(
            "@DepositMethod",
            SqlDbType.VarChar,
            5
        ).Value = "Cash";

        command.Parameters.Add(
            "@StartAt",
            SqlDbType.DateTime2
        ).Value = start;

        command.Parameters.Add(
            "@EndAt",
            SqlDbType.DateTime2
        ).Value = end;

        command.Parameters.Add(
            "@RequestKey",
            SqlDbType.UniqueIdentifier
        ).Value = requestKey;

        command.Parameters.Add(
            "@PriceUnit",
            SqlDbType.VarChar,
            5
        ).Value = selection.PriceUnit;

        command.Parameters.Add(
            "@DurationMonths",
            SqlDbType.TinyInt
        ).Value =
            (object?)selection.DurationMonths
            ?? DBNull.Value;

        if (selection.ExpectedTotal.HasValue)
        {
            var expected = command.Parameters.Add(
                "@ExpectedTotal",
                SqlDbType.Decimal
            );

            expected.Precision = 18;
            expected.Scale = 2;
            expected.Value = selection.ExpectedTotal.Value;
        }

        var output = command.Parameters.Add(
            "@NewBookingId",
            SqlDbType.BigInt
        );

        output.Direction = ParameterDirection.Output;

        await command.ExecuteNonQueryAsync();

        long bookingId = Convert.ToInt64(output.Value);

        var row = await db.Bookings
            .AsNoTracking()
            .SingleAsync(
                b => b.BookingId == bookingId
            );

        if (_actor() != selection.CustomerId)
        {
            throw new InvalidOperationException(
                "Phiên đăng nhập đã thay đổi. " +
                "Hãy đăng nhập lại để xem đơn đã tạo.");
        }

        return new BookingCreated(
            row.BookingId,
            row.BookingCode,
            row.TotalAmount,
            row.DepositAmount,
            row.DepositDeadline
        );
    }

    // F21 - Tim lai don da tao theo RequestKey
    public virtual async Task<BookingCreated?> FindByRequestKeyAsync(
        long customerId,
        Guid requestKey)
    {
        if (customerId <= 0 ||
            requestKey == Guid.Empty)
        {
            throw new ArgumentException(
                "Thông tin yêu cầu không hợp lệ.");
        }

        if (_actor() != customerId)
        {
            throw new InvalidOperationException(
                "Phiên đăng nhập đã thay đổi.");
        }

        using var db = _create();

        var booking = await db.Bookings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                b => b.RequestKey == requestKey &&
                     b.CustomerId == customerId
            );

        if (booking == null)
        {
            return null;
        }

        if (_actor() != customerId)
        {
            throw new InvalidOperationException(
                "Phiên đăng nhập đã thay đổi.");
        }

        return new BookingCreated(
            booking.BookingId,
            booking.BookingCode,
            booking.TotalAmount,
            booking.DepositAmount,
            booking.DepositDeadline
        );
    }

    // Giu tuong thich voi cac chuc nang cu
    public async Task<(
        long BookingId,
        string BookingCode,
        decimal TotalAmount,
        decimal DepositAmount)> CreateBookingAsync(
            long customerId,
            long vehicleId,
            string rentalType,
            DateTime startAtLocal,
            DateTime endAtLocal)
    {
        var selection = new BookingSelection(
            customerId,
            vehicleId,
            rentalType,
            startAtLocal,
            endAtLocal
        );

        var result = await CreateAsync(
            selection,
            Guid.NewGuid()
        );

        return (
            result.BookingId,
            result.BookingCode,
            result.TotalAmount,
            result.DepositAmount
        );
    }
}
