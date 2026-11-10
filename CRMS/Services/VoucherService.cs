
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRMS.Data;
using CRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Services;

public class VoucherService
{
    // Constructor khong tham so, khong phu thuoc Repository.
    public VoucherService()
    {
    }

    // Kiem tra quyen Admin
    private static void RequireAdmin()
    {
        var user = CurrentUserService.Instance;

        if (!user.IsLoggedIn ||
            !user.IsAdmin ||
            user.UserId == null)
        {
            throw new UnauthorizedAccessException(
                "Chi Admin moi co quyen quan ly Voucher.");
        }
    }

    // F20 - Lay danh sach Voucher
    public async Task<List<Voucher>> GetAllAsync()
    {
        RequireAdmin();

        await using var db = CrmsContextFactory.Create();

        return await db.Vouchers
            .AsNoTracking()
            .OrderByDescending(v => v.VoucherId)
            .ToListAsync();
    }

    // F20 - Tim Voucher theo ID
    public async Task<Voucher?> GetByIdAsync(long id)
    {
        RequireAdmin();

        await using var db = CrmsContextFactory.Create();

        return await db.Vouchers
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.VoucherId == id);
    }

    // Kiem tra ma Voucher trung
    private static async Task<bool> CodeExistsAsync(
        CrmsContext db,
        string code,
        long? excludeId = null)
    {
        return await db.Vouchers.AnyAsync(v =>
            v.VoucherCode == code &&
            (!excludeId.HasValue ||
             v.VoucherId != excludeId.Value));
    }

    // Kiem tra du lieu Voucher
    private static void Validate(Voucher voucher)
    {
        ArgumentNullException.ThrowIfNull(voucher);

        if (string.IsNullOrWhiteSpace(voucher.VoucherCode))
        {
            throw new ArgumentException(
                "Ma Voucher khong duoc de trong.");
        }

        voucher.VoucherCode = voucher.VoucherCode.Trim();

        if (voucher.VoucherCode.Length > 40)
        {
            throw new ArgumentException(
                "Ma Voucher toi da 40 ky tu.");
        }

        if (voucher.DiscountType != "Percent" &&
            voucher.DiscountType != "Fixed")
        {
            throw new ArgumentException(
                "Loai giam gia khong hop le.");
        }

        if (voucher.DiscountValue <= 0)
        {
            throw new ArgumentException(
                "Gia tri giam phai lon hon 0.");
        }

        if (voucher.DiscountType == "Percent" &&
            voucher.DiscountValue > 100)
        {
            throw new ArgumentException(
                "Phan tram giam khong duoc qua 100.");
        }

        if (voucher.MinimumOrderAmount < 0)
        {
            throw new ArgumentException(
                "Gia tri don toi thieu khong hop le.");
        }

        if (voucher.MaxDiscountAmount.HasValue &&
            voucher.MaxDiscountAmount.Value <= 0)
        {
            throw new ArgumentException(
                "Muc giam toi da phai lon hon 0.");
        }

        if (voucher.StartsAt >= voucher.EndsAt)
        {
            throw new ArgumentException(
                "Ngay ket thuc phai sau ngay bat dau.");
        }

        if (voucher.UsageLimit.HasValue &&
            voucher.UsageLimit.Value <= 0)
        {
            throw new ArgumentException(
                "Gioi han su dung phai lon hon 0.");
        }

        if (voucher.PerCustomerLimit <= 0)
        {
            throw new ArgumentException(
                "Gioi han moi khach phai lon hon 0.");
        }
    }

    // F20 - Them Voucher
    public async Task AddAsync(Voucher voucher)
    {
        RequireAdmin();
        Validate(voucher);

        await using var db = CrmsContextFactory.Create();

        if (await CodeExistsAsync(db, voucher.VoucherCode))
        {
            throw new InvalidOperationException(
                "Ma Voucher da ton tai.");
        }

        RequireAdmin();

        try
        {
            db.Vouchers.Add(voucher);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException(
                "Khong the them Voucher. " +
                "Hay kiem tra du lieu hoac ma trung.");
        }
    }

    // F20 - Cap nhat Voucher
    public async Task UpdateAsync(Voucher voucher)
    {
        RequireAdmin();
        Validate(voucher);

        await using var db = CrmsContextFactory.Create();

        if (await CodeExistsAsync(
            db,
            voucher.VoucherCode,
            voucher.VoucherId))
        {
            throw new InvalidOperationException(
                "Ma Voucher da ton tai.");
        }

        var existing = await db.Vouchers
            .FirstOrDefaultAsync(
                v => v.VoucherId == voucher.VoucherId);

        if (existing == null)
        {
            throw new InvalidOperationException(
                "Khong tim thay Voucher.");
        }

        db.Entry(existing)
            .Property(v => v.RowVersion)
            .OriginalValue = voucher.RowVersion;

        existing.VoucherCode = voucher.VoucherCode;
        existing.DiscountType = voucher.DiscountType;
        existing.DiscountValue = voucher.DiscountValue;
        existing.MaxDiscountAmount = voucher.MaxDiscountAmount;
        existing.MinimumOrderAmount = voucher.MinimumOrderAmount;
        existing.StartsAt = voucher.StartsAt;
        existing.EndsAt = voucher.EndsAt;
        existing.UsageLimit = voucher.UsageLimit;
        existing.PerCustomerLimit = voucher.PerCustomerLimit;
        existing.IsActive = voucher.IsActive;

        RequireAdmin();

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "Voucher da duoc thay doi boi nguoi khac. " +
                "Hay tai lai du lieu.");
        }
    }

    // F20 - Kich hoat / Vo hieu hoa Voucher
    public async Task SetActiveAsync(
        long id,
        bool isActive,
        byte[] rowVersion)
    {
        RequireAdmin();

        await using var db = CrmsContextFactory.Create();

        var voucher = await db.Vouchers
            .FirstOrDefaultAsync(v => v.VoucherId == id);

        if (voucher == null)
        {
            throw new InvalidOperationException(
                "Khong tim thay Voucher.");
        }

        db.Entry(voucher)
            .Property(v => v.RowVersion)
            .OriginalValue = rowVersion;

        voucher.IsActive = isActive;

        RequireAdmin();

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "Trang thai Voucher da thay doi. " +
                "Hay tai lai danh sach.");
        }
    }
}
