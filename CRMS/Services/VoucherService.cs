
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CRMS.Models;
using CRMS.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Services;

public class VoucherService
{
    private readonly VoucherRepository _repository;

    public VoucherService(VoucherRepository repository)
    {
        _repository = repository;
    }

    private static void RequireAdmin()
    {
        var user = CurrentUserService.Instance;

        if (!user.IsLoggedIn || !user.IsAdmin ||
            user.UserId == null)
        {
            throw new UnauthorizedAccessException(
                "Chi Admin moi co quyen quan ly Voucher.");
        }
    }

    public Task<List<Voucher>> GetAllAsync()
    {
        RequireAdmin();
        return _repository.GetAllAsync();
    }

    public Task<Voucher?> GetByIdAsync(long id)
    {
        RequireAdmin();
        return _repository.GetByIdAsync(id);
    }

    private static void Validate(Voucher voucher)
    {
        if (voucher == null)
            throw new ArgumentNullException(nameof(voucher));

        if (string.IsNullOrWhiteSpace(voucher.VoucherCode))
            throw new ArgumentException(
                "Ma Voucher khong duoc de trong.");

        voucher.VoucherCode = voucher.VoucherCode.Trim();

        if (voucher.VoucherCode.Length > 40)
            throw new ArgumentException(
                "Ma Voucher toi da 40 ky tu.");

        if (voucher.DiscountType != "Percent" &&
            voucher.DiscountType != "Fixed")
            throw new ArgumentException(
                "Loai giam gia khong hop le.");

        if (voucher.DiscountValue <= 0)
            throw new ArgumentException(
                "Gia tri giam phai lon hon 0.");

        if (voucher.DiscountType == "Percent" &&
            voucher.DiscountValue > 100)
            throw new ArgumentException(
                "Phan tram giam khong duoc qua 100.");

        if (voucher.MinimumOrderAmount < 0)
            throw new ArgumentException(
                "Gia tri don toi thieu khong hop le.");

        if (voucher.MaxDiscountAmount.HasValue &&
            voucher.MaxDiscountAmount.Value <= 0)
            throw new ArgumentException(
                "Muc giam toi da phai lon hon 0.");

        if (voucher.StartsAt >= voucher.EndsAt)
            throw new ArgumentException(
                "Ngay ket thuc phai sau ngay bat dau.");

        if (voucher.UsageLimit.HasValue &&
            voucher.UsageLimit.Value <= 0)
            throw new ArgumentException(
                "Gioi han su dung phai lon hon 0.");

        if (voucher.PerCustomerLimit <= 0)
            throw new ArgumentException(
                "Gioi han moi khach phai lon hon 0.");
    }

    public async Task AddAsync(Voucher voucher)
    {
        RequireAdmin();
        Validate(voucher);

        if (await _repository.CodeExistsAsync(
            voucher.VoucherCode))
            throw new InvalidOperationException(
                "Ma Voucher da ton tai.");

        RequireAdmin();

        try
        {
            await _repository.AddAsync(voucher);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException(
                "Khong the them Voucher. " +
                "Hay kiem tra du lieu hoac ma trung.");
        }
    }

    public async Task UpdateAsync(Voucher voucher)
    {
        RequireAdmin();
        Validate(voucher);

        if (await _repository.CodeExistsAsync(
            voucher.VoucherCode, voucher.VoucherId))
            throw new InvalidOperationException(
                "Ma Voucher da ton tai.");

        RequireAdmin();

        try
        {
            await _repository.UpdateAsync(voucher);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "Voucher da duoc thay doi boi nguoi khac. " +
                "Hay tai lai du lieu.");
        }
    }

    public async Task SetActiveAsync(
        long id, bool isActive, byte[] rowVersion)
    {
        RequireAdmin();

        try
        {
            await _repository.SetActiveAsync(
                id, isActive, rowVersion);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "Trang thai Voucher da thay doi. " +
                "Hay tai lai danh sach.");
        }
    }
}
