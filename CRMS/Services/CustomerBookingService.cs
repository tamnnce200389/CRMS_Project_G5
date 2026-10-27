
using System;
using System.Data;
using System.Threading.Tasks;
using CRMS.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Services
{
    public class CustomerBookingService
    {
        public async Task<(long BookingId, string BookingCode,
            decimal TotalAmount, decimal DepositAmount)>
            CreateBookingAsync(
                long customerId,
                long vehicleId,
                string rentalType,
                DateTime startAtLocal,
                DateTime endAtLocal)
        {
            if (customerId <= 0 || vehicleId <= 0)
                throw new ArgumentException(
                    "Customer hoặc Vehicle không hợp lệ.");

            if (rentalType != "SelfDrive" &&
                rentalType != "WithDriver")
                throw new ArgumentException(
                    "Loại thuê xe không hợp lệ.");

            if (startAtLocal <= DateTime.Now ||
                endAtLocal <= startAtLocal)
                throw new ArgumentException(
                    "Thời gian thuê không hợp lệ.");

            // WPF nhận giờ Việt Nam.
            // Database CRMS lưu giờ UTC.
            TimeZoneInfo vnTimeZone =
                TimeZoneInfo.FindSystemTimeZoneById(
                    "SE Asia Standard Time");

            DateTime startUtc =
                TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(
                        startAtLocal,
                        DateTimeKind.Unspecified),
                    vnTimeZone);

            DateTime endUtc =
                TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(
                        endAtLocal,
                        DateTimeKind.Unspecified),
                    vnTimeZone);

            using var db = CrmsContextFactory.Create();

            var connection =
                (SqlConnection)db.Database.GetDbConnection();

            await connection.OpenAsync();

            using var command = new SqlCommand(
                "dbo.usp_CreateBooking",
                connection);

            command.CommandType =
                CommandType.StoredProcedure;

            command.Parameters.Add(
                "@CustomerId",
                SqlDbType.BigInt).Value = customerId;

            command.Parameters.Add(
                "@VehicleId",
                SqlDbType.BigInt).Value = vehicleId;

            command.Parameters.Add(
                "@RentalType",
                SqlDbType.VarChar, 12).Value = rentalType;

            // Giai đoạn test dùng đặt cọc tiền mặt.
            command.Parameters.Add(
                "@DepositMethod",
                SqlDbType.VarChar, 5).Value = "Cash";

            command.Parameters.Add(
                "@StartAt",
                SqlDbType.DateTime2).Value = startUtc;

            command.Parameters.Add(
                "@EndAt",
                SqlDbType.DateTime2).Value = endUtc;

            command.Parameters.Add(
                "@RequestKey",
                SqlDbType.UniqueIdentifier).Value =
                    Guid.NewGuid();

            var outputId = command.Parameters.Add(
                "@NewBookingId",
                SqlDbType.BigInt);

            outputId.Direction = ParameterDirection.Output;

            // Procedure tự kiểm tra lịch thuê và bảo trì.
            await command.ExecuteNonQueryAsync();

            long bookingId =
                Convert.ToInt64(outputId.Value);

            // Đọc lại thông tin Booking vừa lưu.
            var booking = await db.Bookings
                .AsNoTracking()
                .SingleAsync(b => b.BookingId == bookingId);

            return (
                booking.BookingId,
                booking.BookingCode,
                booking.TotalAmount,
                booking.DepositAmount
            );
        }
    }
}
