using CRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Data;

public partial class CrmsContext
{
    public DbSet<SearchSetting> SearchSettings => Set<SearchSetting>();
    public DbSet<SearchCity> Cities => Set<SearchCity>();
    public DbSet<RentalLocation> RentalLocations => Set<RentalLocation>();
    public DbSet<VehicleOwner> VehicleOwners => Set<VehicleOwner>();
    public DbSet<VehicleSearchProfile> VehicleSearchProfiles => Set<VehicleSearchProfile>();
    public DbSet<VehicleRentalMode> VehicleRentalModes => Set<VehicleRentalMode>();
    public DbSet<VehicleRentalPolicy> VehicleRentalPolicies => Set<VehicleRentalPolicy>();
    public DbSet<VehicleHourlyRate> VehicleHourlyRates => Set<VehicleHourlyRate>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<VehicleAmenity> VehicleAmenities => Set<VehicleAmenity>();
    public DbSet<RentalPurpose> RentalPurposes => Set<RentalPurpose>();
    public DbSet<VehicleRentalPurpose> VehicleRentalPurposes => Set<VehicleRentalPurpose>();
    public DbSet<VehicleTravelArea> VehicleTravelAreas => Set<VehicleTravelArea>();
    public DbSet<VehicleDeliveryArea> VehicleDeliveryAreas => Set<VehicleDeliveryArea>();
    public DbSet<VehicleDriverRoute> VehicleDriverRoutes => Set<VehicleDriverRoute>();
    public DbSet<VehicleVoucherEligibility> VehicleVoucherEligibility => Set<VehicleVoucherEligibility>();
    public DbSet<VehicleCalendarBlock> VehicleCalendarBlocks => Set<VehicleCalendarBlock>();
    public DbSet<BookingOfferSnapshot> BookingOfferSnapshots => Set<BookingOfferSnapshot>();
    public DbSet<SearchOwnerRating> SearchOwnerRatings => Set<SearchOwnerRating>();
    public DbSet<SearchCatalogRow> SearchCatalogRows => Set<SearchCatalogRow>();
    public IQueryable<SearchAvailableVehicle> SearchAvailableVehicles(DateTime startUtc, DateTime endUtc, DateTime nowUtc)
        => FromExpression(() => SearchAvailableVehicles(startUtc, endUtc, nowUtc));
    public IQueryable<SearchDeliveryQuote> SearchDeliveryQuotes(int cityId, double latitude, double longitude, string rentalMode)
        => FromExpression(() => SearchDeliveryQuotes(cityId, latitude, longitude, rentalMode));

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.Property(e => e.PriceUnit).HasMaxLength(5).IsUnicode(false).HasDefaultValue("Day");
            entity.Property(e => e.RentalUnits).HasPrecision(10, 2);
            entity.Property(e => e.UnitRate).HasPrecision(18, 2);
        });
        modelBuilder.Entity<SearchSetting>(entity =>
        {
            entity.ToTable("SearchSettings", "dbo");
            entity.HasKey(e => new { e.Id });
            entity.Property(e => e.MinimumOwnerRating).HasPrecision(3, 2);
        });
        modelBuilder.Entity<SearchCity>(entity =>
        {
            entity.ToTable("Cities", "dbo");
            entity.HasKey(e => new { e.CityId });
            entity.Property(e => e.CityKey).HasMaxLength(40).IsUnicode(false).IsRequired();
            entity.Property(e => e.CityName).HasMaxLength(120).IsRequired();
            entity.Property(e => e.CountryCode).HasMaxLength(2).IsUnicode(false).IsRequired();
            entity.Property(e => e.SourceReference).HasMaxLength(500);
        });
        modelBuilder.Entity<RentalLocation>(entity =>
        {
            entity.ToTable("RentalLocations", "dbo");
            entity.HasKey(e => new { e.LocationId });
            entity.Property(e => e.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(e => e.AddressLine).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Latitude).HasPrecision(9, 6);
            entity.Property(e => e.Longitude).HasPrecision(9, 6);
        });
        modelBuilder.Entity<VehicleOwner>(entity =>
        {
            entity.ToTable("VehicleOwners", "dbo");
            entity.HasKey(e => new { e.OwnerId });
            entity.Property(e => e.OwnerType).HasMaxLength(10).IsUnicode(false).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(e => e.ContactPhone).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.VerifiedAt).HasPrecision(0);
            entity.Property(e => e.CreatedAt).HasPrecision(0);
        });
        modelBuilder.Entity<VehicleSearchProfile>(entity =>
        {
            entity.ToTable("VehicleSearchProfiles", "dbo");
            entity.HasKey(e => new { e.VehicleId });
            entity.Property(e => e.VerifiedAt).HasPrecision(0);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
            entity.Property(e => e.RowVersion).IsRowVersion();
        });
        modelBuilder.Entity<VehicleRentalMode>(entity =>
        {
            entity.ToTable("VehicleRentalModes", "dbo");
            entity.HasKey(e => new { e.VehicleId, e.RentalMode });
            entity.Property(e => e.RentalMode).HasMaxLength(12).IsUnicode(false).IsRequired();
            entity.Property(e => e.VerifiedAt).HasPrecision(0);
        });
        modelBuilder.Entity<VehicleRentalPolicy>(entity =>
        {
            entity.ToTable("VehicleRentalPolicies", "dbo");
            entity.HasKey(e => new { e.VehicleId, e.RentalMode });
            entity.Property(e => e.RentalMode).HasMaxLength(12).IsUnicode(false).IsRequired();
            entity.Property(e => e.CollateralAmount).HasPrecision(18, 2);
            entity.Property(e => e.CollateralDescription).HasMaxLength(1000);
            entity.Property(e => e.MileagePeriod).HasMaxLength(8).IsUnicode(false);
            entity.Property(e => e.ExcessKmFee).HasPrecision(18, 2);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
            entity.Property(e => e.RowVersion).IsRowVersion();
        });
        modelBuilder.Entity<VehicleHourlyRate>(entity =>
        {
            entity.ToTable("VehicleHourlyRates", "dbo");
            entity.HasKey(e => new { e.VehicleId, e.RentalMode });
            entity.Property(e => e.RentalMode).HasMaxLength(12).IsUnicode(false).IsRequired();
            entity.Property(e => e.HourlyRate).HasPrecision(18, 2);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
        });
        modelBuilder.Entity<Amenity>(entity =>
        {
            entity.ToTable("Amenities", "dbo");
            entity.HasKey(e => new { e.AmenityId });
            entity.Property(e => e.AmenityCode).HasMaxLength(40).IsUnicode(false).IsRequired();
            entity.Property(e => e.AmenityName).HasMaxLength(100).IsRequired();
        });
        modelBuilder.Entity<VehicleAmenity>(entity =>
        {
            entity.ToTable("VehicleAmenities", "dbo");
            entity.HasKey(e => new { e.VehicleId, e.RentalMode, e.AmenityId });
            entity.Property(e => e.RentalMode).HasMaxLength(12).IsUnicode(false).IsRequired();
            entity.Property(e => e.AdditionalFee).HasPrecision(18, 2);
            entity.Property(e => e.FeeUnit).HasMaxLength(8).IsUnicode(false).IsRequired();
        });
        modelBuilder.Entity<RentalPurpose>(entity =>
        {
            entity.ToTable("RentalPurposes", "dbo");
            entity.HasKey(e => new { e.PurposeId });
            entity.Property(e => e.PurposeCode).HasMaxLength(40).IsUnicode(false).IsRequired();
            entity.Property(e => e.PurposeName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Definition).HasMaxLength(500).IsRequired();
        });
        modelBuilder.Entity<VehicleRentalPurpose>(entity =>
        {
            entity.ToTable("VehicleRentalPurposes", "dbo");
            entity.HasKey(e => new { e.VehicleId, e.RentalMode, e.PurposeId });
            entity.Property(e => e.RentalMode).HasMaxLength(12).IsUnicode(false).IsRequired();
            entity.Property(e => e.VerifiedAt).HasPrecision(0);
        });
        modelBuilder.Entity<VehicleTravelArea>(entity =>
        {
            entity.ToTable("VehicleTravelAreas", "dbo");
            entity.HasKey(e => new { e.VehicleId, e.RentalMode, e.CityId });
            entity.Property(e => e.RentalMode).HasMaxLength(12).IsUnicode(false).IsRequired();
        });
        modelBuilder.Entity<VehicleDeliveryArea>(entity =>
        {
            entity.ToTable("VehicleDeliveryAreas", "dbo");
            entity.HasKey(e => new { e.VehicleId, e.RentalMode, e.CityId });
            entity.Property(e => e.RentalMode).HasMaxLength(12).IsUnicode(false).IsRequired();
            entity.Property(e => e.FeeModel).HasMaxLength(8).IsUnicode(false).IsRequired();
            entity.Property(e => e.FlatFee).HasPrecision(18, 2);
            entity.Property(e => e.FeePerKm).HasPrecision(18, 2);
            entity.Property(e => e.MinimumFee).HasPrecision(18, 2);
            entity.Property(e => e.ServiceRadiusKm).HasPrecision(7, 2);
            entity.Property(e => e.FreeWithinKm).HasPrecision(7, 2);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
        });
        modelBuilder.Entity<VehicleDriverRoute>(entity =>
        {
            entity.ToTable("VehicleDriverRoutes", "dbo");
            entity.HasKey(e => new { e.RouteId });
            entity.Property(e => e.RentalMode).HasMaxLength(12).IsUnicode(false).IsRequired();
            entity.Property(e => e.RouteType).HasMaxLength(20).IsUnicode(false).IsRequired();
            entity.Property(e => e.PricingModel).HasMaxLength(12).IsUnicode(false).IsRequired();
            entity.Property(e => e.TripBasePrice).HasPrecision(18, 2);
            entity.Property(e => e.ExcessKmFee).HasPrecision(18, 2);
            entity.Property(e => e.ExcessHourFee).HasPrecision(18, 2);
        });
        modelBuilder.Entity<VehicleVoucherEligibility>(entity =>
        {
            entity.ToTable("VehicleVoucherEligibility", "dbo");
            entity.HasKey(e => new { e.VehicleId, e.RentalMode, e.VoucherId });
            entity.Property(e => e.RentalMode).HasMaxLength(12).IsUnicode(false).IsRequired();
        });
        modelBuilder.Entity<VehicleCalendarBlock>(entity =>
        {
            entity.ToTable("VehicleCalendarBlocks", "dbo", table => table.UseSqlOutputClause(false));
            entity.HasKey(e => new { e.BlockId });
            entity.Property(e => e.StartAt).HasPrecision(0);
            entity.Property(e => e.EndAt).HasPrecision(0);
            entity.Property(e => e.Reason).HasMaxLength(500).IsRequired();
            entity.Property(e => e.CreatedAt).HasPrecision(0);
        });
        modelBuilder.Entity<BookingOfferSnapshot>(entity =>
        {
            entity.ToTable("BookingOfferSnapshots", "dbo", table => table.UseSqlOutputClause(false));
            entity.HasKey(e => new { e.BookingId });
            entity.Property(e => e.RentalMode).HasMaxLength(12).IsUnicode(false).IsRequired();
            entity.Property(e => e.PriceUnit).HasMaxLength(5).IsUnicode(false).IsRequired();
            entity.Property(e => e.PolicySnapshotJson).IsRequired();
            entity.Property(e => e.CreatedAt).HasPrecision(0);
        });
        modelBuilder.Entity<SearchOwnerRating>(entity =>
        {
            entity.HasNoKey().ToView("vSearchOwnerRatings", "dbo");
            entity.Property(e => e.AverageRating).HasPrecision(10, 4);
        });
        modelBuilder.Entity<SearchCatalogRow>(entity =>
        {
            entity.HasNoKey().ToView("vVehicleSearchCatalog", "dbo");
            entity.Property(e => e.CategoryName).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Brand).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Model).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Transmission).HasMaxLength(10).IsUnicode(false).IsRequired();
            entity.Property(e => e.FuelType).HasMaxLength(12).IsUnicode(false).IsRequired();
            entity.Property(e => e.DailyRate).HasPrecision(18, 2);
            entity.Property(e => e.RentalMode).HasMaxLength(12).IsUnicode(false).IsRequired();
            entity.Property(e => e.OwnerName).HasMaxLength(160).IsRequired();
            entity.Property(e => e.CityName).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Latitude).HasPrecision(9, 6);
            entity.Property(e => e.Longitude).HasPrecision(9, 6);
            entity.Property(e => e.BaseMonthlyRate).HasPrecision(18, 2);
            entity.Property(e => e.MonthlyRate).HasPrecision(18, 2);
            entity.Property(e => e.DiscountPercent).HasPrecision(5, 2);
            entity.Property(e => e.SearchUnitRate).HasPrecision(18, 2);
            entity.Property(e => e.SearchPriceUnit).HasMaxLength(5).IsUnicode(false).IsRequired();
            entity.Property(e => e.CollateralAmount).HasPrecision(18, 2);
            entity.Property(e => e.MileagePeriod).HasMaxLength(8).IsUnicode(false);
            entity.Property(e => e.ExcessKmFee).HasPrecision(18, 2);
            entity.Property(e => e.HourlyRate).HasPrecision(18, 2);
            entity.Property(e => e.AverageRating).HasPrecision(10, 4);
        });
        modelBuilder.Entity<SearchAvailableVehicle>().HasNoKey();
        modelBuilder.Entity<SearchDeliveryQuote>().HasNoKey();
        modelBuilder.Entity<SearchDeliveryQuote>().Property(q => q.DeliveryAndCollectionFee).HasPrecision(18, 2);
        modelBuilder.HasDbFunction(typeof(CrmsContext).GetMethod(nameof(SearchAvailableVehicles))!).HasName("ufn_SearchAvailableVehicles").HasSchema("dbo");
        modelBuilder.HasDbFunction(typeof(CrmsContext).GetMethod(nameof(SearchDeliveryQuotes))!).HasName("ufn_SearchDeliveryQuotes").HasSchema("dbo");
    }
}
