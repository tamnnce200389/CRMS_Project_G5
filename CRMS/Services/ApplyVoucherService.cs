
using System;
using System.Data;
using System.Threading.Tasks;
using CRMS.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Services;

public class ApplyVoucherService
{
    public async Task<decimal> ApplyAsync(
        long bookingId,
        long customerId,
        string voucherCode)
    {
        if (bookingId <= 0)
            throw new ArgumentException("BookingId không hợp lệ.");

        if (customerId <= 0)
            throw new ArgumentException("CustomerId không hợp lệ.");

        if (string.IsNullOrWhiteSpace(voucherCode))
            throw new ArgumentException("Vui lòng nhập mã Voucher.");

        voucherCode = voucherCode.Trim();

        if (voucherCode.Length > 40)
            throw new ArgumentException("Mã Voucher tối đa 40 ký tự.");

        if (CurrentUserService.Instance.UserId != customerId)
            throw new InvalidOperationException(
                "Bạn không có quyền áp dụng Voucher cho khách hàng này.");

        await using var db = CrmsContextFactory.Create();

        var connection = (SqlConnection)db.Database.GetDbConnection();

        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "dbo.usp_ApplyVoucher",
            connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add(
            "@BookingId", SqlDbType.BigInt).Value = bookingId;

        command.Parameters.Add(
            "@CustomerId", SqlDbType.BigInt).Value = customerId;

        command.Parameters.Add(
            "@VoucherCode", SqlDbType.NVarChar, 40).Value = voucherCode;

        var discount = command.Parameters.Add(
            "@DiscountAmount", SqlDbType.Decimal);

        discount.Precision = 18;
        discount.Scale = 2;
        discount.Direction = ParameterDirection.Output;

        await command.ExecuteNonQueryAsync();

        return discount.Value == DBNull.Value
            ? 0m
            : Convert.ToDecimal(discount.Value);
    }
}
