using CRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Data;

public partial class CrmsContext
{
    public DbSet<DemoPayment> DemoPayments => Set<DemoPayment>();

    private static void ConfigureDemoPayments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("Bookings", "dbo", table => table.UseSqlOutputClause(false));
            entity.Property(b => b.IsDemo).HasDefaultValue(false);
        });
        modelBuilder.Entity<DemoPayment>(entity =>
        {
            entity.ToTable("DemoPayments", "dbo");
            entity.HasKey(p => p.DemoPaymentId);
            entity.HasIndex(p => p.BookingId).IsUnique();
            entity.HasIndex(p => p.TransferContent).IsUnique();
            entity.HasIndex(p => p.BankTransactionReference).IsUnique().HasFilter("[BankTransactionReference] IS NOT NULL");
            entity.HasIndex(p => p.ConfirmationRequestKey).IsUnique().HasFilter("[ConfirmationRequestKey] IS NOT NULL");
            entity.Property(p => p.SimulatedDepositAmount).HasPrecision(18, 2);
            entity.Property(p => p.RequiredTransferAmount).HasPrecision(18, 2);
            entity.Property(p => p.ActualAmount).HasPrecision(18, 2);
            entity.Property(p => p.TransferContent).HasMaxLength(25).IsUnicode(false);
            entity.Property(p => p.BankBin).HasMaxLength(6).IsUnicode(false);
            entity.Property(p => p.BankName).HasMaxLength(80);
            entity.Property(p => p.AccountNumber).HasMaxLength(34).IsUnicode(false);
            entity.Property(p => p.AccountName).HasMaxLength(120);
            entity.Property(p => p.PaymentStatus).HasMaxLength(10).IsUnicode(false);
            entity.Property(p => p.BankTransactionReference).HasMaxLength(100);
            entity.Property(p => p.PaidAt).HasPrecision(0);
            entity.Property(p => p.CreatedAt).HasPrecision(0);
            entity.Property(p => p.RowVersion).IsRowVersion();
            entity.HasOne<Booking>().WithMany().HasForeignKey(p => p.BookingId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<User>().WithMany().HasForeignKey(p => p.VerifiedByUserId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
