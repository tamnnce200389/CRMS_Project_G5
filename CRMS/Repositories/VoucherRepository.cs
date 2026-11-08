
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRMS.Data;
using CRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Repositories;

public class VoucherRepository
{
    // Giữ constructor cũ để tương thích ViewModel.
    // Không giữ DbContext lâu dài.
    public VoucherRepository(CrmsContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
    }

    public async Task<List<Voucher>> GetAllAsync()
    {
        await using var db = CrmsContextFactory.Create();

        return await db.Vouchers
            .AsNoTracking()
            .OrderByDescending(v => v.VoucherId)
            .ToListAsync();
    }

    public async Task<Voucher?> GetByIdAsync(long id)
    {
        await using var db = CrmsContextFactory.Create();

        return await db.Vouchers
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.VoucherId == id);
    }

    public async Task<bool> CodeExistsAsync(
        string code, long? excludeId = null)
    {
        await using var db = CrmsContextFactory.Create();

        return await db.Vouchers.AnyAsync(v =>
            v.VoucherCode == code &&
            (!excludeId.HasValue ||
             v.VoucherId != excludeId.Value));
    }

    public async Task AddAsync(Voucher voucher)
    {
        await using var db = CrmsContextFactory.Create();

        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Voucher voucher)
    {
        await using var db = CrmsContextFactory.Create();

        var existing = await db.Vouchers
            .FirstOrDefaultAsync(
                v => v.VoucherId == voucher.VoucherId);

        if (existing == null)
            throw new InvalidOperationException(
                "Không tìm thấy Voucher.");

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

        await db.SaveChangesAsync();
    }

    public async Task SetActiveAsync(
        long id, bool isActive, byte[] rowVersion)
    {
        await using var db = CrmsContextFactory.Create();

        var voucher = await db.Vouchers
            .FirstOrDefaultAsync(v => v.VoucherId == id);

        if (voucher == null)
            throw new InvalidOperationException(
                "Không tìm thấy Voucher.");

        db.Entry(voucher)
            .Property(v => v.RowVersion)
            .OriginalValue = rowVersion;

        voucher.IsActive = isActive;

        await db.SaveChangesAsync();
    }
}
