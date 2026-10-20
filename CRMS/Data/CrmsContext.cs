using System;
using System.Collections.Generic;
using CRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Data;

public partial class CrmsContext : DbContext
{
    public CrmsContext(DbContextOptions<CrmsContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Booking> Bookings { get; set; }

    public virtual DbSet<BookingStatusHistory> BookingStatusHistories { get; set; }

    public virtual DbSet<BookingVoucher> BookingVouchers { get; set; }

    public virtual DbSet<Driver> Drivers { get; set; }

    public virtual DbSet<MaintenanceRecord> MaintenanceRecords { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<RentalPolicy> RentalPolicies { get; set; }

    public virtual DbSet<Review> Reviews { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Vehicle> Vehicles { get; set; }

    public virtual DbSet<VehicleCategory> VehicleCategories { get; set; }

    public virtual DbSet<VehicleHandover> VehicleHandovers { get; set; }

    public virtual DbSet<VehicleImage> VehicleImages { get; set; }

    public virtual DbSet<VehicleReturn> VehicleReturns { get; set; }

    public virtual DbSet<Voucher> Vouchers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.BookingId).HasName("PK__Bookings__73951AED1278161A");

            entity.HasIndex(e => new { e.CustomerId, e.CreatedAt }, "IX_Bookings_Customer").IsDescending(false, true);

            entity.HasIndex(e => new { e.DriverId, e.StartAt, e.EndAt }, "IX_Bookings_DriverRange");

            entity.HasIndex(e => e.DepositDeadline, "IX_Bookings_PendingExpiry");

            entity.HasIndex(e => new { e.VehicleId, e.StartAt, e.EndAt }, "IX_Bookings_VehicleRange");

            entity.HasIndex(e => e.BookingCode, "UX_Bookings_Code").IsUnique();

            entity.HasIndex(e => e.RequestKey, "UX_Bookings_RequestKey").IsUnique();

            entity.Property(e => e.BookingCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.BookingStatus)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.Property(e => e.CancelledAt).HasPrecision(0);
            entity.Property(e => e.ConfirmedAt).HasPrecision(0);
            entity.Property(e => e.CreatedAt).HasPrecision(0);
            entity.Property(e => e.DepositAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DepositDeadline).HasPrecision(0);
            entity.Property(e => e.DepositMethod)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DriverAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DriverDailyRate).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.EndAt).HasPrecision(0);
            entity.Property(e => e.RentalType)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.StartAt).HasPrecision(0);
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
            entity.Property(e => e.VehicleAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.VehicleDailyRate).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Customer).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Bookings_Customer");

            entity.HasOne(d => d.Driver).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.DriverId)
                .HasConstraintName("FK_Bookings_Driver");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Bookings_Vehicle");
        });

        modelBuilder.Entity<BookingStatusHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId).HasName("PK__BookingS__4D7B4ABD9BCDA700");

            entity.ToTable("BookingStatusHistory");

            entity.HasIndex(e => new { e.BookingId, e.ChangedAt }, "IX_History_BookingTime").IsDescending(false, true);

            entity.Property(e => e.ChangeNote).HasMaxLength(500);
            entity.Property(e => e.ChangedAt).HasPrecision(0);
            entity.Property(e => e.NewStatus)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.OldStatus)
                .HasMaxLength(16)
                .IsUnicode(false);

            entity.HasOne(d => d.Booking).WithMany(p => p.BookingStatusHistories)
                .HasForeignKey(d => d.BookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_History_Booking");

            entity.HasOne(d => d.ChangedByUser).WithMany(p => p.BookingStatusHistories)
                .HasForeignKey(d => d.ChangedByUserId)
                .HasConstraintName("FK_History_User");
        });

        modelBuilder.Entity<BookingVoucher>(entity =>
        {
            entity.HasKey(e => e.BookingId).HasName("PK__BookingV__73951AED0703C23A");

            entity.HasIndex(e => new { e.VoucherId, e.RedemptionStatus }, "IX_BookingVoucher_Usage");

            entity.Property(e => e.BookingId).ValueGeneratedNever();
            entity.Property(e => e.AppliedAt).HasPrecision(0);
            entity.Property(e => e.CreatedAt).HasPrecision(0);
            entity.Property(e => e.DiscountSnapshot).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.RedemptionStatus)
                .HasMaxLength(10)
                .IsUnicode(false);

            entity.HasOne(d => d.Booking).WithOne(p => p.BookingVoucher)
                .HasForeignKey<BookingVoucher>(d => d.BookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BookingVoucher_Booking");

            entity.HasOne(d => d.Voucher).WithMany(p => p.BookingVouchers)
                .HasForeignKey(d => d.VoucherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BookingVoucher_Voucher");
        });

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.HasKey(e => e.DriverId).HasName("PK__Drivers__F1B1CD0457E56F62");

            entity.HasIndex(e => e.LicenseNumber, "UX_Drivers_LicenseNumber").IsUnique();

            entity.Property(e => e.FullName).HasMaxLength(120);
            entity.Property(e => e.LicenseNumber).HasMaxLength(40);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MaintenanceRecord>(entity =>
        {
            entity.HasKey(e => e.MaintenanceId).HasName("PK__Maintena__E60542D55334ED9E");

            entity.HasIndex(e => new { e.VehicleId, e.StartAt, e.EndAt }, "IX_Maintenance_VehicleRange");

            entity.Property(e => e.Cost).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Description).HasMaxLength(1500);
            entity.Property(e => e.EndAt).HasPrecision(0);
            entity.Property(e => e.MaintenanceStatus)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.StartAt).HasPrecision(0);

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.MaintenanceRecords)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Maintenance_User");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.MaintenanceRecords)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Maintenance_Vehicle");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("PK__Payments__9B556A383ACC23C1");

            entity.HasIndex(e => new { e.BookingId, e.CreatedAt }, "IX_Payments_Booking").IsDescending(false, true);

            entity.HasIndex(e => e.GatewayTxnRef, "UX_Payments_GatewayRef").IsUnique();

            entity.HasIndex(e => e.GatewayTransactionNo, "UX_Payments_GatewayTxnNo").IsUnique();

            entity.HasIndex(e => e.IdempotencyKey, "UX_Payments_Idempotency").IsUnique();

            entity.HasIndex(e => e.BookingId, "UX_Payments_OneDepositSuccess").IsUnique();

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasPrecision(0);
            entity.Property(e => e.GatewayResponseCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.GatewayTransactionNo)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.GatewayTxnRef)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.PaidAt).HasPrecision(0);
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.PaymentType)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(d => d.Booking).WithOne(p => p.Payment)
                .HasForeignKey<Payment>(d => d.BookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payments_Booking");

            entity.HasOne(d => d.VerifiedByUser).WithMany(p => p.Payments)
                .HasForeignKey(d => d.VerifiedByUserId)
                .HasConstraintName("FK_Payments_Verifier");
        });

        modelBuilder.Entity<RentalPolicy>(entity =>
        {
            entity.HasKey(e => e.PolicyId);

            entity.Property(e => e.DepositPercent).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.DriverDailyFee).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.ReviewId).HasName("PK__Reviews__74BC79CE28A458EB");

            entity.HasIndex(e => e.BookingId, "UX_Reviews_Booking").IsUnique();

            entity.Property(e => e.Comment).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasPrecision(0);

            entity.HasOne(d => d.Booking).WithOne(p => p.Review)
                .HasForeignKey<Review>(d => d.BookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reviews_Booking");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4CBCC786F1");

            entity.HasIndex(e => e.Email, "UX_Users_Email").IsUnique();

            entity.Property(e => e.CreatedAt).HasPrecision(0);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.FullName).HasMaxLength(120);
            entity.Property(e => e.PasswordHash).HasMaxLength(500);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
            entity.Property(e => e.UserRole)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(e => e.VehicleId).HasName("PK__Vehicles__476B54923572B626");

            entity.HasIndex(e => new { e.CategoryId, e.OperationalStatus, e.DailyRate }, "IX_Vehicles_Filter");

            entity.HasIndex(e => e.LicensePlate, "UX_Vehicles_Plate").IsUnique();

            entity.Property(e => e.Brand).HasMaxLength(80);
            entity.Property(e => e.CreatedAt).HasPrecision(0);
            entity.Property(e => e.DailyRate).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.FuelType)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.LicensePlate)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Model).HasMaxLength(100);
            entity.Property(e => e.OperationalStatus)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Transmission)
                .HasMaxLength(10)
                .IsUnicode(false);

            entity.HasOne(d => d.Category).WithMany(p => p.Vehicles)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Vehicles_Categories");
        });

        modelBuilder.Entity<VehicleCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__VehicleC__19093A0B1F62DE44");

            entity.HasIndex(e => e.CategoryName, "UX_Categories_Name").IsUnique();

            entity.Property(e => e.CategoryName).HasMaxLength(80);
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<VehicleHandover>(entity =>
        {
            entity.HasKey(e => e.HandoverId).HasName("PK__VehicleH__DB2A1F816305B0E0");

            entity.HasIndex(e => e.BookingId, "UX_Handover_Booking").IsUnique();

            entity.Property(e => e.ConditionNotes).HasMaxLength(2000);
            entity.Property(e => e.HandedOverAt).HasPrecision(0);

            entity.HasOne(d => d.Booking).WithOne(p => p.VehicleHandover)
                .HasForeignKey<VehicleHandover>(d => d.BookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Handover_Booking");

            entity.HasOne(d => d.Staff).WithMany(p => p.VehicleHandovers)
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Handover_Staff");
        });

        modelBuilder.Entity<VehicleImage>(entity =>
        {
            entity.HasKey(e => e.ImageId).HasName("PK__VehicleI__7516F70C8A19E48F");

            entity.HasIndex(e => new { e.VehicleId, e.SortOrder }, "IX_Images_Vehicle");

            entity.HasIndex(e => e.VehicleId, "UX_Images_OnePrimary").IsUnique();

            entity.Property(e => e.ImageUrl).HasMaxLength(1000);

            entity.HasOne(d => d.Vehicle).WithOne(p => p.VehicleImage)
                .HasForeignKey<VehicleImage>(d => d.VehicleId)
                .HasConstraintName("FK_Images_Vehicle");
        });

        modelBuilder.Entity<VehicleReturn>(entity =>
        {
            entity.HasKey(e => e.ReturnId).HasName("PK__VehicleR__F445E9A8C6B8E660");

            entity.HasIndex(e => e.BookingId, "UX_Returns_Booking").IsUnique();

            entity.Property(e => e.DamageFee).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InspectionNotes).HasMaxLength(2000);
            entity.Property(e => e.LateFee).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OtherFee).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ReturnedAt).HasPrecision(0);

            entity.HasOne(d => d.Booking).WithOne(p => p.VehicleReturn)
                .HasForeignKey<VehicleReturn>(d => d.BookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Returns_Booking");

            entity.HasOne(d => d.Staff).WithMany(p => p.VehicleReturns)
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Returns_Staff");
        });

        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(e => e.VoucherId).HasName("PK__Vouchers__3AEE7921F7EA9562");

            entity.HasIndex(e => e.VoucherCode, "UX_Vouchers_Code").IsUnique();

            entity.Property(e => e.DiscountType)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.DiscountValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.EndsAt).HasPrecision(0);
            entity.Property(e => e.MaxDiscountAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MinimumOrderAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.StartsAt).HasPrecision(0);
            entity.Property(e => e.VoucherCode).HasMaxLength(40);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
