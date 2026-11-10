/* CRMS - FULL CORRECTED SQL, 2026-11-05. SQL Server 2019+.
   Target: CRMS. Run in one SSMS query/connection, outside any existing transaction.
   Default: upgrade schema/procedures, preserve existing data; do NOT insert demo data.
   For a DEMO database only: change @IncludeDemoData from 0 to 1 below.
   Demo mode includes the original 29 cars + search metadata + synthetic QA financial history.
   The final section assigns the bundled exterior/interior photos to those 29 cars.
   On a new DEMO database, set @IncludeDemoData=1 to create the matching vehicles.
   QR schema update 2026-11-10: includes Bookings.IsDemo and DemoPayments for the Staff-verified 1,000 VND flow.
   This single script installs/upgrades the QR schema; no extra SQL file is required.
   Existing synthetic data is preserved, not automatically removed from reports.
   No login/password changes; no GRANT/db_owner requirement is introduced.
   For existing CRMS, migration identity needs ALTER/CREATE objects, DML and EXECUTE rights.
   A database must already exist for crms_user if that login cannot CREATE DATABASE.
   UI availability and hourly/monthly checkout still require their own C# integration.
*/
USE master;
GO
IF DB_ID(N'CRMS') IS NULL
BEGIN
    CREATE DATABASE [CRMS];
END;
GO
USE [CRMS];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @IncludeDemoData bit = 0; -- 0: existing database; 1: DEMO DATA ONLY
IF @@TRANCOUNT<>0 THROW 53900,'Run this full script outside an existing transaction.',1;
DECLARE @DemoAlreadyInstalled bit=CASE WHEN EXISTS(
 SELECT 1 FROM sys.extended_properties WHERE name=N'CRMS.QaScenariosVersion'
 AND (class=0 OR (class=1 AND major_id=OBJECT_ID(N'dbo.SearchSettings') AND minor_id=0))) THEN 1 ELSE 0 END;
BEGIN TRY
 BEGIN TRANSACTION;
 -- Refuse inconsistent existing calendars before changing any schema/procedure.
IF OBJECT_ID(N'dbo.VehicleCalendarBlocks',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Bookings',N'U') IS NOT NULL
EXEC(N'IF EXISTS(SELECT 1 FROM dbo.VehicleCalendarBlocks block JOIN dbo.Bookings b ON b.VehicleId=block.VehicleId
 WHERE block.IsActive=1 AND ((b.BookingStatus=''InUse'' AND b.StartAt<block.EndAt)
 OR (b.BookingStatus=''Confirmed'' AND b.StartAt<block.EndAt AND b.EndAt>block.StartAt)
 OR (b.BookingStatus=''PendingDeposit'' AND b.DepositDeadline>SYSUTCDATETIME() AND b.StartAt<block.EndAt AND b.EndAt>block.StartAt)))
 THROW 53901,''Existing active calendar/booking conflict. Review records; migration was rolled back.'',1;');
EXEC(N'-- ================================================================
-- 01. TABLES / INDEXES (original schema)
-- 02. STORED PROCEDURES (original logic, syntax fixed)
-- ================================================================
-- 1. USERS: Customer, Staff, Admin. Guest is an unauthenticated visitor.
IF OBJECT_ID(N''dbo.Users'', N''U'') IS NULL
BEGIN
    CREATE TABLE dbo.Users (
        UserId              BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        FullName            NVARCHAR(120) NOT NULL,
        Email               NVARCHAR(256) NOT NULL,
        Phone               VARCHAR(20) NULL,
        PasswordHash        NVARCHAR(500) NOT NULL, -- WPF AuthService uses Microsoft.Extensions.Identity.Core PasswordHasher
        UserRole            VARCHAR(10) NOT NULL CONSTRAINT DF_Users_UserRole DEFAULT (''Customer''),
        IsActive            BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
        CreatedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt           DATETIME2(0) NULL,
        RowVersion          ROWVERSION NOT NULL,
        CONSTRAINT CK_Users_Role CHECK (UserRole IN (''Customer'',''Staff'',''Admin''))
    );
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.Users'') AND name = N''UX_Users_Email'')
BEGIN
    CREATE UNIQUE INDEX UX_Users_Email ON dbo.Users(Email);
END;');
EXEC(N'-- 2. VEHICLE CATALOG
IF OBJECT_ID(N''dbo.VehicleCategories'', N''U'') IS NULL
BEGIN
    CREATE TABLE dbo.VehicleCategories (
        CategoryId          INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CategoryName        NVARCHAR(80) NOT NULL,
        Description         NVARCHAR(500) NULL,
        IsActive            BIT NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT (1)
    );
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.VehicleCategories'') AND name = N''UX_Categories_Name'')
BEGIN
    CREATE UNIQUE INDEX UX_Categories_Name ON dbo.VehicleCategories(CategoryName);
END;');
EXEC(N'IF OBJECT_ID(N''dbo.Vehicles'', N''U'') IS NULL
BEGIN
    CREATE TABLE dbo.Vehicles (
        VehicleId           BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CategoryId          INT NOT NULL,
        LicensePlate        VARCHAR(20) NOT NULL,
        Brand               NVARCHAR(80) NOT NULL,
        Model               NVARCHAR(100) NOT NULL,
        ManufactureYear     SMALLINT NOT NULL,
        SeatCount           TINYINT NOT NULL,
        Transmission        VARCHAR(10) NOT NULL,
        FuelType            VARCHAR(12) NOT NULL,
        DailyRate           DECIMAL(18,2) NOT NULL,
        OdometerKm          INT NOT NULL CONSTRAINT DF_Vehicles_Odometer DEFAULT (0),
        OperationalStatus   VARCHAR(12) NOT NULL CONSTRAINT DF_Vehicles_Status DEFAULT (''Active''),
        Description         NVARCHAR(2000) NULL,
        CreatedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_Vehicles_CreatedAt DEFAULT SYSUTCDATETIME(),
        RowVersion          ROWVERSION NOT NULL,
        CONSTRAINT FK_Vehicles_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.VehicleCategories(CategoryId),
        CONSTRAINT CK_Vehicles_Year CHECK (ManufactureYear BETWEEN 1990 AND 2100),
        CONSTRAINT CK_Vehicles_Seats CHECK (SeatCount BETWEEN 1 AND 30),
        CONSTRAINT CK_Vehicles_Transmission CHECK (Transmission IN (''Automatic'',''Manual'')),
        CONSTRAINT CK_Vehicles_Fuel CHECK (FuelType IN (''Gasoline'',''Diesel'',''Electric'',''Hybrid'')),
        CONSTRAINT CK_Vehicles_Price CHECK (DailyRate > 0),
        CONSTRAINT CK_Vehicles_Odometer CHECK (OdometerKm >= 0),
        CONSTRAINT CK_Vehicles_Status CHECK (OperationalStatus IN (''Active'',''Suspended''))
    );
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.Vehicles'') AND name = N''UX_Vehicles_Plate'')
BEGIN
    CREATE UNIQUE INDEX UX_Vehicles_Plate ON dbo.Vehicles(LicensePlate);
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.Vehicles'') AND name = N''IX_Vehicles_Filter'')
BEGIN
    CREATE INDEX IX_Vehicles_Filter ON dbo.Vehicles(CategoryId, OperationalStatus, DailyRate);
END;');
EXEC(N'-- Gia thue dai han theo goi 1/3/6/9/12 thang
IF OBJECT_ID(N''dbo.VehicleLongTermRates'', N''U'') IS NULL
BEGIN
    CREATE TABLE dbo.VehicleLongTermRates
    (
        VehicleId       BIGINT        NOT NULL,
        DurationMonths  TINYINT       NOT NULL,
        BaseMonthlyRate DECIMAL(18,2) NOT NULL,
        DiscountPercent DECIMAL(5,2)  NOT NULL,
        MonthlyRate AS CAST(ROUND(BaseMonthlyRate * (100 - DiscountPercent) / 100, 2) AS DECIMAL(18,2)) PERSISTED,
        IsActive        BIT           NOT NULL DEFAULT (1),
        UpdatedAt       DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_VehicleLongTermRates PRIMARY KEY (VehicleId, DurationMonths),
        CONSTRAINT FK_VehicleLongTermRates_Vehicles FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles(VehicleId),
        CONSTRAINT CK_VehicleLongTermRates_Price CHECK (BaseMonthlyRate > 0),
        CONSTRAINT CK_VehicleLongTermRates_Discount CHECK (
            (DurationMonths = 1  AND DiscountPercent = 0) OR
            (DurationMonths = 3  AND DiscountPercent = 2) OR
            (DurationMonths = 6  AND DiscountPercent = 3) OR
            (DurationMonths = 9  AND DiscountPercent = 4) OR
            (DurationMonths = 12 AND DiscountPercent = 5)
        )
    );
END;');
EXEC(N'IF OBJECT_ID(N''dbo.VehicleImages'', N''U'') IS NULL
BEGIN
    CREATE TABLE dbo.VehicleImages (
        ImageId             BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        VehicleId           BIGINT NOT NULL,
        ImageUrl            NVARCHAR(1000) NOT NULL,
        IsPrimary           BIT NOT NULL CONSTRAINT DF_Images_IsPrimary DEFAULT (0),
        SortOrder           INT NOT NULL CONSTRAINT DF_Images_SortOrder DEFAULT (0),
        CONSTRAINT FK_Images_Vehicle FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles(VehicleId) ON DELETE CASCADE
    );
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.VehicleImages'') AND name = N''IX_Images_Vehicle'')
BEGIN
    CREATE INDEX IX_Images_Vehicle ON dbo.VehicleImages(VehicleId, SortOrder);
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.VehicleImages'') AND name = N''UX_Images_OnePrimary'')
BEGIN
    CREATE UNIQUE INDEX UX_Images_OnePrimary ON dbo.VehicleImages(VehicleId) WHERE IsPrimary = 1;
END;');
EXEC(N'-- 3. DRIVERS AND RENTAL POLICY
IF OBJECT_ID(N''dbo.Drivers'', N''U'') IS NULL
BEGIN
    CREATE TABLE dbo.Drivers (
        DriverId            BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        FullName            NVARCHAR(120) NOT NULL,
        Phone               VARCHAR(20) NULL,
        LicenseNumber       NVARCHAR(40) NULL,
        IsActive            BIT NOT NULL CONSTRAINT DF_Drivers_IsActive DEFAULT (1),
        Notes               NVARCHAR(1000) NULL
    );
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.Drivers'') AND name = N''UX_Drivers_LicenseNumber'')
BEGIN
    CREATE UNIQUE INDEX UX_Drivers_LicenseNumber ON dbo.Drivers(LicenseNumber) WHERE LicenseNumber IS NOT NULL;
END;');
EXEC(N'IF OBJECT_ID(N''dbo.RentalPolicies'', N''U'') IS NULL
BEGIN
    CREATE TABLE dbo.RentalPolicies (
        PolicyId             TINYINT NOT NULL CONSTRAINT PK_RentalPolicies PRIMARY KEY,
        DepositPercent       DECIMAL(5,2) NOT NULL CONSTRAINT DF_Policy_Deposit DEFAULT (50.00),
        DriverDailyFee       DECIMAL(18,2) NOT NULL CONSTRAINT DF_Policy_DriverFee DEFAULT (350000.00),
        OnlineHoldMinutes    INT NOT NULL CONSTRAINT DF_Policy_OnlineHold DEFAULT (30),
        CashHoldHours        INT NOT NULL CONSTRAINT DF_Policy_CashHold DEFAULT (24),
        UpdatedAt            DATETIME2(0) NOT NULL CONSTRAINT DF_Policy_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_Policy_Singleton CHECK (PolicyId = 1),
        CONSTRAINT CK_Policy_Deposit CHECK (DepositPercent = 50.00),
        CONSTRAINT CK_Policy_DriverFee CHECK (DriverDailyFee >= 0),
        CONSTRAINT CK_Policy_Holds CHECK (OnlineHoldMinutes BETWEEN 5 AND 1440 AND CashHoldHours BETWEEN 1 AND 168)
    );
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM dbo.RentalPolicies WHERE PolicyId = 1)
    INSERT INTO dbo.RentalPolicies(PolicyId) VALUES (1);');
EXEC(N'-- 4. VOUCHERS
IF OBJECT_ID(N''dbo.Vouchers'', N''U'') IS NULL
BEGIN
    CREATE TABLE dbo.Vouchers (
        VoucherId            BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        VoucherCode          NVARCHAR(40) NOT NULL,
        DiscountType         VARCHAR(10) NOT NULL,
        DiscountValue        DECIMAL(18,2) NOT NULL,
        MaxDiscountAmount    DECIMAL(18,2) NULL,
        MinimumOrderAmount   DECIMAL(18,2) NOT NULL CONSTRAINT DF_Voucher_Minimum DEFAULT (0),
        StartsAt             DATETIME2(0) NOT NULL,
        EndsAt               DATETIME2(0) NOT NULL,
        UsageLimit           INT NULL,
        PerCustomerLimit     INT NOT NULL CONSTRAINT DF_Voucher_CustomerLimit DEFAULT (1),
        IsActive             BIT NOT NULL CONSTRAINT DF_Voucher_Active DEFAULT (1),
        RowVersion           ROWVERSION NOT NULL,
        CONSTRAINT CK_Voucher_Type CHECK (DiscountType IN (''Percent'',''Fixed'')),
        CONSTRAINT CK_Voucher_Amount CHECK (DiscountValue > 0 AND (DiscountType <> ''Percent'' OR DiscountValue <= 100)),
        CONSTRAINT CK_Voucher_Max CHECK (MaxDiscountAmount IS NULL OR MaxDiscountAmount > 0),
        CONSTRAINT CK_Voucher_Min CHECK (MinimumOrderAmount >= 0),
        CONSTRAINT CK_Voucher_Dates CHECK (EndsAt > StartsAt),
        CONSTRAINT CK_Voucher_Limits CHECK ((UsageLimit IS NULL OR UsageLimit > 0) AND PerCustomerLimit > 0)
    );
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.Vouchers'') AND name = N''UX_Vouchers_Code'')
BEGIN
    CREATE UNIQUE INDEX UX_Vouchers_Code ON dbo.Vouchers(VoucherCode);
END;');
EXEC(N'-- 5. BOOKINGS: prices are snapshotted to preserve history when catalog prices change.
-- Date/time convention: UTC; rental interval [StartAt, EndAt) - EndAt is exclusive.
IF OBJECT_ID(N''dbo.Bookings'', N''U'') IS NULL
BEGIN
    CREATE TABLE dbo.Bookings (
        BookingId             BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        BookingCode           VARCHAR(30) NOT NULL,
        RequestKey            UNIQUEIDENTIFIER NOT NULL, -- Double-click/retry idempotency token
        CustomerId            BIGINT NOT NULL,
        VehicleId             BIGINT NOT NULL,
        DriverId              BIGINT NULL, -- Assigned later by Staff for chauffeur rentals
        RentalType            VARCHAR(12) NOT NULL,
        DepositMethod         VARCHAR(5) NOT NULL,
        StartAt               DATETIME2(0) NOT NULL,
        EndAt                 DATETIME2(0) NOT NULL,
        RentalDays            INT NOT NULL,
        VehicleDailyRate      DECIMAL(18,2) NOT NULL,
        DriverDailyRate       DECIMAL(18,2) NOT NULL,
        VehicleAmount         DECIMAL(18,2) NOT NULL,
        DriverAmount          DECIMAL(18,2) NOT NULL,
        DiscountAmount        DECIMAL(18,2) NOT NULL CONSTRAINT DF_Booking_Discount DEFAULT (0),
        TotalAmount           DECIMAL(18,2) NOT NULL,
        DepositAmount         DECIMAL(18,2) NOT NULL,
        DepositDeadline       DATETIME2(0) NOT NULL,
        BookingStatus         VARCHAR(16) NOT NULL CONSTRAINT DF_Booking_Status DEFAULT (''PendingDeposit''),
        ConfirmedAt           DATETIME2(0) NULL,
        CancelledAt           DATETIME2(0) NULL,
        CancelReason          NVARCHAR(500) NULL,
        CreatedAt             DATETIME2(0) NOT NULL CONSTRAINT DF_Booking_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt             DATETIME2(0) NULL,
        RowVersion            ROWVERSION NOT NULL,
        CONSTRAINT FK_Bookings_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_Bookings_Vehicle FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles(VehicleId),
        CONSTRAINT FK_Bookings_Driver FOREIGN KEY (DriverId) REFERENCES dbo.Drivers(DriverId),
        CONSTRAINT CK_Bookings_RentalType CHECK (RentalType IN (''SelfDrive'',''WithDriver'')),
        CONSTRAINT CK_Bookings_DepositMethod CHECK (DepositMethod IN (''Cash'',''VNPay'')),
        CONSTRAINT CK_Bookings_Dates CHECK (EndAt > StartAt),
        CONSTRAINT CK_Bookings_Days CHECK (RentalDays > 0),
        CONSTRAINT CK_Bookings_Rates CHECK (VehicleDailyRate > 0 AND DriverDailyRate >= 0),
        CONSTRAINT CK_Bookings_Amounts CHECK (
            VehicleAmount >= 0 AND DriverAmount >= 0 AND DiscountAmount >= 0 AND
            TotalAmount >= 0 AND DepositAmount >= 0 AND DepositAmount <= TotalAmount AND
            DiscountAmount <= VehicleAmount + DriverAmount AND
            TotalAmount = VehicleAmount + DriverAmount - DiscountAmount
        ),
        CONSTRAINT CK_Bookings_SelfDrive CHECK (
            (RentalType = ''SelfDrive'' AND DriverId IS NULL AND DriverDailyRate = 0 AND DriverAmount = 0)
            OR RentalType = ''WithDriver''
        ),
        CONSTRAINT CK_Bookings_Status CHECK (
            BookingStatus IN (''PendingDeposit'',''Confirmed'',''InUse'',''Returned'',''Completed'',''Cancelled'',''Expired'')
        )
    );
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.Bookings'') AND name = N''UX_Bookings_Code'')
BEGIN
    CREATE UNIQUE INDEX UX_Bookings_Code ON dbo.Bookings(BookingCode);
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.Bookings'') AND name = N''UX_Bookings_RequestKey'')
BEGIN
    CREATE UNIQUE INDEX UX_Bookings_RequestKey ON dbo.Bookings(RequestKey);
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.Bookings'') AND name = N''IX_Bookings_VehicleRange'')
BEGIN
    CREATE INDEX IX_Bookings_VehicleRange ON dbo.Bookings(VehicleId, StartAt, EndAt) INCLUDE (BookingStatus, DepositDeadline);
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.Bookings'') AND name = N''IX_Bookings_Customer'')
BEGIN
    CREATE INDEX IX_Bookings_Customer ON dbo.Bookings(CustomerId, CreatedAt DESC);
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.Bookings'') AND name = N''IX_Bookings_DriverRange'')
BEGIN
    CREATE INDEX IX_Bookings_DriverRange ON dbo.Bookings(DriverId, StartAt, EndAt) WHERE DriverId IS NOT NULL;
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.Bookings'') AND name = N''IX_Bookings_PendingExpiry'')
BEGIN
    CREATE INDEX IX_Bookings_PendingExpiry ON dbo.Bookings(DepositDeadline) WHERE BookingStatus = ''PendingDeposit'';
END;');
EXEC(N'IF OBJECT_ID(N''dbo.BookingVouchers'', N''U'') IS NULL
BEGIN
    CREATE TABLE dbo.BookingVouchers (
        BookingId             BIGINT NOT NULL PRIMARY KEY,
        VoucherId             BIGINT NOT NULL,
        DiscountSnapshot      DECIMAL(18,2) NOT NULL,
        RedemptionStatus      VARCHAR(10) NOT NULL CONSTRAINT DF_BookingVoucher_Status DEFAULT (''Reserved''),
        CreatedAt             DATETIME2(0) NOT NULL CONSTRAINT DF_BookingVoucher_Created DEFAULT SYSUTCDATETIME(),
        AppliedAt             DATETIME2(0) NULL,
        CONSTRAINT FK_BookingVoucher_Booking FOREIGN KEY (BookingId) REFERENCES dbo.Bookings(BookingId),
        CONSTRAINT FK_BookingVoucher_Voucher FOREIGN KEY (VoucherId) REFERENCES dbo.Vouchers(VoucherId),
        CONSTRAINT CK_BookingVoucher_Discount CHECK (DiscountSnapshot >= 0),
        CONSTRAINT CK_BookingVoucher_Status CHECK (RedemptionStatus IN (''Reserved'',''Applied'',''Released''))
    );
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.BookingVouchers'') AND name = N''IX_BookingVoucher_Usage'')
BEGIN
    CREATE INDEX IX_BookingVoucher_Usage ON dbo.BookingVouchers(VoucherId, RedemptionStatus);
END;');
EXEC(N'-- 6. PAYMENT HISTORY. Refund is a positive amount with PaymentType=''Refund''.
IF OBJECT_ID(N''dbo.Payments'', N''U'') IS NULL
BEGIN
    CREATE TABLE dbo.Payments (
        PaymentId             BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        BookingId             BIGINT NOT NULL,
        PaymentType           VARCHAR(10) NOT NULL,
        PaymentMethod         VARCHAR(12) NOT NULL,
        Amount                DECIMAL(18,2) NOT NULL,
        PaymentStatus         VARCHAR(10) NOT NULL CONSTRAINT DF_Payments_Status DEFAULT (''Pending''),
        GatewayTxnRef         VARCHAR(100) NULL, -- Merchant-created VNPay vnp_TxnRef, unique per attempt
        GatewayTransactionNo  VARCHAR(100) NULL, -- VNPay transaction id
        GatewayResponseCode   VARCHAR(20) NULL,
        IdempotencyKey        UNIQUEIDENTIFIER NULL,
        VerifiedByUserId      BIGINT NULL, -- Staff/Admin confirmation for cash/manual refunds
        PaidAt                DATETIME2(0) NULL,
        CreatedAt             DATETIME2(0) NOT NULL CONSTRAINT DF_Payments_Created DEFAULT SYSUTCDATETIME(),
        Note                  NVARCHAR(500) NULL,
        RowVersion            ROWVERSION NOT NULL,
        CONSTRAINT FK_Payments_Booking FOREIGN KEY (BookingId) REFERENCES dbo.Bookings(BookingId),
        CONSTRAINT FK_Payments_Verifier FOREIGN KEY (VerifiedByUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_Payments_Type CHECK (PaymentType IN (''Deposit'',''Balance'',''Refund'')),
        CONSTRAINT CK_Payments_Method CHECK (PaymentMethod IN (''Cash'',''VNPay'',''BankTransfer'')),
        CONSTRAINT CK_Payments_Status CHECK (PaymentStatus IN (''Pending'',''Success'',''Failed'',''Cancelled'')),
        CONSTRAINT CK_Payments_Amount CHECK (Amount > 0),
        CONSTRAINT CK_Payments_GatewayRef CHECK (PaymentMethod <> ''VNPay'' OR GatewayTxnRef IS NOT NULL),
        CONSTRAINT CK_Payments_BankTransfer CHECK (PaymentType = ''Refund'' OR PaymentMethod <> ''BankTransfer''),
        CONSTRAINT CK_Payments_SuccessPaidAt CHECK (PaymentStatus <> ''Success'' OR PaidAt IS NOT NULL)
    );
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.Payments'') AND name = N''IX_Payments_Booking'')
BEGIN
    CREATE INDEX IX_Payments_Booking ON dbo.Payments(BookingId, CreatedAt DESC);
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.Payments'') AND name = N''UX_Payments_GatewayRef'')
BEGIN
    CREATE UNIQUE INDEX UX_Payments_GatewayRef ON dbo.Payments(GatewayTxnRef) WHERE GatewayTxnRef IS NOT NULL;
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.Payments'') AND name = N''UX_Payments_GatewayTxnNo'')
BEGIN
    CREATE UNIQUE INDEX UX_Payments_GatewayTxnNo ON dbo.Payments(GatewayTransactionNo) WHERE GatewayTransactionNo IS NOT NULL;
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.Payments'') AND name = N''UX_Payments_Idempotency'')
BEGIN
    CREATE UNIQUE INDEX UX_Payments_Idempotency ON dbo.Payments(IdempotencyKey) WHERE IdempotencyKey IS NOT NULL;
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.Payments'') AND name = N''UX_Payments_OneDepositSuccess'')
BEGIN
    CREATE UNIQUE INDEX UX_Payments_OneDepositSuccess ON dbo.Payments(BookingId)
        WHERE PaymentType = ''Deposit'' AND PaymentStatus = ''Success'';
END;');
EXEC(N'-- 7. PICKUP, RETURN, AND MAINTENANCE
IF OBJECT_ID(N''dbo.VehicleHandovers'', N''U'') IS NULL
BEGIN
    CREATE TABLE dbo.VehicleHandovers (
        HandoverId             BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        BookingId              BIGINT NOT NULL,
        StaffId                BIGINT NOT NULL,
        HandedOverAt           DATETIME2(0) NOT NULL CONSTRAINT DF_Handover_At DEFAULT SYSUTCDATETIME(),
        OdometerOutKm          INT NOT NULL,
        FuelPercentOut         TINYINT NOT NULL,
        ConditionNotes         NVARCHAR(2000) NULL,
        CONSTRAINT FK_Handover_Booking FOREIGN KEY (BookingId) REFERENCES dbo.Bookings(BookingId),
        CONSTRAINT FK_Handover_Staff FOREIGN KEY (StaffId) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_Handover_Mileage CHECK (OdometerOutKm >= 0),
        CONSTRAINT CK_Handover_Fuel CHECK (FuelPercentOut BETWEEN 0 AND 100)
    );
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.VehicleHandovers'') AND name = N''UX_Handover_Booking'')
BEGIN
    CREATE UNIQUE INDEX UX_Handover_Booking ON dbo.VehicleHandovers(BookingId);
END;');
EXEC(N'IF OBJECT_ID(N''dbo.VehicleReturns'', N''U'') IS NULL
BEGIN
    CREATE TABLE dbo.VehicleReturns (
        ReturnId                BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        BookingId               BIGINT NOT NULL,
        StaffId                 BIGINT NOT NULL,
        ReturnedAt              DATETIME2(0) NOT NULL CONSTRAINT DF_Returns_At DEFAULT SYSUTCDATETIME(),
        OdometerInKm            INT NOT NULL,
        FuelPercentIn           TINYINT NOT NULL,
        LateFee                 DECIMAL(18,2) NOT NULL CONSTRAINT DF_Returns_LateFee DEFAULT (0),
        DamageFee               DECIMAL(18,2) NOT NULL CONSTRAINT DF_Returns_DamageFee DEFAULT (0),
        OtherFee                DECIMAL(18,2) NOT NULL CONSTRAINT DF_Returns_OtherFee DEFAULT (0),
        InspectionNotes         NVARCHAR(2000) NULL,
        CONSTRAINT FK_Returns_Booking FOREIGN KEY (BookingId) REFERENCES dbo.Bookings(BookingId),
        CONSTRAINT FK_Returns_Staff FOREIGN KEY (StaffId) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_Returns_Mileage CHECK (OdometerInKm >= 0),
        CONSTRAINT CK_Returns_Fuel CHECK (FuelPercentIn BETWEEN 0 AND 100),
        CONSTRAINT CK_Returns_Fees CHECK (LateFee >= 0 AND DamageFee >= 0 AND OtherFee >= 0)
    );
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.VehicleReturns'') AND name = N''UX_Returns_Booking'')
BEGIN
    CREATE UNIQUE INDEX UX_Returns_Booking ON dbo.VehicleReturns(BookingId);
END;');
EXEC(N'IF OBJECT_ID(N''dbo.MaintenanceRecords'', N''U'') IS NULL
BEGIN
    CREATE TABLE dbo.MaintenanceRecords (
        MaintenanceId           BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        VehicleId               BIGINT NOT NULL,
        CreatedByUserId         BIGINT NOT NULL,
        StartAt                 DATETIME2(0) NOT NULL,
        EndAt                   DATETIME2(0) NOT NULL,
        MaintenanceStatus       VARCHAR(12) NOT NULL CONSTRAINT DF_Maintenance_Status DEFAULT (''Scheduled''),
        Description             NVARCHAR(1500) NOT NULL,
        Cost                    DECIMAL(18,2) NOT NULL CONSTRAINT DF_Maintenance_Cost DEFAULT (0),
        CONSTRAINT FK_Maintenance_Vehicle FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles(VehicleId),
        CONSTRAINT FK_Maintenance_User FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_Maintenance_Time CHECK (EndAt > StartAt),
        CONSTRAINT CK_Maintenance_Cost CHECK (Cost >= 0),
        CONSTRAINT CK_Maintenance_Status CHECK (MaintenanceStatus IN (''Scheduled'',''InProgress'',''Completed'',''Cancelled''))
    );
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.MaintenanceRecords'') AND name = N''IX_Maintenance_VehicleRange'')
BEGIN
    CREATE INDEX IX_Maintenance_VehicleRange ON dbo.MaintenanceRecords(VehicleId, StartAt, EndAt);
END;');
EXEC(N'-- 8. REVIEWS AND BOOKING AUDIT
IF OBJECT_ID(N''dbo.Reviews'', N''U'') IS NULL
BEGIN
    CREATE TABLE dbo.Reviews (
        ReviewId                BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        BookingId               BIGINT NOT NULL,
        Rating                  TINYINT NOT NULL,
        Comment                 NVARCHAR(2000) NULL,
        IsVisible               BIT NOT NULL CONSTRAINT DF_Reviews_Visible DEFAULT (1),
        CreatedAt               DATETIME2(0) NOT NULL CONSTRAINT DF_Reviews_Created DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Reviews_Booking FOREIGN KEY (BookingId) REFERENCES dbo.Bookings(BookingId),
        CONSTRAINT CK_Reviews_Rating CHECK (Rating BETWEEN 1 AND 5)
    );
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.Reviews'') AND name = N''UX_Reviews_Booking'')
BEGIN
    CREATE UNIQUE INDEX UX_Reviews_Booking ON dbo.Reviews(BookingId);
END;');
-- BEGIN QR DEMO SCHEMA (2026-11-10)
-- Shared QR flow: actual transfer is 1,000 VND; Staff verifies receipt.
-- Keep existing bookings as non-demo and never post simulated deposits to Payments.
EXEC(N'IF COL_LENGTH(N''dbo.Bookings'', N''IsDemo'') IS NULL
        ALTER TABLE dbo.Bookings ADD IsDemo bit NOT NULL CONSTRAINT DF_Bookings_IsDemo DEFAULT(0) WITH VALUES;
    IF OBJECT_ID(N''dbo.DemoPayments'', N''U'') IS NULL
    BEGIN
        CREATE TABLE dbo.DemoPayments
        (
            DemoPaymentId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_DemoPayments PRIMARY KEY,
            BookingId bigint NOT NULL REFERENCES dbo.Bookings(BookingId),
            SimulatedDepositAmount decimal(18,2) NOT NULL CHECK(SimulatedDepositAmount > 0),
            RequiredTransferAmount decimal(18,2) NOT NULL CONSTRAINT DF_DemoPayments_Required DEFAULT(1000) CHECK(RequiredTransferAmount=1000),
            ActualAmount decimal(18,2) NOT NULL CONSTRAINT DF_DemoPayments_Actual DEFAULT(0),
            TransferContent varchar(25) NOT NULL,
            BankBin varchar(6) NOT NULL,
            BankName nvarchar(80) NOT NULL,
            AccountNumber varchar(34) NOT NULL,
            AccountName nvarchar(120) NOT NULL,
            PaymentStatus varchar(10) NOT NULL CONSTRAINT DF_DemoPayments_Status DEFAULT(''Pending''),
            BankTransactionReference nvarchar(100) NULL,
            ConfirmationRequestKey uniqueidentifier NULL,
            VerifiedByUserId bigint NULL REFERENCES dbo.Users(UserId),
            PaidAt datetime2(0) NULL,
            ConfirmedBooking bit NOT NULL CONSTRAINT DF_DemoPayments_Confirmed DEFAULT(0),
            CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_DemoPayments_Created DEFAULT SYSUTCDATETIME(),
            RowVersion rowversion NOT NULL,
            CONSTRAINT CK_DemoPayments_State CHECK
            (
                (PaymentStatus=''Pending'' AND ActualAmount=0 AND PaidAt IS NULL AND VerifiedByUserId IS NULL
                 AND BankTransactionReference IS NULL AND ConfirmationRequestKey IS NULL AND ConfirmedBooking=0)
                OR
                (PaymentStatus=''Success'' AND ActualAmount=1000 AND PaidAt IS NOT NULL AND VerifiedByUserId IS NOT NULL
                 AND NULLIF(LTRIM(RTRIM(BankTransactionReference)),'''') IS NOT NULL AND ConfirmationRequestKey IS NOT NULL)
            )
        );
        CREATE UNIQUE INDEX UX_DemoPayments_Booking ON dbo.DemoPayments(BookingId);
        CREATE UNIQUE INDEX UX_DemoPayments_TransferContent ON dbo.DemoPayments(TransferContent);
        CREATE UNIQUE INDEX UX_DemoPayments_BankReference ON dbo.DemoPayments(BankTransactionReference) WHERE BankTransactionReference IS NOT NULL;
        CREATE UNIQUE INDEX UX_DemoPayments_RequestKey ON dbo.DemoPayments(ConfirmationRequestKey) WHERE ConfirmationRequestKey IS NOT NULL;
    END;');
-- END QR DEMO SCHEMA
EXEC(N'IF OBJECT_ID(N''dbo.BookingStatusHistory'', N''U'') IS NULL
BEGIN
    CREATE TABLE dbo.BookingStatusHistory (
        HistoryId               BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        BookingId               BIGINT NOT NULL,
        OldStatus               VARCHAR(16) NULL,
        NewStatus               VARCHAR(16) NOT NULL,
        ChangedByUserId         BIGINT NULL, -- NULL for automated changes
        ChangedAt               DATETIME2(0) NOT NULL CONSTRAINT DF_History_Changed DEFAULT SYSUTCDATETIME(),
        ChangeNote              NVARCHAR(500) NULL,
        CONSTRAINT FK_History_Booking FOREIGN KEY (BookingId) REFERENCES dbo.Bookings(BookingId),
        CONSTRAINT FK_History_User FOREIGN KEY (ChangedByUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_History_Old CHECK (OldStatus IS NULL OR OldStatus IN (''PendingDeposit'',''Confirmed'',''InUse'',''Returned'',''Completed'',''Cancelled'',''Expired'')),
        CONSTRAINT CK_History_New CHECK (NewStatus IN (''PendingDeposit'',''Confirmed'',''InUse'',''Returned'',''Completed'',''Cancelled'',''Expired''))
    );
END;');
EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.BookingStatusHistory'') AND name = N''IX_History_BookingTime'')
BEGIN
    CREATE INDEX IX_History_BookingTime ON dbo.BookingStatusHistory(BookingId, ChangedAt DESC);
END;');
EXEC(N'-- SEARCH EXTENSIONS
/* SEARCH EXTENSIONS: original tables/procedures unchanged. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
BEGIN TRANSACTION;
IF OBJECT_ID(N''dbo.VehicleLongTermRates'',N''U'') IS NULL THROW 52001,''Run the original CRMS schema first.'',1;
IF OBJECT_ID(N''dbo.SearchSettings'', N''U'') IS NULL
BEGIN
CREATE TABLE dbo.SearchSettings (
 Id tinyint NOT NULL CONSTRAINT PK_SearchSettings PRIMARY KEY CHECK(Id=1),
 MinimumOwnerRating decimal(3,2) NOT NULL CHECK(MinimumOwnerRating BETWEEN 1 AND 5),
 MinimumOwnerReviews int NOT NULL CHECK(MinimumOwnerReviews>=1)
);
END;

IF OBJECT_ID(N''dbo.Cities'', N''U'') IS NULL
BEGIN
CREATE TABLE dbo.Cities (
 CityId int IDENTITY PRIMARY KEY, CityKey varchar(40) NOT NULL UNIQUE,
 CityName nvarchar(120) NOT NULL, CountryCode char(2) NOT NULL DEFAULT(''VN''),
 IsActive bit NOT NULL DEFAULT(1), SourceReference nvarchar(500) NULL
);
END;

IF OBJECT_ID(N''dbo.RentalLocations'', N''U'') IS NULL
BEGIN
CREATE TABLE dbo.RentalLocations (
 LocationId bigint IDENTITY PRIMARY KEY, CityId int NOT NULL REFERENCES dbo.Cities(CityId),
 DisplayName nvarchar(160) NOT NULL, AddressLine nvarchar(500) NOT NULL,
 Latitude decimal(9,6) NULL, Longitude decimal(9,6) NULL, IsActive bit NOT NULL DEFAULT(1),
 CONSTRAINT CK_RentalLocations_Coordinates CHECK((Latitude IS NULL AND Longitude IS NULL) OR
 (Latitude IS NOT NULL AND Longitude IS NOT NULL AND Latitude BETWEEN -90 AND 90 AND Longitude BETWEEN -180 AND 180))
);
END;

IF OBJECT_ID(N''dbo.VehicleOwners'', N''U'') IS NULL
BEGIN
CREATE TABLE dbo.VehicleOwners (
 OwnerId bigint IDENTITY PRIMARY KEY, UserId bigint NULL REFERENCES dbo.Users(UserId),
 OwnerType varchar(10) NOT NULL CHECK(OwnerType IN (''Individual'',''Company'')),
 DisplayName nvarchar(160) NOT NULL, ContactPhone varchar(20) NULL,
 IsVerified bit NOT NULL DEFAULT(0), VerifiedAt datetime2(0) NULL, IsActive bit NOT NULL DEFAULT(1),
 CreatedAt datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT CK_Owners_Verification CHECK(IsVerified=0 OR VerifiedAt IS NOT NULL)
);
END;

IF OBJECT_ID(N''dbo.VehicleSearchProfiles'', N''U'') IS NULL
BEGIN
CREATE TABLE dbo.VehicleSearchProfiles (
 VehicleId bigint NOT NULL PRIMARY KEY REFERENCES dbo.Vehicles(VehicleId),
 OwnerId bigint NULL REFERENCES dbo.VehicleOwners(OwnerId),
 LocationId bigint NULL REFERENCES dbo.RentalLocations(LocationId),
 IsPublished bit NOT NULL DEFAULT(0), VerifiedAt datetime2(0) NULL,
 UpdatedAt datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(), RowVersion rowversion,
 CONSTRAINT CK_SearchProfiles_Publication CHECK(IsPublished=0 OR (OwnerId IS NOT NULL AND LocationId IS NOT NULL AND VerifiedAt IS NOT NULL))
);
END;

IF OBJECT_ID(N''dbo.VehicleRentalModes'', N''U'') IS NULL
BEGIN
CREATE TABLE dbo.VehicleRentalModes (
 VehicleId bigint NOT NULL REFERENCES dbo.Vehicles(VehicleId),
 RentalMode varchar(12) NOT NULL CHECK(RentalMode IN (''SelfDrive'',''WithDriver'',''LongTerm'')),
 IsEnabled bit NOT NULL DEFAULT(0), VerifiedAt datetime2(0) NULL,
 CONSTRAINT PK_VehicleRentalModes PRIMARY KEY(VehicleId,RentalMode),
 CONSTRAINT CK_RentalModes_Verified CHECK(IsEnabled=0 OR VerifiedAt IS NOT NULL)
);
END;

IF OBJECT_ID(N''dbo.VehicleRentalPolicies'', N''U'') IS NULL
BEGIN
CREATE TABLE dbo.VehicleRentalPolicies (
 VehicleId bigint NOT NULL, RentalMode varchar(12) NOT NULL,
 SupportsInstantBooking bit NULL, RequiresCollateral bit NULL, CollateralAmount decimal(18,2) NULL,
 CollateralDescription nvarchar(1000) NULL, UnlimitedMileage bit NULL, IncludedKm int NULL,
 MileagePeriod varchar(8) NULL, ExcessKmFee decimal(18,2) NULL, AllowsIntercityTravel bit NULL,
 UpdatedAt datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(), RowVersion rowversion,
 CONSTRAINT PK_VehicleRentalPolicies PRIMARY KEY(VehicleId,RentalMode),
 CONSTRAINT FK_RentalPolicies_Mode FOREIGN KEY(VehicleId,RentalMode) REFERENCES dbo.VehicleRentalModes(VehicleId,RentalMode),
 CONSTRAINT CK_RentalPolicies_Collateral CHECK((RequiresCollateral IS NULL AND CollateralAmount IS NULL AND CollateralDescription IS NULL) OR
 (RequiresCollateral IS NOT NULL AND RequiresCollateral=0 AND CollateralAmount IS NOT NULL AND CollateralAmount=0) OR
 (RequiresCollateral IS NOT NULL AND RequiresCollateral=1 AND (COALESCE(CollateralAmount,0)>0 OR LEN(LTRIM(RTRIM(COALESCE(CollateralDescription,N''''))))>0))),
 CONSTRAINT CK_RentalPolicies_Mileage CHECK((UnlimitedMileage IS NULL AND IncludedKm IS NULL AND MileagePeriod IS NULL AND ExcessKmFee IS NULL) OR
 (UnlimitedMileage IS NOT NULL AND UnlimitedMileage=1 AND IncludedKm IS NULL AND MileagePeriod IS NULL AND ExcessKmFee IS NULL) OR
 (UnlimitedMileage IS NOT NULL AND UnlimitedMileage=0 AND IncludedKm IS NOT NULL AND IncludedKm>0 AND MileagePeriod IS NOT NULL AND MileagePeriod IN (''Day'',''Month'',''Trip'') AND ExcessKmFee IS NOT NULL AND ExcessKmFee>=0)),
 CONSTRAINT CK_RentalPolicies_CollateralAmount CHECK(CollateralAmount IS NULL OR CollateralAmount>=0)
);
END;

IF OBJECT_ID(N''dbo.VehicleHourlyRates'', N''U'') IS NULL
BEGIN
CREATE TABLE dbo.VehicleHourlyRates (
 VehicleId bigint NOT NULL, RentalMode varchar(12) NOT NULL CHECK(RentalMode=''SelfDrive''),
 HourlyRate decimal(18,2) NOT NULL CHECK(HourlyRate>0), MinimumHours tinyint NOT NULL,
 MaximumHours tinyint NOT NULL, BillingIncrementMinutes smallint NOT NULL DEFAULT(60),
 IsActive bit NOT NULL DEFAULT(0), UpdatedAt datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT PK_VehicleHourlyRates PRIMARY KEY(VehicleId,RentalMode),
 CONSTRAINT FK_HourlyRates_Mode FOREIGN KEY(VehicleId,RentalMode) REFERENCES dbo.VehicleRentalModes(VehicleId,RentalMode),
 CONSTRAINT CK_HourlyRates_Hours CHECK(MinimumHours BETWEEN 1 AND 23 AND MaximumHours BETWEEN MinimumHours AND 23),
 CONSTRAINT CK_HourlyRates_Increment CHECK(BillingIncrementMinutes IN(15,30,60))
);
END;

IF OBJECT_ID(N''dbo.Amenities'', N''U'') IS NULL
BEGIN
CREATE TABLE dbo.Amenities (
 AmenityId int IDENTITY PRIMARY KEY, AmenityCode varchar(40) NOT NULL UNIQUE,
 AmenityName nvarchar(100) NOT NULL, IsActive bit NOT NULL DEFAULT(1)
);
END;

IF OBJECT_ID(N''dbo.VehicleAmenities'', N''U'') IS NULL
BEGIN
CREATE TABLE dbo.VehicleAmenities (
 VehicleId bigint NOT NULL, RentalMode varchar(12) NOT NULL,
 AmenityId int NOT NULL REFERENCES dbo.Amenities(AmenityId), Quantity smallint NOT NULL CHECK(Quantity>0),
 AdditionalFee decimal(18,2) NOT NULL DEFAULT(0) CHECK(AdditionalFee>=0),
 FeeUnit varchar(8) NOT NULL DEFAULT(''Trip'') CHECK(FeeUnit IN(''Trip'',''Day'',''Month'')),
 IsActive bit NOT NULL DEFAULT(0),
 CONSTRAINT PK_VehicleAmenities PRIMARY KEY(VehicleId,RentalMode,AmenityId),
 CONSTRAINT FK_Amenities_Mode FOREIGN KEY(VehicleId,RentalMode) REFERENCES dbo.VehicleRentalModes(VehicleId,RentalMode)
);
END;

IF OBJECT_ID(N''dbo.RentalPurposes'', N''U'') IS NULL
BEGIN
CREATE TABLE dbo.RentalPurposes (
 PurposeId int IDENTITY PRIMARY KEY, PurposeCode varchar(40) NOT NULL UNIQUE,
 PurposeName nvarchar(100) NOT NULL, Definition nvarchar(500) NOT NULL, IsActive bit NOT NULL DEFAULT(1)
);
END;

IF OBJECT_ID(N''dbo.VehicleRentalPurposes'', N''U'') IS NULL
BEGIN
CREATE TABLE dbo.VehicleRentalPurposes (
 VehicleId bigint NOT NULL, RentalMode varchar(12) NOT NULL,
 PurposeId int NOT NULL REFERENCES dbo.RentalPurposes(PurposeId), VerifiedAt datetime2(0) NOT NULL,
 CONSTRAINT PK_VehicleRentalPurposes PRIMARY KEY(VehicleId,RentalMode,PurposeId),
 CONSTRAINT FK_Purposes_Mode FOREIGN KEY(VehicleId,RentalMode) REFERENCES dbo.VehicleRentalModes(VehicleId,RentalMode)
);
END;

IF OBJECT_ID(N''dbo.VehicleTravelAreas'', N''U'') IS NULL
BEGIN
CREATE TABLE dbo.VehicleTravelAreas (
 VehicleId bigint NOT NULL, RentalMode varchar(12) NOT NULL,
 CityId int NOT NULL REFERENCES dbo.Cities(CityId), IsActive bit NOT NULL DEFAULT(0),
 CONSTRAINT PK_VehicleTravelAreas PRIMARY KEY(VehicleId,RentalMode,CityId),
 CONSTRAINT FK_TravelAreas_Mode FOREIGN KEY(VehicleId,RentalMode) REFERENCES dbo.VehicleRentalModes(VehicleId,RentalMode)
);
END;

IF OBJECT_ID(N''dbo.VehicleDeliveryAreas'', N''U'') IS NULL
BEGIN
CREATE TABLE dbo.VehicleDeliveryAreas (
 VehicleId bigint NOT NULL, RentalMode varchar(12) NOT NULL,
 CityId int NOT NULL REFERENCES dbo.Cities(CityId),
 FeeModel varchar(8) NOT NULL CHECK(FeeModel IN(''Flat'',''PerKm'')),
 FlatFee decimal(18,2) NULL, FeePerKm decimal(18,2) NULL, MinimumFee decimal(18,2) NULL,
 ServiceRadiusKm decimal(7,2) NOT NULL CHECK(ServiceRadiusKm>0),
 FreeWithinKm decimal(7,2) NOT NULL DEFAULT(0),
 IsActive bit NOT NULL DEFAULT(0), UpdatedAt datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT PK_VehicleDeliveryAreas PRIMARY KEY(VehicleId,RentalMode,CityId),
 CONSTRAINT FK_DeliveryAreas_Mode FOREIGN KEY(VehicleId,RentalMode) REFERENCES dbo.VehicleRentalModes(VehicleId,RentalMode),
 CONSTRAINT CK_DeliveryAreas_FreeRadius CHECK(FreeWithinKm BETWEEN 0 AND ServiceRadiusKm),
 CONSTRAINT CK_DeliveryAreas_Fees CHECK((FeeModel=''Flat'' AND FlatFee IS NOT NULL AND FlatFee>=0 AND FeePerKm IS NULL AND MinimumFee IS NULL) OR
 (FeeModel=''PerKm'' AND FlatFee IS NULL AND FeePerKm IS NOT NULL AND FeePerKm>=0 AND MinimumFee IS NOT NULL AND MinimumFee>=0))
);
END;

IF OBJECT_ID(N''dbo.VehicleDriverRoutes'', N''U'') IS NULL
BEGIN
CREATE TABLE dbo.VehicleDriverRoutes (
 RouteId bigint IDENTITY PRIMARY KEY, VehicleId bigint NOT NULL,
 RentalMode varchar(12) NOT NULL DEFAULT(''WithDriver'') CHECK(RentalMode=''WithDriver''),
 RouteType varchar(20) NOT NULL CHECK(RouteType IN(''InnerCity'',''InterCityRoundTrip'',''InterCityOneWay'')),
 PickupCityId int NOT NULL REFERENCES dbo.Cities(CityId), DestinationCityId int NOT NULL REFERENCES dbo.Cities(CityId),
 PricingModel varchar(12) NOT NULL CHECK(PricingModel IN(''DailyPolicy'',''FixedTrip'')),
 TripBasePrice decimal(18,2) NULL, IncludedKm int NULL, IncludedHours smallint NULL,
 ExcessKmFee decimal(18,2) NULL, ExcessHourFee decimal(18,2) NULL,
 IsActive bit NOT NULL DEFAULT(0),
 CONSTRAINT FK_DriverRoutes_Mode FOREIGN KEY(VehicleId,RentalMode) REFERENCES dbo.VehicleRentalModes(VehicleId,RentalMode),
 CONSTRAINT UQ_DriverRoutes_Route UNIQUE(VehicleId,RouteType,PickupCityId,DestinationCityId),
 CONSTRAINT CK_DriverRoutes_Cities CHECK((RouteType=''InnerCity'' AND PickupCityId=DestinationCityId) OR (RouteType<>''InnerCity'' AND PickupCityId<>DestinationCityId)),
 CONSTRAINT CK_DriverRoutes_Rates CHECK((PricingModel=''DailyPolicy'' AND TripBasePrice IS NULL AND IncludedKm IS NULL AND IncludedHours IS NULL AND ExcessKmFee IS NULL AND ExcessHourFee IS NULL) OR
 (PricingModel=''FixedTrip'' AND TripBasePrice IS NOT NULL AND TripBasePrice>0 AND IncludedKm IS NOT NULL AND IncludedKm>0 AND IncludedHours IS NOT NULL AND IncludedHours>0 AND ExcessKmFee IS NOT NULL AND ExcessKmFee>=0 AND ExcessHourFee IS NOT NULL AND ExcessHourFee>=0))
);
END;

IF OBJECT_ID(N''dbo.VehicleVoucherEligibility'', N''U'') IS NULL
BEGIN
CREATE TABLE dbo.VehicleVoucherEligibility (
 VehicleId bigint NOT NULL, RentalMode varchar(12) NOT NULL,
 VoucherId bigint NOT NULL REFERENCES dbo.Vouchers(VoucherId), IsActive bit NOT NULL DEFAULT(0),
 CONSTRAINT PK_VehicleVoucherEligibility PRIMARY KEY(VehicleId,RentalMode,VoucherId),
 CONSTRAINT FK_VoucherEligibility_Mode FOREIGN KEY(VehicleId,RentalMode) REFERENCES dbo.VehicleRentalModes(VehicleId,RentalMode)
);
END;

IF OBJECT_ID(N''dbo.VehicleCalendarBlocks'', N''U'') IS NULL
BEGIN
CREATE TABLE dbo.VehicleCalendarBlocks (
 BlockId bigint IDENTITY PRIMARY KEY, VehicleId bigint NOT NULL REFERENCES dbo.Vehicles(VehicleId),
 StartAt datetime2(0) NOT NULL, EndAt datetime2(0) NOT NULL,
 Reason nvarchar(500) NOT NULL, IsActive bit NOT NULL DEFAULT(1),
 CreatedByUserId bigint NOT NULL REFERENCES dbo.Users(UserId), CreatedAt datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT CK_CalendarBlocks_Dates CHECK(EndAt>StartAt)
);
END;

IF OBJECT_ID(N''dbo.BookingOfferSnapshots'', N''U'') IS NULL
BEGIN
CREATE TABLE dbo.BookingOfferSnapshots (
 BookingId bigint NOT NULL PRIMARY KEY REFERENCES dbo.Bookings(BookingId),
 VehicleId bigint NOT NULL REFERENCES dbo.Vehicles(VehicleId),
 OwnerId bigint NOT NULL REFERENCES dbo.VehicleOwners(OwnerId),
 RentalMode varchar(12) NOT NULL CHECK(RentalMode IN(''SelfDrive'',''WithDriver'',''LongTerm'')),
 PriceUnit varchar(5) NOT NULL CHECK(PriceUnit IN(''Hour'',''Day'',''Month'')),
 PolicySnapshotJson nvarchar(max) NOT NULL CHECK(ISJSON(PolicySnapshotJson)=1),
 CreatedAt datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT CK_BookingOfferSnapshots_Unit CHECK((RentalMode=''LongTerm'' AND PriceUnit=''Month'') OR (RentalMode=''SelfDrive'' AND PriceUnit IN(''Hour'',''Day'')) OR (RentalMode=''WithDriver'' AND PriceUnit=''Day''))
);
END;

IF NOT EXISTS(SELECT 1 FROM dbo.SearchSettings WHERE Id=1)
 INSERT dbo.SearchSettings VALUES(1,4.80,5);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(''dbo.VehicleOwners'') AND name=''UX_Search_OwnerUser'')
 CREATE UNIQUE INDEX UX_Search_OwnerUser ON dbo.VehicleOwners(UserId) WHERE UserId IS NOT NULL;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(''dbo.VehicleSearchProfiles'') AND name=''IX_Search_LocationOwner'')
 CREATE INDEX IX_Search_LocationOwner ON dbo.VehicleSearchProfiles(LocationId,OwnerId) INCLUDE(IsPublished);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(''dbo.VehicleCalendarBlocks'') AND name=''IX_Search_Calendar'')
 CREATE INDEX IX_Search_Calendar ON dbo.VehicleCalendarBlocks(VehicleId,StartAt,EndAt) INCLUDE(IsActive);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(''dbo.BookingOfferSnapshots'') AND name=''IX_Search_BookingOwner'')
 CREATE INDEX IX_Search_BookingOwner ON dbo.BookingOfferSnapshots(OwnerId,BookingId);

EXEC(N''CREATE OR ALTER VIEW dbo.vSearchOwnerRatings AS
SELECT s.OwnerId, COUNT_BIG(*) AS ReviewCount,
 CAST(AVG(CAST(r.Rating AS decimal(10,4))) AS decimal(10,4)) AS AverageRating
FROM dbo.Reviews r JOIN dbo.Bookings b ON b.BookingId=r.BookingId
 JOIN dbo.BookingOfferSnapshots s ON s.BookingId=b.BookingId AND s.VehicleId=b.VehicleId
WHERE r.IsVisible=1 AND b.BookingStatus=''''Completed''''
GROUP BY s.OwnerId'');

EXEC(N''CREATE OR ALTER VIEW dbo.vVehicleSearchCatalog AS
SELECT v.VehicleId,v.CategoryId,c.CategoryName,v.Brand,v.Model,v.ManufactureYear,v.SeatCount,
 v.Transmission,v.FuelType,v.DailyRate,m.RentalMode,p.OwnerId,o.DisplayName AS OwnerName,
 l.LocationId,l.CityId,city.CityName,l.Latitude,l.Longitude,
 r.DurationMonths,r.BaseMonthlyRate,r.MonthlyRate,r.DiscountPercent,
 CAST(CASE WHEN m.RentalMode=''''LongTerm'''' THEN r.MonthlyRate ELSE v.DailyRate END AS decimal(18,2)) AS SearchUnitRate,
 CASE WHEN m.RentalMode=''''LongTerm'''' THEN ''''Month'''' ELSE ''''Day'''' END AS SearchPriceUnit,
 policy.SupportsInstantBooking,policy.RequiresCollateral,policy.CollateralAmount,
 policy.UnlimitedMileage,policy.IncludedKm,policy.MileagePeriod,policy.ExcessKmFee,policy.AllowsIntercityTravel,
 h.HourlyRate,h.MinimumHours,h.MaximumHours,h.BillingIncrementMinutes,
 ratings.ReviewCount,ratings.AverageRating,
 CAST(CASE WHEN ratings.ReviewCount>=settings.MinimumOwnerReviews AND ratings.AverageRating>=settings.MinimumOwnerRating THEN 1 ELSE 0 END AS bit) AS IsHighlyRatedOwner
FROM dbo.Vehicles v
 JOIN dbo.VehicleCategories c ON c.CategoryId=v.CategoryId AND c.IsActive=1
 JOIN dbo.VehicleSearchProfiles p ON p.VehicleId=v.VehicleId AND p.IsPublished=1
 JOIN dbo.VehicleOwners o ON o.OwnerId=p.OwnerId AND o.IsActive=1 AND o.IsVerified=1
 JOIN dbo.RentalLocations l ON l.LocationId=p.LocationId AND l.IsActive=1
 JOIN dbo.Cities city ON city.CityId=l.CityId AND city.IsActive=1
 JOIN dbo.VehicleRentalModes m ON m.VehicleId=v.VehicleId AND m.IsEnabled=1
 LEFT JOIN dbo.VehicleRentalPolicies policy ON policy.VehicleId=m.VehicleId AND policy.RentalMode=m.RentalMode
 LEFT JOIN dbo.VehicleHourlyRates h ON h.VehicleId=m.VehicleId AND h.RentalMode=m.RentalMode AND h.IsActive=1
 LEFT JOIN dbo.VehicleLongTermRates r ON r.VehicleId=v.VehicleId AND m.RentalMode=''''LongTerm'''' AND r.IsActive=1
 LEFT JOIN dbo.vSearchOwnerRatings ratings ON ratings.OwnerId=p.OwnerId
 CROSS JOIN dbo.SearchSettings settings
WHERE v.OperationalStatus=''''Active'''' AND (m.RentalMode<>''''LongTerm'''' OR r.VehicleId IS NOT NULL)'');

EXEC(N''CREATE OR ALTER FUNCTION dbo.ufn_SearchAvailableVehicles(@StartUtc datetime2(0),@EndUtc datetime2(0),@NowUtc datetime2(0))
RETURNS TABLE AS RETURN
SELECT v.VehicleId FROM dbo.Vehicles v
WHERE @StartUtc>@NowUtc AND @EndUtc>@StartUtc AND v.OperationalStatus=''''Active''''
 AND NOT EXISTS(SELECT 1 FROM dbo.Bookings b WHERE b.VehicleId=v.VehicleId AND
 ((b.BookingStatus=''''InUse'''' AND b.StartAt<@EndUtc)
 OR (b.BookingStatus=''''Confirmed'''' AND b.StartAt<@EndUtc AND b.EndAt>@StartUtc)
 OR (b.BookingStatus=''''PendingDeposit'''' AND b.DepositDeadline>@NowUtc AND b.StartAt<@EndUtc AND b.EndAt>@StartUtc)))
 AND NOT EXISTS(SELECT 1 FROM dbo.MaintenanceRecords m WHERE m.VehicleId=v.VehicleId AND m.StartAt<@EndUtc AND m.EndAt>@StartUtc AND m.MaintenanceStatus IN(''''Scheduled'''',''''InProgress''''))
 AND NOT EXISTS(SELECT 1 FROM dbo.VehicleCalendarBlocks b WHERE b.VehicleId=v.VehicleId AND b.IsActive=1 AND b.StartAt<@EndUtc AND b.EndAt>@StartUtc)'');

EXEC(N''CREATE OR ALTER FUNCTION dbo.ufn_SearchDeliveryQuotes(@CityId int,@Latitude float,@Longitude float,@RentalMode varchar(12))
RETURNS TABLE AS RETURN
WITH origin AS
(SELECT p.VehicleId,l.Latitude,l.Longitude FROM dbo.VehicleSearchProfiles p JOIN dbo.RentalLocations l ON l.LocationId=p.LocationId WHERE p.IsPublished=1 AND l.IsActive=1 AND l.Latitude IS NOT NULL AND l.Longitude IS NOT NULL),
distances AS
(SELECT a.*,geography::Point(o.Latitude,o.Longitude,4326).STDistance(geography::Point(
 CASE WHEN @Latitude BETWEEN -90 AND 90 THEN @Latitude ELSE 0 END,
 CASE WHEN @Longitude BETWEEN -180 AND 180 THEN @Longitude ELSE 0 END,4326))/1000.0 AS DistanceKm
FROM dbo.VehicleDeliveryAreas a JOIN origin o ON o.VehicleId=a.VehicleId
JOIN dbo.VehicleRentalModes m ON m.VehicleId=a.VehicleId AND m.RentalMode=a.RentalMode AND m.IsEnabled=1
WHERE a.IsActive=1 AND a.CityId=@CityId AND a.RentalMode=@RentalMode AND @Latitude BETWEEN -90 AND 90 AND @Longitude BETWEEN -180 AND 180)
SELECT VehicleId,RentalMode,CityId,DistanceKm,
 CAST(CASE WHEN FreeWithinKm>0 AND DistanceKm<=FreeWithinKm THEN 0
 WHEN FeeModel=''''Flat'''' THEN FlatFee
 WHEN (DistanceKm-FreeWithinKm)*FeePerKm<MinimumFee THEN MinimumFee
 ELSE ROUND((DistanceKm-FreeWithinKm)*FeePerKm,0) END AS decimal(18,2)) AS DeliveryAndCollectionFee
FROM distances WHERE DistanceKm<=ServiceRadiusKm'');

INSERT dbo.Amenities(AmenityCode,AmenityName)
SELECT v.Code,v.Name FROM (VALUES(''ChildSeat'',N''Ghế trẻ em''),(''Bluetooth'',N''Bluetooth''),(''DashCamera'',N''Camera hành trình''),(''ReverseCamera'',N''Camera lùi''),(''SportPackage'',N''Phiên bản/thể thao đã xác minh'')) v(Code,Name)
WHERE NOT EXISTS(SELECT 1 FROM dbo.Amenities a WHERE a.AmenityCode=v.Code);
INSERT dbo.RentalPurposes(PurposeCode,PurposeName,Definition)
SELECT v.Code,v.Name,v.Definition FROM (VALUES
 (''NewDriver'',N''Lái mới'',N''Chủ xe xác nhận phù hợp người mới có giấy phép; không thay thế kiểm tra bằng lái.''),
 (''Business'',N''Công việc, đi lại'',N''Chủ xe xác nhận phù hợp công việc.''),
 (''Family'',N''Gia đình'',N''Chủ xe xác nhận phù hợp gia đình; ghế trẻ em là tiêu chí riêng.''),
 (''Camping'',N''Cắm trại, chở đồ'',N''Được chủ xe cho phép, tuân thủ tải trọng và phạm vi di chuyển.''),
 (''Friends'',N''Nhóm bạn'',N''Phù hợp đi nhóm; số chỗ vẫn là ràng buộc bắt buộc.''),
 (''Event'',N''Tiếp khách, dự tiệc'',N''Chủ xe xác nhận phục vụ sự kiện.'')) v(Code,Name,Definition)
WHERE NOT EXISTS(SELECT 1 FROM dbo.RentalPurposes p WHERE p.PurposeCode=v.Code);
COMMIT TRANSACTION;
PRINT N''Search metadata installed on CRMS. No vehicle capabilities were inferred.'';
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;');
EXEC(N'-- Additive upgrade: preserve existing bookings and their original daily pricing.
IF COL_LENGTH(''dbo.Bookings'',''PriceUnit'') IS NULL
 ALTER TABLE dbo.Bookings ADD PriceUnit varchar(5) NOT NULL CONSTRAINT DF_Bookings_PriceUnit DEFAULT(''Day'') WITH VALUES;
IF COL_LENGTH(''dbo.Bookings'',''RentalUnits'') IS NULL
 ALTER TABLE dbo.Bookings ADD RentalUnits decimal(10,2) NULL;
IF COL_LENGTH(''dbo.Bookings'',''UnitRate'') IS NULL
 ALTER TABLE dbo.Bookings ADD UnitRate decimal(18,2) NULL;');
EXEC(N'UPDATE dbo.Bookings SET RentalUnits=RentalDays,UnitRate=VehicleDailyRate
 WHERE PriceUnit=''Day'' AND RentalUnits IS NULL AND UnitRate IS NULL;
ALTER TABLE dbo.Bookings DROP CONSTRAINT CK_Bookings_RentalType;
ALTER TABLE dbo.Bookings WITH CHECK ADD CONSTRAINT CK_Bookings_RentalType CHECK(RentalType IN(''SelfDrive'',''WithDriver'',''LongTerm''));
ALTER TABLE dbo.Bookings DROP CONSTRAINT CK_Bookings_SelfDrive;
ALTER TABLE dbo.Bookings WITH CHECK ADD CONSTRAINT CK_Bookings_SelfDrive CHECK(
 (RentalType IN(''SelfDrive'',''LongTerm'') AND DriverId IS NULL AND DriverDailyRate=0 AND DriverAmount=0) OR RentalType=''WithDriver'');
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(''dbo.Bookings'') AND name=''CK_Bookings_Billing'')
 ALTER TABLE dbo.Bookings WITH CHECK ADD CONSTRAINT CK_Bookings_Billing CHECK(
 ((RentalType=''SelfDrive'' AND PriceUnit IN(''Hour'',''Day'')) OR (RentalType=''WithDriver'' AND PriceUnit=''Day'') OR (RentalType=''LongTerm'' AND PriceUnit=''Month''))
 AND ((PriceUnit=''Day'' AND RentalUnits IS NULL AND UnitRate IS NULL) OR (RentalUnits IS NOT NULL AND RentalUnits>0 AND UnitRate IS NOT NULL AND UnitRate>0)));');
EXEC(N'EXEC(N''CREATE OR ALTER TRIGGER dbo.tr_Search_BookingSnapshot ON dbo.BookingOfferSnapshots
AFTER INSERT, UPDATE, DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 52002,''''Booking offer snapshots are immutable. Do not edit or delete history.'''',1;
 IF EXISTS(SELECT 1 FROM inserted s JOIN dbo.Bookings b ON b.BookingId=s.BookingId
   LEFT JOIN dbo.VehicleSearchProfiles p ON p.VehicleId=s.VehicleId
   WHERE b.VehicleId<>s.VehicleId OR b.RentalType<>s.RentalMode
      OR s.PriceUnit<>b.PriceUnit OR b.BookingStatus NOT IN(''''PendingDeposit'''',''''Confirmed'''')
      OR p.OwnerId IS NULL OR p.OwnerId<>s.OwnerId OR p.IsPublished<>1 OR p.VerifiedAt IS NULL)
   THROW 52003,''''Snapshot must match the actual booking and verified current owner at booking time.'''',1;
END'');');
EXEC(N'-- Atomic booking. Legacy callers default to Day; LongTerm defaults to Month.
CREATE OR ALTER PROCEDURE dbo.usp_CreateBooking
 @CustomerId bigint, @VehicleId bigint, @RentalType varchar(12), @DepositMethod varchar(5),
 @StartAt datetime2(0), @EndAt datetime2(0), @RequestKey uniqueidentifier,
 @NewBookingId bigint OUTPUT, @PriceUnit varchar(5)=NULL, @DurationMonths tinyint=NULL, @ExpectedTotal decimal(18,2)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @Now datetime2(0)=SYSUTCDATETIME(),@ExistingId bigint,@LockResult int,
 @Resource nvarchar(255)=CONCAT(N''CRMS_VEHICLE_'',@VehicleId),@DailyRate decimal(18,2),@Rate decimal(18,2),
 @DriverRate decimal(18,2),@Percent decimal(5,2),@OnlineHold int,@CashHold int,@Days int,
 @Units decimal(10,2),@Car decimal(18,2),@Driver decimal(18,2),@Total decimal(18,2),@Deposit decimal(18,2),
 @Owner bigint,@ProfileExists bit=0,@MinHours tinyint,@MaxHours tinyint,@Increment int,@Seconds bigint;
 SET @PriceUnit=COALESCE(@PriceUnit,CASE WHEN @RentalType=''LongTerm'' THEN ''Month'' ELSE ''Day'' END);
 IF @RequestKey IS NULL OR @StartAt IS NULL OR @EndAt IS NULL OR @EndAt<=@StartAt
  OR @RentalType IS NULL OR @PriceUnit IS NULL OR @DepositMethod IS NULL
  OR @DepositMethod NOT IN(''Cash'',''VNPay'')
  OR NOT ((@RentalType=''SelfDrive'' AND @PriceUnit IN(''Hour'',''Day'')) OR (@RentalType=''WithDriver'' AND @PriceUnit=''Day'') OR (@RentalType=''LongTerm'' AND @PriceUnit=''Month''))
  OR (@PriceUnit<>''Month'' AND @DurationMonths IS NOT NULL)
  THROW 51000,''Invalid booking dates, mode or billing unit.'',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  -- Serialize identical retry tokens before checking the vehicle.
  DECLARE @RequestResource nvarchar(255)=CONCAT(N''CRMS_REQUEST_'',CONVERT(varchar(36),@RequestKey));
  EXEC @LockResult=sys.sp_getapplock @Resource=@RequestResource,@LockMode=''Exclusive'',@LockOwner=''Transaction'',@LockTimeout=10000;
  IF @LockResult<0 THROW 51003,''Could not acquire request lock.'',1;
  SELECT @ExistingId=BookingId FROM dbo.Bookings WHERE RequestKey=@RequestKey;
  IF @ExistingId IS NOT NULL
  BEGIN
   IF NOT EXISTS(SELECT 1 FROM dbo.Bookings WHERE BookingId=@ExistingId AND CustomerId=@CustomerId AND VehicleId=@VehicleId
    AND StartAt=@StartAt AND EndAt=@EndAt AND RentalType=@RentalType AND DepositMethod=@DepositMethod AND PriceUnit=@PriceUnit
    AND (@PriceUnit<>''Month'' OR RentalUnits=@DurationMonths))
    THROW 51007,''Request key already belongs to a different booking request.'',1;
   SET @NewBookingId=@ExistingId; COMMIT; RETURN;
  END;
  IF @StartAt<=@Now THROW 51000,''Pickup must be in the future.'',1;
  IF NOT EXISTS(SELECT 1 FROM dbo.Users WHERE UserId=@CustomerId AND UserRole=''Customer'' AND IsActive=1)
   THROW 51001,''Customer is not active.'',1;
  EXEC @LockResult=sys.sp_getapplock @Resource=@Resource,@LockMode=''Exclusive'',@LockOwner=''Transaction'',@LockTimeout=10000;
  IF @LockResult<0 THROW 51003,''Could not acquire vehicle reservation lock.'',1;
  SET @Now=SYSUTCDATETIME();
  IF @StartAt<=@Now THROW 51000,''Pickup elapsed while waiting for reservation lock.'',1;
  SELECT @DailyRate=v.DailyRate FROM dbo.Vehicles v WITH(UPDLOCK,HOLDLOCK)
   JOIN dbo.VehicleCategories c ON c.CategoryId=v.CategoryId AND c.IsActive=1
   WHERE v.VehicleId=@VehicleId AND v.OperationalStatus=''Active'';
  IF @DailyRate IS NULL THROW 51002,''Vehicle or category is not active.'',1;
  IF EXISTS(SELECT 1 FROM dbo.VehicleSearchProfiles WHERE VehicleId=@VehicleId) SET @ProfileExists=1;
  IF @ProfileExists=1
  BEGIN
   SELECT @Owner=p.OwnerId FROM dbo.VehicleSearchProfiles p WITH(UPDLOCK,HOLDLOCK)
    JOIN dbo.VehicleOwners o ON o.OwnerId=p.OwnerId AND o.IsActive=1 AND o.IsVerified=1
    JOIN dbo.RentalLocations l ON l.LocationId=p.LocationId AND l.IsActive=1
    JOIN dbo.Cities c ON c.CityId=l.CityId AND c.IsActive=1
    JOIN dbo.VehicleRentalModes m ON m.VehicleId=p.VehicleId AND m.RentalMode=@RentalType AND m.IsEnabled=1 AND m.VerifiedAt IS NOT NULL
    WHERE p.VehicleId=@VehicleId AND p.IsPublished=1 AND p.VerifiedAt IS NOT NULL;
   IF @Owner IS NULL THROW 51008,''Vehicle is not published for this rental mode.'',1;
  END;
  IF @ProfileExists=0 AND @PriceUnit<>''Day'' THROW 51008,''Hourly/monthly offers require a verified search profile.'',1;
  IF EXISTS(SELECT 1 FROM dbo.Bookings WITH(UPDLOCK,HOLDLOCK) WHERE VehicleId=@VehicleId AND
   ((BookingStatus=''InUse'' AND StartAt<@EndAt) OR (BookingStatus=''Confirmed'' AND StartAt<@EndAt AND EndAt>@StartAt)
    OR (BookingStatus=''PendingDeposit'' AND DepositDeadline>@Now AND StartAt<@EndAt AND EndAt>@StartAt)))
   THROW 51004,''Vehicle already booked during requested dates.'',1;
  IF EXISTS(SELECT 1 FROM dbo.MaintenanceRecords WITH(UPDLOCK,HOLDLOCK) WHERE VehicleId=@VehicleId AND StartAt<@EndAt AND EndAt>@StartAt AND MaintenanceStatus IN(''Scheduled'',''InProgress''))
   THROW 51005,''Vehicle is unavailable due to maintenance.'',1;
  IF EXISTS(SELECT 1 FROM dbo.VehicleCalendarBlocks WITH(UPDLOCK,HOLDLOCK) WHERE VehicleId=@VehicleId AND IsActive=1 AND StartAt<@EndAt AND EndAt>@StartAt)
   THROW 51009,''Vehicle calendar is blocked during requested dates.'',1;
  SELECT @Percent=DepositPercent,@DriverRate=DriverDailyFee,@OnlineHold=OnlineHoldMinutes,@CashHold=CashHoldHours FROM dbo.RentalPolicies WHERE PolicyId=1;
  IF @Percent IS NULL THROW 51010,''Rental policy is missing.'',1;
  SET @Seconds=DATEDIFF_BIG(second,@StartAt,@EndAt);
  SET @Days=CONVERT(int,(@Seconds+86399)/86400);
  IF @PriceUnit=''Month''
  BEGIN
   IF @DurationMonths IS NULL OR @DurationMonths NOT IN(1,3,6,9,12) OR DATEADD(month,@DurationMonths,@StartAt)<>@EndAt
    THROW 51011,''Monthly booking must match a supported complete calendar-month plan.'',1;
   SELECT @Rate=MonthlyRate FROM dbo.VehicleLongTermRates WITH(UPDLOCK,HOLDLOCK) WHERE VehicleId=@VehicleId AND DurationMonths=@DurationMonths AND IsActive=1;
   IF @Rate IS NULL THROW 51012,''Monthly plan is unavailable.'',1;
   SET @Units=@DurationMonths;
  END
  ELSE IF @PriceUnit=''Hour''
  BEGIN
   SELECT @Rate=HourlyRate,@MinHours=MinimumHours,@MaxHours=MaximumHours,@Increment=BillingIncrementMinutes
    FROM dbo.VehicleHourlyRates WITH(UPDLOCK,HOLDLOCK) WHERE VehicleId=@VehicleId AND RentalMode=''SelfDrive'' AND IsActive=1;
   IF @Rate IS NULL OR @Seconds<@MinHours*3600 OR @Seconds>@MaxHours*3600
    THROW 51013,''Hourly offer or requested duration is invalid.'',1;
   SET @Units=CEILING(@Seconds/(@Increment*60.0))*@Increment/60.0;
  END
  ELSE
  BEGIN
   IF @Days>365 THROW 51006,''Daily rental cannot exceed 365 days.'',1;
   SET @Rate=@DailyRate; SET @Units=@Days;
  END;
  IF @RentalType<>''WithDriver'' SET @DriverRate=0;
  SET @Car=ROUND(@Rate*@Units,2); SET @Driver=@DriverRate*@Days;
  SET @Total=@Car+@Driver; SET @Deposit=ROUND(@Total*@Percent/100,2);
  IF @ExpectedTotal IS NOT NULL AND (@ExpectedTotal<=0 OR @ExpectedTotal<>@Total)
    THROW 51014,''Price changed. Refresh the quote before confirming.'',1;
  INSERT dbo.Bookings(BookingCode,RequestKey,CustomerId,VehicleId,RentalType,DepositMethod,StartAt,EndAt,RentalDays,
   VehicleDailyRate,DriverDailyRate,VehicleAmount,DriverAmount,DiscountAmount,TotalAmount,DepositAmount,DepositDeadline,PriceUnit,RentalUnits,UnitRate)
  VALUES(CONCAT(''CR-'',CONVERT(varchar(8),@Now,112),''-'',LEFT(REPLACE(CONVERT(varchar(36),NEWID()),''-'',''''),12)),
   @RequestKey,@CustomerId,@VehicleId,@RentalType,@DepositMethod,@StartAt,@EndAt,@Days,@DailyRate,@DriverRate,@Car,@Driver,0,@Total,@Deposit,
   CASE WHEN @DepositMethod=''VNPay'' THEN DATEADD(minute,@OnlineHold,@Now) ELSE DATEADD(hour,@CashHold,@Now) END,@PriceUnit,@Units,@Rate);
  SET @NewBookingId=SCOPE_IDENTITY();
  IF @Owner IS NOT NULL
  BEGIN
   DECLARE @Snapshot nvarchar(max)=(SELECT @PriceUnit AS priceUnit,@Units AS rentalUnits,@Rate AS unitRate,
    @DriverRate AS driverDailyRate,@Total AS totalBeforeVoucher,
    JSON_QUERY((SELECT * FROM dbo.VehicleRentalPolicies WHERE VehicleId=@VehicleId AND RentalMode=@RentalType FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)) AS rentalPolicy
    FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
   INSERT dbo.BookingOfferSnapshots(BookingId,VehicleId,OwnerId,RentalMode,PriceUnit,PolicySnapshotJson)
    VALUES(@NewBookingId,@VehicleId,@Owner,@RentalType,@PriceUnit,@Snapshot);
  END;
  INSERT dbo.BookingStatusHistory(BookingId,NewStatus,ChangeNote) VALUES(@NewBookingId,''PendingDeposit'',N''Reservation created'');
  COMMIT;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK;
  THROW;
 END CATCH;
END;');
EXEC(N'/*
  FOLLOW-UP SERVICE OPERATIONS TO IMPLEMENT IN EF CORE TRANSACTIONS:
  - Voucher: call dbo.usp_ApplyVoucher BEFORE creating any deposit payment attempt.
  - Create pending Payments row with exact DepositAmount, Method=VNPay/Cash.
  - VNPay: verify HMAC, merchant reference, amount and success response in BACKEND/IPN;
    then call dbo.usp_ConfirmDeposit, NEVER from the browser return URL.
  - Cash: after Staff/Admin sees cash, call dbo.usp_ConfirmDeposit with verifier id.
  - Periodically call dbo.usp_ExpirePendingBookings to release unpaid holds.
  - On cancellation (only before handover), release car/driver hold, record manual refund.
  - Assign driver with driver-specific transaction lock and overlapping range checks.
  - Schedule maintenance with the SAME CRMS_VEHICLE_{id} application lock.
  - On return: charge Balance = TotalAmount + LateFee + DamageFee + OtherFee - NetPaid;
    NetPaid = sum(Success Deposit/Balance) - sum(Success Refund).
  - Complete booking only after return and sufficient valid payments.
  - Validate permissions and status transitions in server-side service, not only UI.
*/

-- 10. VOUCHER APPLICATION: one voucher per booking, reserve quota transactionally.
CREATE OR ALTER PROCEDURE dbo.usp_ApplyVoucher
    @BookingId BIGINT,
    @CustomerId BIGINT,
    @VoucherCode NVARCHAR(40),
    @DiscountAmount DECIMAL(18,2) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Now DATETIME2(0) = SYSUTCDATETIME();
    DECLARE @Gross DECIMAL(18,2), @PolicyPercent DECIMAL(5,2), @VoucherId BIGINT;
    DECLARE @Type VARCHAR(10), @Value DECIMAL(18,2), @Max DECIMAL(18,2), @Minimum DECIMAL(18,2);
    DECLARE @GlobalLimit INT, @PerCustomerLimit INT, @ActiveCount INT, @CustomerCount INT;
    DECLARE @ExistingVoucherId BIGINT;
    DECLARE @BookingDeadline datetime2(0),@VoucherStarts datetime2(0),@VoucherEnds datetime2(0);
    BEGIN TRY
      BEGIN TRANSACTION;
      SELECT @Gross = VehicleAmount + DriverAmount,@BookingDeadline=DepositDeadline
      FROM dbo.Bookings WITH (UPDLOCK, HOLDLOCK)
      WHERE BookingId = @BookingId AND CustomerId = @CustomerId
        AND BookingStatus = ''PendingDeposit'';
      SET @Now=SYSUTCDATETIME();
      IF @BookingDeadline<=@Now THROW 51100,''Reservation expired while waiting for its lock.'',1;
      IF @Gross IS NULL THROW 51100, ''Booking is not eligible for a voucher.'', 1;
      -- Price is frozen once any payment attempt exists.
      IF EXISTS (SELECT 1 FROM dbo.Payments WITH (UPDLOCK, HOLDLOCK) WHERE BookingId = @BookingId)
        THROW 51101, ''Voucher must be applied before initiating payment.'', 1;
      SELECT @ExistingVoucherId = VoucherId FROM dbo.BookingVouchers WHERE BookingId = @BookingId;
      IF @ExistingVoucherId IS NOT NULL THROW 51102, ''Booking already has a voucher.'', 1;
      SELECT @VoucherId = VoucherId, @Type = DiscountType, @Value = DiscountValue,
             @Max = MaxDiscountAmount, @Minimum = MinimumOrderAmount,
             @GlobalLimit = UsageLimit, @PerCustomerLimit = PerCustomerLimit,
             @VoucherStarts=StartsAt,@VoucherEnds=EndsAt
      FROM dbo.Vouchers WITH (UPDLOCK, HOLDLOCK)
      WHERE VoucherCode = @VoucherCode AND IsActive = 1 AND StartsAt <= @Now AND EndsAt > @Now;
      SET @Now=SYSUTCDATETIME();
      IF @VoucherId IS NULL OR @VoucherStarts>@Now OR @VoucherEnds<=@Now OR @BookingDeadline<=@Now
        THROW 51103, ''Voucher or reservation expired while waiting for its lock.'', 1;
      -- Explicit scope is an allow-list; no scope rows preserves legacy global vouchers.
      IF EXISTS(SELECT 1 FROM dbo.VehicleVoucherEligibility WITH(UPDLOCK,HOLDLOCK) WHERE VoucherId=@VoucherId)
         AND NOT EXISTS(SELECT 1 FROM dbo.Bookings b JOIN dbo.VehicleVoucherEligibility e
           ON e.VehicleId=b.VehicleId AND e.RentalMode=b.RentalType
           WHERE b.BookingId=@BookingId AND e.VoucherId=@VoucherId AND e.IsActive=1)
        THROW 51107,''Voucher is not eligible for this vehicle and rental mode.'',1;
      IF @Gross < @Minimum THROW 51104, ''Minimum order amount not reached.'', 1;
      SELECT @ActiveCount = COUNT_BIG(*) FROM dbo.BookingVouchers
      WHERE VoucherId = @VoucherId AND RedemptionStatus IN (''Reserved'',''Applied'');
      SELECT @CustomerCount = COUNT_BIG(*) FROM dbo.BookingVouchers bv
      JOIN dbo.Bookings b ON b.BookingId = bv.BookingId
      WHERE bv.VoucherId = @VoucherId AND b.CustomerId = @CustomerId
        AND bv.RedemptionStatus IN (''Reserved'',''Applied'');
      IF (@GlobalLimit IS NOT NULL AND @ActiveCount >= @GlobalLimit)
         OR @CustomerCount >= @PerCustomerLimit
          THROW 51105, ''Voucher usage quota exceeded.'', 1;
      SET @DiscountAmount = CASE WHEN @Type = ''Percent''
          THEN ROUND(@Gross * @Value / 100, 2) ELSE @Value END;
      IF @Max IS NOT NULL AND @DiscountAmount > @Max SET @DiscountAmount = @Max;
      -- Require a positive post-discount amount for the mandatory 50% deposit.
      IF @DiscountAmount >= @Gross THROW 51106, ''Voucher cannot make a rental free.'', 1;
      SELECT @PolicyPercent = DepositPercent FROM dbo.RentalPolicies WHERE PolicyId = 1;
      UPDATE dbo.Bookings SET
          DiscountAmount = @DiscountAmount,
          TotalAmount = @Gross - @DiscountAmount,
          DepositAmount = ROUND((@Gross - @DiscountAmount) * @PolicyPercent / 100.0, 2),
          UpdatedAt = @Now
      WHERE BookingId = @BookingId;
      INSERT INTO dbo.BookingVouchers(BookingId, VoucherId, DiscountSnapshot)
      VALUES (@BookingId, @VoucherId, @DiscountAmount);
      COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
      IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
      THROW;
    END CATCH
END;');
EXEC(N'-- 11. CONFIRM DEPOSIT (after server-side VNPay HMAC and amount validation, or Staff cash receipt).
-- If a valid payment arrives too late for an expired/cancelled booking, record it
-- but keep booking inactive; caller MUST trigger manual refund handling.
CREATE OR ALTER PROCEDURE dbo.usp_ConfirmDeposit
    @PaymentId BIGINT,
    @VerifiedByUserId BIGINT = NULL,
    @GatewayTransactionNo VARCHAR(100) = NULL,
    @GatewayResponseCode VARCHAR(20) = NULL,
    @NeedsManualRefund BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Now DATETIME2(0) = SYSUTCDATETIME();
    DECLARE @BookingId BIGINT, @PayAmount DECIMAL(18,2), @Required DECIMAL(18,2);
    DECLARE @Method VARCHAR(12), @PaymentStatus VARCHAR(10), @BookingStatus VARCHAR(16);
    DECLARE @Deadline DATETIME2(0);
    SET @NeedsManualRefund = 0;
    BEGIN TRY
      BEGIN TRANSACTION;
      SELECT @BookingId = BookingId, @PayAmount = Amount,
             @Method = PaymentMethod, @PaymentStatus = PaymentStatus
      FROM dbo.Payments WITH (UPDLOCK, HOLDLOCK)
      WHERE PaymentId = @PaymentId AND PaymentType = ''Deposit'';
      IF @BookingId IS NULL THROW 51200, ''Deposit payment does not exist.'', 1;
      SELECT @Required = DepositAmount, @BookingStatus = BookingStatus, @Deadline = DepositDeadline
      FROM dbo.Bookings WITH (UPDLOCK, HOLDLOCK) WHERE BookingId = @BookingId;
      SET @Now=SYSUTCDATETIME();
      IF @PaymentStatus = ''Success''
      BEGIN
          IF @BookingStatus IN (''Expired'',''Cancelled'') SET @NeedsManualRefund = 1;
          COMMIT TRANSACTION;
          RETURN;
      END;
      IF @PaymentStatus <> ''Pending'' THROW 51201, ''Payment is not pending.'', 1;
      IF @PayAmount <> @Required THROW 51202, ''Paid amount differs from deposit amount.'', 1;
      IF @Method = ''Cash''
      BEGIN
          IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = @VerifiedByUserId
                AND UserRole IN (''Staff'',''Admin'') AND IsActive = 1)
              THROW 51203, ''Cash deposit requires an authorized Staff/Admin confirmation.'', 1;
      END
      ELSE IF @Method = ''VNPay''
      BEGIN
          IF NULLIF(LTRIM(RTRIM(@GatewayTransactionNo)), '''') IS NULL
              OR @GatewayResponseCode IS NULL OR @GatewayResponseCode <> ''00''
              THROW 51204, ''VNPay success details missing/invalid.'', 1;
          -- WARNING: HMAC and payment reference MUST already be validated by backend.
      END
      ELSE THROW 51205, ''Invalid deposit payment method.'', 1;

      UPDATE dbo.Payments SET PaymentStatus = ''Success'', PaidAt = @Now,
          VerifiedByUserId = @VerifiedByUserId,
          GatewayTransactionNo = CASE WHEN @Method = ''VNPay'' THEN @GatewayTransactionNo ELSE GatewayTransactionNo END,
          GatewayResponseCode = CASE WHEN @Method = ''VNPay'' THEN @GatewayResponseCode ELSE GatewayResponseCode END
      WHERE PaymentId = @PaymentId;

      IF @BookingStatus = ''PendingDeposit'' AND @Deadline > @Now
      BEGIN
          UPDATE dbo.Bookings SET BookingStatus = ''Confirmed'', ConfirmedAt = @Now, UpdatedAt = @Now
          WHERE BookingId = @BookingId;
          UPDATE dbo.BookingVouchers SET RedemptionStatus = ''Applied'', AppliedAt = @Now
          WHERE BookingId = @BookingId AND RedemptionStatus = ''Reserved'';
          INSERT INTO dbo.BookingStatusHistory(BookingId, OldStatus, NewStatus, ChangedByUserId, ChangeNote)
          VALUES (@BookingId, ''PendingDeposit'', ''Confirmed'', @VerifiedByUserId, N''Deposit confirmed'');
      END
      ELSE
      BEGIN
          SET @NeedsManualRefund = 1;
          IF @BookingStatus = ''PendingDeposit''
          BEGIN
              UPDATE dbo.Bookings SET BookingStatus=''Expired'', UpdatedAt=@Now WHERE BookingId=@BookingId;
              UPDATE dbo.BookingVouchers SET RedemptionStatus=''Released''
              WHERE BookingId=@BookingId AND RedemptionStatus=''Reserved'';
              INSERT INTO dbo.BookingStatusHistory(BookingId, OldStatus, NewStatus, ChangeNote)
              VALUES (@BookingId, ''PendingDeposit'', ''Expired'', N''Deposit received after reservation deadline'');
          END
      END;
      COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
      IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
      THROW;
    END CATCH
END;');
EXEC(N'-- 12. RELEASE EXPIRED HOLDS (call from recurring background worker).
CREATE OR ALTER PROCEDURE dbo.usp_ExpirePendingBookings
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Expired TABLE(BookingId BIGINT PRIMARY KEY);
    BEGIN TRANSACTION;
    UPDATE dbo.Bookings SET BookingStatus = ''Expired'', UpdatedAt = SYSUTCDATETIME()
      OUTPUT inserted.BookingId INTO @Expired(BookingId)
    WHERE BookingStatus = ''PendingDeposit'' AND DepositDeadline <= SYSUTCDATETIME();
    UPDATE bv SET RedemptionStatus = ''Released''
    FROM dbo.BookingVouchers bv JOIN @Expired x ON x.BookingId = bv.BookingId
    WHERE bv.RedemptionStatus = ''Reserved'';
    INSERT INTO dbo.BookingStatusHistory(BookingId, OldStatus, NewStatus, ChangeNote)
    SELECT BookingId, ''PendingDeposit'', ''Expired'', N''Deposit deadline elapsed''
    FROM @Expired;
    COMMIT TRANSACTION;
END;');
EXEC(N'-- 13. DRIVER ASSIGNMENT: prevent two active bookings using the same driver at once.
CREATE OR ALTER PROCEDURE dbo.usp_AssignDriver
    @BookingId BIGINT,
    @DriverId BIGINT,
    @StaffUserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Start DATETIME2(0), @End DATETIME2(0), @CurrentDriver BIGINT;
    DECLARE @LockResult INT;
    DECLARE @LockResource NVARCHAR(255);
    BEGIN TRY
      BEGIN TRANSACTION;
      IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = @StaffUserId
                   AND UserRole IN (''Staff'',''Admin'') AND IsActive = 1)
          THROW 51300, ''Only Staff/Admin may assign a driver.'', 1;
      SET @LockResource = CONCAT(N''CRMS_DRIVER_'', @DriverId);
      EXEC @LockResult = sys.sp_getapplock
          @Resource = @LockResource,
          @LockMode = ''Exclusive'', @LockOwner = ''Transaction'', @LockTimeout = 10000;
      IF @LockResult < 0 THROW 51301, ''Driver lock unavailable.'', 1;
      SELECT @Start = StartAt, @End = EndAt, @CurrentDriver = DriverId
      FROM dbo.Bookings WITH (UPDLOCK, HOLDLOCK)
      WHERE BookingId = @BookingId AND BookingStatus = ''Confirmed'' AND RentalType = ''WithDriver'';
      IF @Start IS NULL THROW 51302, ''Booking does not need/allow driver assignment.'', 1;
      IF NOT EXISTS (SELECT 1 FROM dbo.Drivers WHERE DriverId = @DriverId AND IsActive = 1)
          THROW 51303, ''Driver is inactive or missing.'', 1;
      IF EXISTS (SELECT 1 FROM dbo.Bookings WITH (UPDLOCK, HOLDLOCK)
         WHERE DriverId = @DriverId AND BookingId <> @BookingId
         AND ((BookingStatus = ''Confirmed'' AND StartAt < @End AND EndAt > @Start)
              OR (BookingStatus = ''InUse'' AND StartAt < @End)))
          THROW 51304, ''Driver already assigned in this period.'', 1;
      UPDATE dbo.Bookings SET DriverId = @DriverId, UpdatedAt = SYSUTCDATETIME()
      WHERE BookingId = @BookingId;
      COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
      IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
      THROW;
    END CATCH
END;');
EXEC(N'-- 14. MAINTENANCE SCHEDULING: same per-vehicle lock as booking creation.
CREATE OR ALTER PROCEDURE dbo.usp_ScheduleMaintenance
    @VehicleId BIGINT,
    @CreatedByUserId BIGINT,
    @StartAt DATETIME2(0),
    @EndAt DATETIME2(0),
    @Description NVARCHAR(1500),
    @MaintenanceId BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @LockResult INT, @Now DATETIME2(0) = SYSUTCDATETIME();
    DECLARE @LockResource NVARCHAR(255);
    IF @EndAt <= @StartAt OR @StartAt <= @Now THROW 51400, ''Invalid maintenance period.'', 1;
    BEGIN TRY
      BEGIN TRANSACTION;
      IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = @CreatedByUserId
                     AND UserRole = ''Admin'' AND IsActive = 1)
          THROW 51401, ''Only Admin may schedule maintenance.'', 1;
      SET @LockResource = CONCAT(N''CRMS_VEHICLE_'', @VehicleId);
      EXEC @LockResult = sys.sp_getapplock
          @Resource = @LockResource,
          @LockMode = ''Exclusive'', @LockOwner = ''Transaction'', @LockTimeout = 10000;
      IF @LockResult < 0 THROW 51402, ''Vehicle lock unavailable.'', 1;
      SET @Now=SYSUTCDATETIME();
      IF @StartAt IS NULL OR @EndAt IS NULL OR @StartAt<=@Now OR @EndAt<=@StartAt
          THROW 51400,''Maintenance period elapsed while waiting for vehicle lock.'',1;
      IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE VehicleId = @VehicleId)
          THROW 51403, ''Vehicle not found.'', 1;
      IF EXISTS (SELECT 1 FROM dbo.Bookings WITH (UPDLOCK, HOLDLOCK)
                WHERE VehicleId = @VehicleId
                  AND ((BookingStatus = ''InUse'' AND StartAt < @EndAt)
                     OR (BookingStatus = ''Confirmed'' AND StartAt < @EndAt AND EndAt > @StartAt)
                     OR (BookingStatus = ''PendingDeposit'' AND DepositDeadline > @Now AND StartAt < @EndAt AND EndAt > @StartAt)))
          THROW 51404, ''Vehicle already booked for maintenance period.'', 1;
      IF EXISTS (SELECT 1 FROM dbo.MaintenanceRecords WITH (UPDLOCK, HOLDLOCK)
                WHERE VehicleId = @VehicleId AND StartAt < @EndAt AND EndAt > @StartAt
                  AND MaintenanceStatus IN (''Scheduled'',''InProgress''))
          THROW 51405, ''Overlapping maintenance schedule.'', 1;
      IF EXISTS(SELECT 1 FROM dbo.VehicleCalendarBlocks WITH(UPDLOCK,HOLDLOCK)
          WHERE VehicleId=@VehicleId AND IsActive=1 AND StartAt<@EndAt AND EndAt>@StartAt)
          THROW 51406,''Vehicle has an active calendar block.'',1;
      INSERT INTO dbo.MaintenanceRecords(VehicleId, CreatedByUserId, StartAt, EndAt, Description)
      VALUES (@VehicleId, @CreatedByUserId, @StartAt, @EndAt, @Description);
      SET @MaintenanceId = SCOPE_IDENTITY();
      COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
      IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
      THROW;
    END CATCH
END;');
EXEC(N'-- 15. CANCELLATION: only before vehicle handover; manual refund is a separate action.
CREATE OR ALTER PROCEDURE dbo.usp_CancelBooking
    @BookingId BIGINT,
    @CancelledByUserId BIGINT,
    @Reason NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @CustomerId BIGINT, @OldStatus VARCHAR(16), @Now DATETIME2(0)=SYSUTCDATETIME();
    DECLARE @Paid DECIMAL(18,2);
    BEGIN TRY
      BEGIN TRANSACTION;
      SELECT @CustomerId=CustomerId, @OldStatus=BookingStatus
      FROM dbo.Bookings WITH (UPDLOCK, HOLDLOCK) WHERE BookingId=@BookingId;
      IF @CustomerId IS NULL THROW 51500, ''Booking not found.'', 1;
      IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId=@CancelledByUserId AND IsActive=1
          AND (UserId=@CustomerId OR UserRole IN (''Staff'',''Admin'')))
          THROW 51501, ''Not authorized to cancel this booking.'', 1;
      IF @OldStatus=''Cancelled''
      BEGIN
          COMMIT TRANSACTION;
          RETURN;
      END;
      IF @OldStatus NOT IN (''PendingDeposit'',''Confirmed'')
          THROW 51502, ''Only uncollected bookings can be cancelled.'', 1;
      IF EXISTS (SELECT 1 FROM dbo.VehicleHandovers WHERE BookingId=@BookingId)
          THROW 51503, ''Vehicle has already been handed over.'', 1;
      UPDATE dbo.Bookings SET BookingStatus=''Cancelled'', CancelledAt=@Now,
          CancelReason=@Reason, UpdatedAt=@Now WHERE BookingId=@BookingId;
      UPDATE dbo.BookingVouchers SET RedemptionStatus=''Released'' WHERE BookingId=@BookingId
          AND RedemptionStatus IN (''Reserved'',''Applied'');
      INSERT INTO dbo.BookingStatusHistory(BookingId,OldStatus,NewStatus,ChangedByUserId,ChangeNote)
      VALUES (@BookingId,@OldStatus,''Cancelled'',@CancelledByUserId,@Reason);
      -- A paid deposit does NOT automatically disappear; Staff must refund it.
      SELECT @Paid = COALESCE(SUM(CASE WHEN PaymentType=''Refund'' THEN -Amount ELSE Amount END),0)
      FROM dbo.Payments WHERE BookingId=@BookingId AND PaymentStatus=''Success'';
      IF @Paid>0 AND NOT EXISTS (SELECT 1 FROM dbo.Payments
                               WHERE BookingId=@BookingId AND PaymentType=''Refund'' AND PaymentStatus=''Pending'')
          INSERT INTO dbo.Payments(BookingId,PaymentType,PaymentMethod,Amount,Note)
          VALUES(@BookingId,''Refund'',''BankTransfer'',@Paid,N''Manual refund pending after cancellation'');
      COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
      IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
      THROW;
    END CATCH
END;');
EXEC(N'-- 16. STAFF HANDOVER: booking must be confirmed; chauffeur requires an assigned driver.
CREATE OR ALTER PROCEDURE dbo.usp_HandoverVehicle
    @BookingId BIGINT, @StaffUserId BIGINT, @OdometerOutKm INT,
    @FuelPercentOut TINYINT, @ConditionNotes NVARCHAR(2000)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Status VARCHAR(16), @RentalType VARCHAR(12), @DriverId BIGINT, @VehicleId BIGINT;
    BEGIN TRY
      BEGIN TRANSACTION;
      IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId=@StaffUserId AND IsActive=1
                     AND UserRole IN (''Staff'',''Admin''))
          THROW 51600, ''Only Staff/Admin may hand over a vehicle.'', 1;
      SELECT @Status=BookingStatus,@RentalType=RentalType,@DriverId=DriverId,@VehicleId=VehicleId
      FROM dbo.Bookings WITH (UPDLOCK, HOLDLOCK) WHERE BookingId=@BookingId;
      IF @Status <> ''Confirmed'' OR @Status IS NULL
          THROW 51601, ''Booking is not confirmed.'', 1;
      IF @RentalType=''WithDriver'' AND @DriverId IS NULL
          THROW 51602, ''Driver must be assigned before handover.'', 1;
      IF EXISTS (SELECT 1 FROM dbo.VehicleHandovers WHERE BookingId=@BookingId)
          THROW 51603, ''Vehicle was already handed over.'', 1;
      IF @OdometerOutKm < (SELECT OdometerKm FROM dbo.Vehicles WHERE VehicleId=@VehicleId)
          THROW 51604, ''Odometer must not decrease.'', 1;
      INSERT INTO dbo.VehicleHandovers(BookingId,StaffId,OdometerOutKm,FuelPercentOut,ConditionNotes)
      VALUES(@BookingId,@StaffUserId,@OdometerOutKm,@FuelPercentOut,@ConditionNotes);
      UPDATE dbo.Bookings SET BookingStatus=''InUse'',UpdatedAt=SYSUTCDATETIME() WHERE BookingId=@BookingId;
      INSERT INTO dbo.BookingStatusHistory(BookingId,OldStatus,NewStatus,ChangedByUserId,ChangeNote)
      VALUES(@BookingId,''Confirmed'',''InUse'',@StaffUserId,N''Vehicle handover completed'');
      COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
      IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
      THROW;
    END CATCH
END;');
EXEC(N'-- 17. STAFF RETURN: separate vehicle-returned from paid-and-completed.
CREATE OR ALTER PROCEDURE dbo.usp_ReturnVehicle
    @BookingId BIGINT, @StaffUserId BIGINT, @OdometerInKm INT,
    @FuelPercentIn TINYINT,
    @LateFee DECIMAL(18,2)=0, @DamageFee DECIMAL(18,2)=0, @OtherFee DECIMAL(18,2)=0,
    @InspectionNotes NVARCHAR(2000)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Status VARCHAR(16),@VehicleId BIGINT,@OdometerOut INT;
    BEGIN TRY
      BEGIN TRANSACTION;
      IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId=@StaffUserId AND IsActive=1
                     AND UserRole IN (''Staff'',''Admin''))
          THROW 51700, ''Only Staff/Admin may receive a vehicle.'', 1;
      SELECT @Status=BookingStatus,@VehicleId=VehicleId FROM dbo.Bookings WITH (UPDLOCK,HOLDLOCK)
      WHERE BookingId=@BookingId;
      IF @Status<>''InUse'' OR @Status IS NULL THROW 51701, ''Vehicle is not in use.'', 1;
      SELECT @OdometerOut=OdometerOutKm FROM dbo.VehicleHandovers WHERE BookingId=@BookingId;
      IF @OdometerOut IS NULL OR @OdometerInKm<@OdometerOut
          THROW 51702, ''Invalid return odometer reading.'', 1;
      INSERT INTO dbo.VehicleReturns
          (BookingId,StaffId,OdometerInKm,FuelPercentIn,LateFee,DamageFee,OtherFee,InspectionNotes)
      VALUES(@BookingId,@StaffUserId,@OdometerInKm,@FuelPercentIn,@LateFee,@DamageFee,@OtherFee,@InspectionNotes);
      UPDATE dbo.Vehicles SET OdometerKm=@OdometerInKm WHERE VehicleId=@VehicleId;
      UPDATE dbo.Bookings SET BookingStatus=''Returned'',UpdatedAt=SYSUTCDATETIME() WHERE BookingId=@BookingId;
      INSERT INTO dbo.BookingStatusHistory(BookingId,OldStatus,NewStatus,ChangedByUserId,ChangeNote)
      VALUES(@BookingId,''InUse'',''Returned'',@StaffUserId,N''Vehicle returned, final balance pending'');
      COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
      IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
      THROW;
    END CATCH
END;');
EXEC(N'-- 18. FINISH BOOKING after sufficient paid balance + return fees.
CREATE OR ALTER PROCEDURE dbo.usp_CompleteBooking
    @BookingId BIGINT, @StaffUserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @BaseAmount DECIMAL(18,2),@Extra DECIMAL(18,2),@Paid DECIMAL(18,2);
    BEGIN TRY
      BEGIN TRANSACTION;
      IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId=@StaffUserId AND IsActive=1
                     AND UserRole IN (''Staff'',''Admin''))
          THROW 51800, ''Only Staff/Admin may complete booking.'', 1;
      SELECT @BaseAmount=TotalAmount FROM dbo.Bookings WITH (UPDLOCK,HOLDLOCK)
      WHERE BookingId=@BookingId AND BookingStatus=''Returned'';
      IF @BaseAmount IS NULL THROW 51801, ''Booking is not returned.'', 1;
      SELECT @Extra=LateFee+DamageFee+OtherFee FROM dbo.VehicleReturns WHERE BookingId=@BookingId;
      IF @Extra IS NULL THROW 51803, ''Return inspection is required before completion.'', 1;
      SELECT @Paid=COALESCE(SUM(CASE WHEN PaymentType=''Refund'' THEN -Amount ELSE Amount END),0)
      FROM dbo.Payments WHERE BookingId=@BookingId AND PaymentStatus=''Success'';
      IF @Paid < @BaseAmount+@Extra THROW 51802, ''Remaining balance has not been paid.'', 1;
      UPDATE dbo.Bookings SET BookingStatus=''Completed'',UpdatedAt=SYSUTCDATETIME()
      WHERE BookingId=@BookingId;
      INSERT INTO dbo.BookingStatusHistory(BookingId,OldStatus,NewStatus,ChangedByUserId,ChangeNote)
      VALUES(@BookingId,''Returned'',''Completed'',@StaffUserId,N''All payments reconciled'');
      COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
      IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
      THROW;
    END CATCH
END;');
EXEC(N'-- ================================================================

IF NOT EXISTS (SELECT 1 FROM dbo.RentalPolicies WHERE PolicyId = 1)
BEGIN
    INSERT INTO dbo.RentalPolicies
        (PolicyId, DepositPercent, DriverDailyFee, OnlineHoldMinutes, CashHoldHours)
    VALUES (1, 50, 350000, 30, 24);
END;');
EXEC(N'IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N''dbo.VehicleVoucherEligibility'') AND name=N''IX_Search_VoucherScope'') CREATE INDEX IX_Search_VoucherScope ON dbo.VehicleVoucherEligibility(VoucherId,VehicleId,RentalMode) INCLUDE(IsActive);');
EXEC(N'CREATE OR ALTER TRIGGER dbo.tr_Search_CalendarBookingGuard
ON dbo.VehicleCalendarBlocks
AFTER INSERT, UPDATE
AS
BEGIN
 SET NOCOUNT ON;
 SET XACT_ABORT ON;
 IF NOT EXISTS(SELECT 1 FROM inserted) RETURN;
 -- Same transaction-owned vehicle lock as usp_CreateBooking. Sorted for bulk updates.
 DECLARE @vehicle bigint,@resource nvarchar(255),@result int;
 DECLARE affected CURSOR LOCAL FAST_FORWARD FOR
 SELECT VehicleId FROM inserted UNION SELECT VehicleId FROM deleted ORDER BY VehicleId;
 OPEN affected;
 FETCH NEXT FROM affected INTO @vehicle;
 WHILE @@FETCH_STATUS=0
 BEGIN
  SET @resource=CONCAT(N''CRMS_VEHICLE_'',@vehicle);
  EXEC @result=sys.sp_getapplock @Resource=@resource,@LockMode=''Exclusive'',@LockOwner=''Transaction'',@LockTimeout=10000;
  IF @result<0 THROW 53600,''Could not acquire calendar vehicle lock. Retry the operation.'',1;
  FETCH NEXT FROM affected INTO @vehicle;
 END;
 CLOSE affected;
 DEALLOCATE affected;
 IF EXISTS(
  SELECT 1 FROM inserted block
  JOIN dbo.Bookings booking WITH(UPDLOCK,HOLDLOCK) ON booking.VehicleId=block.VehicleId
  WHERE block.IsActive=1 AND (
   (booking.BookingStatus=''InUse'' AND booking.StartAt<block.EndAt)
   OR (booking.BookingStatus=''Confirmed'' AND booking.StartAt<block.EndAt AND booking.EndAt>block.StartAt)
   OR (booking.BookingStatus=''PendingDeposit'' AND booking.DepositDeadline>SYSUTCDATETIME()
       AND booking.StartAt<block.EndAt AND booking.EndAt>block.StartAt)))
  THROW 53601,''Calendar block conflicts with an existing active reservation or vehicle in use.'',1;
 IF EXISTS(SELECT 1 FROM inserted block JOIN dbo.MaintenanceRecords m WITH(UPDLOCK,HOLDLOCK)
   ON m.VehicleId=block.VehicleId AND m.StartAt<block.EndAt AND m.EndAt>block.StartAt
   WHERE block.IsActive=1 AND m.MaintenanceStatus IN(''Scheduled'',''InProgress''))
  THROW 53602,''Calendar block conflicts with scheduled or in-progress maintenance.'',1;
END;');
IF @IncludeDemoData=1
BEGIN
EXEC(N'-- OPTIONAL DEVELOPMENT DATA. Run after CRMS.sql in the selected test database.
-- 03. DU LIEU MAU (SEED DATA) - CHI THEM BAN GHI CON THIEU
-- Khong DROP / DELETE / TRUNCATE / UPDATE du lieu cu.
-- Demo records are fictional. Vehicle photos use the user-supplied bundled assets.
-- All times stored as UTC, rental prices in VND.
-- ================================================================

-- Rental policy - 50 percent deposit and driver fee per day

-- Account demo (ASP.NET Core PasswordHasher V3 compatible hashes).
-- Only for LOCAL DEVELOPMENT. Change passwords before deployment.
-- Admin: Admin@12345! | Staff: Staff@12345! | Customer: Customer@12345!

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N''admin.demo@crms.example.com'')
    INSERT INTO dbo.Users(FullName, Email, PasswordHash, UserRole, IsActive)
    VALUES (N''Admin Demo'', N''admin.demo@crms.example.com'', N''AQAAAAIAAYagAAAAENAttIFARiGhxy2y2QPPLcub7ePM7kSfWpdDnZI8jHyAlaGxgS39ARbeltG8osQtKw=='', ''Admin'', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N''staff01@crms.example.com'')
    INSERT INTO dbo.Users(FullName, Email, PasswordHash, UserRole, IsActive)
    VALUES (N''Staff Demo 01'', N''staff01@crms.example.com'', N''AQAAAAIAAYagAAAAEEUoALyZBYcdvnmRhl6a3vGiFM80QugI7nCUvKbgdVfhS2jPWimkkVBv8y8AC8lG0g=='', ''Staff'', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N''staff02@crms.example.com'')
    INSERT INTO dbo.Users(FullName, Email, PasswordHash, UserRole, IsActive)
    VALUES (N''Staff Demo 02'', N''staff02@crms.example.com'', N''AQAAAAIAAYagAAAAEL9B5wJqoHa7bRRYZuues7yxGMZmL/mKQzD9Y+W6t6cOJTZc7kZ19wsKOWseWPI0wQ=='', ''Staff'', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N''customer01@crms.example.com'')
    INSERT INTO dbo.Users(FullName, Email, PasswordHash, UserRole, IsActive)
    VALUES (N''Customer Demo 01'', N''customer01@crms.example.com'', N''AQAAAAIAAYagAAAAENgGBqW8pvRHTMQtOgMewDJuzV75VdQBazNg28ASyCQnlPMK0nv4dY72RPnieEUihA=='', ''Customer'', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N''customer02@crms.example.com'')
    INSERT INTO dbo.Users(FullName, Email, PasswordHash, UserRole, IsActive)
    VALUES (N''Customer Demo 02'', N''customer02@crms.example.com'', N''AQAAAAIAAYagAAAAEIb7S8utzVL+warzYfTHbKV7UTYGA18XYeM7SG8W5tfsgNomb0AvdKRANiKhojnF6g=='', ''Customer'', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N''customer03@crms.example.com'')
    INSERT INTO dbo.Users(FullName, Email, PasswordHash, UserRole, IsActive)
    VALUES (N''Customer Demo 03'', N''customer03@crms.example.com'', N''AQAAAAIAAYagAAAAELzzZ7ATi1XgV/rOo1SDMp59hG0W2eZWLSya9GLn3RRSxq3w0ezjyhr2nSEEG5pMpw=='', ''Customer'', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N''customer04@crms.example.com'')
    INSERT INTO dbo.Users(FullName, Email, PasswordHash, UserRole, IsActive)
    VALUES (N''Customer Demo 04'', N''customer04@crms.example.com'', N''AQAAAAIAAYagAAAAEICjxbOyX7Ydy6r1xrQcQcRblulb0hQ6oD4T1Lh7Cy7OhZD0cuCWBGsZ6sCznXZLpw=='', ''Customer'', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N''customer05@crms.example.com'')
    INSERT INTO dbo.Users(FullName, Email, PasswordHash, UserRole, IsActive)
    VALUES (N''Customer Demo 05'', N''customer05@crms.example.com'', N''AQAAAAIAAYagAAAAEHv9NlbxdtagizlN02hzgmLnPtxCPb+iFJ8CA4730xv2Kl3RMCZahb6DZJwwqdxk6Q=='', ''Customer'', 1);');
EXEC(N'-- Seven vehicle categories
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleCategories WHERE CategoryName = N''Sedan'')
    INSERT INTO dbo.VehicleCategories(CategoryName, Description) VALUES (N''Sedan'', N''Xe sedan 4-5 cho, phu hop di pho va cong tac'');
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleCategories WHERE CategoryName = N''SUV'')
    INSERT INTO dbo.VehicleCategories(CategoryName, Description) VALUES (N''SUV'', N''Xe gam cao cho gia dinh va du lich'');
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleCategories WHERE CategoryName = N''MPV'')
    INSERT INTO dbo.VehicleCategories(CategoryName, Description) VALUES (N''MPV'', N''Xe da dung 7-8 cho'');
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleCategories WHERE CategoryName = N''Hatchback'')
    INSERT INTO dbo.VehicleCategories(CategoryName, Description) VALUES (N''Hatchback'', N''Xe nho gon di chuyen trong do thi'');
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleCategories WHERE CategoryName = N''Pickup'')
    INSERT INTO dbo.VehicleCategories(CategoryName, Description) VALUES (N''Pickup'', N''Xe ban tai phu hop duong xa'');
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleCategories WHERE CategoryName = N''Luxury'')
    INSERT INTO dbo.VehicleCategories(CategoryName, Description) VALUES (N''Luxury'', N''Xe hang sang cho su kien va cong tac'');
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleCategories WHERE CategoryName = N''Electric'')
    INSERT INTO dbo.VehicleCategories(CategoryName, Description) VALUES (N''Electric'', N''Xe dien tiet kiem nhien lieu'');');
EXEC(N'-- Three vehicles retained from the first 02_Demo_Data.sql sample
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate=''65A-12345'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, Description)
    SELECT CategoryId, ''65A-12345'', N''Toyota'', N''Vios'', 2023, 5, ''Automatic'', ''Gasoline'', 700000, N''Xe demo tu bo du lieu goc''
    FROM dbo.VehicleCategories WHERE CategoryName = N''Sedan'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate=''65A-67890'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, Description)
    SELECT CategoryId, ''65A-67890'', N''Ford'', N''Everest'', 2024, 7, ''Automatic'', ''Diesel'', 1500000, N''Xe demo tu bo du lieu goc''
    FROM dbo.VehicleCategories WHERE CategoryName = N''SUV'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate=''65A-24680'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, Description)
    SELECT CategoryId, ''65A-24680'', N''Toyota'', N''Innova'', 2022, 7, ''Manual'', ''Gasoline'', 1100000, N''Xe demo tu bo du lieu goc''
    FROM dbo.VehicleCategories WHERE CategoryName = N''MPV'';');
EXEC(N'-- 26 additional fictional vehicles, including one Suspended for status filtering
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-001'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-001'', N''Toyota'', N''Vios'', 2023, 5, ''Automatic'', ''Gasoline'', 700000, 18000, ''Active'', N''Xe demo Toyota Vios; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''Sedan'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-002'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-002'', N''Honda'', N''City'', 2024, 5, ''Automatic'', ''Gasoline'', 820000, 11000, ''Active'', N''Xe demo Honda City; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''Sedan'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-003'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-003'', N''Hyundai'', N''Accent'', 2022, 5, ''Manual'', ''Gasoline'', 620000, 34000, ''Active'', N''Xe demo Hyundai Accent; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''Sedan'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-004'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-004'', N''Mazda'', N''Mazda 3'', 2023, 5, ''Automatic'', ''Gasoline'', 950000, 21000, ''Active'', N''Xe demo Mazda Mazda 3; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''Sedan'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-005'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-005'', N''Kia'', N''K3'', 2024, 5, ''Automatic'', ''Gasoline'', 850000, 9000, ''Active'', N''Xe demo Kia K3; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''Sedan'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-006'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-006'', N''Mazda'', N''CX-5'', 2023, 5, ''Automatic'', ''Gasoline'', 1200000, 25000, ''Active'', N''Xe demo Mazda CX-5; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''SUV'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-007'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-007'', N''Ford'', N''Everest'', 2024, 7, ''Automatic'', ''Diesel'', 1550000, 14000, ''Active'', N''Xe demo Ford Everest; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''SUV'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-008'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-008'', N''Hyundai'', N''Santa Fe'', 2023, 7, ''Automatic'', ''Diesel'', 1450000, 23000, ''Active'', N''Xe demo Hyundai Santa Fe; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''SUV'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-009'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-009'', N''Toyota'', N''Fortuner'', 2022, 7, ''Automatic'', ''Diesel'', 1400000, 41000, ''Active'', N''Xe demo Toyota Fortuner; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''SUV'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-010'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-010'', N''Kia'', N''Seltos'', 2024, 5, ''Automatic'', ''Gasoline'', 980000, 15000, ''Active'', N''Xe demo Kia Seltos; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''SUV'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-011'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-011'', N''Mitsubishi'', N''Xpander'', 2023, 7, ''Automatic'', ''Gasoline'', 1000000, 28000, ''Active'', N''Xe demo Mitsubishi Xpander; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''MPV'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-012'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-012'', N''Toyota'', N''Innova'', 2022, 7, ''Manual'', ''Gasoline'', 1100000, 39000, ''Active'', N''Xe demo Toyota Innova; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''MPV'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-013'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-013'', N''Toyota'', N''Veloz Cross'', 2024, 7, ''Automatic'', ''Gasoline'', 1120000, 9000, ''Active'', N''Xe demo Toyota Veloz Cross; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''MPV'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-014'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-014'', N''Kia'', N''Carnival'', 2024, 7, ''Automatic'', ''Diesel'', 1900000, 12000, ''Active'', N''Xe demo Kia Carnival; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''MPV'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-015'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-015'', N''Hyundai'', N''Grand i10'', 2022, 5, ''Manual'', ''Gasoline'', 520000, 33000, ''Active'', N''Xe demo Hyundai Grand i10; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''Hatchback'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-016'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-016'', N''Toyota'', N''Wigo'', 2023, 5, ''Automatic'', ''Gasoline'', 580000, 18000, ''Active'', N''Xe demo Toyota Wigo; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''Hatchback'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-017'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-017'', N''Suzuki'', N''Swift'', 2024, 5, ''Automatic'', ''Gasoline'', 680000, 7000, ''Active'', N''Xe demo Suzuki Swift; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''Hatchback'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-018'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-018'', N''Ford'', N''Ranger'', 2023, 5, ''Automatic'', ''Diesel'', 1300000, 35000, ''Active'', N''Xe demo Ford Ranger; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''Pickup'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-019'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-019'', N''Mitsubishi'', N''Triton'', 2022, 5, ''Automatic'', ''Diesel'', 1180000, 42000, ''Active'', N''Xe demo Mitsubishi Triton; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''Pickup'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-020'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-020'', N''Mercedes-Benz'', N''C 200'', 2024, 5, ''Automatic'', ''Gasoline'', 2400000, 8000, ''Active'', N''Xe demo Mercedes-Benz C 200; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''Luxury'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-021'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-021'', N''BMW'', N''320i'', 2023, 5, ''Automatic'', ''Gasoline'', 2300000, 15000, ''Active'', N''Xe demo BMW 320i; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''Luxury'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-022'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-022'', N''Lexus'', N''ES 250'', 2024, 5, ''Automatic'', ''Gasoline'', 3200000, 6000, ''Active'', N''Xe demo Lexus ES 250; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''Luxury'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-023'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-023'', N''VinFast'', N''VF 5'', 2024, 5, ''Automatic'', ''Electric'', 800000, 12000, ''Active'', N''Xe demo VinFast VF 5; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''Electric'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-024'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-024'', N''VinFast'', N''VF 8'', 2025, 5, ''Automatic'', ''Electric'', 1350000, 6500, ''Active'', N''Xe demo VinFast VF 8; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''Electric'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-025'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-025'', N''VinFast'', N''VF 9'', 2025, 7, ''Automatic'', ''Electric'', 2000000, 4200, ''Active'', N''Xe demo VinFast VF 9; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''Electric'';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = ''DEMO-CRMS-026'')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, ''DEMO-CRMS-026'', N''Toyota'', N''Camry Hybrid'', 2024, 5, ''Automatic'', ''Hybrid'', 1750000, 11500, ''Suspended'', N''Xe demo Toyota Camry Hybrid; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.''
    FROM dbo.VehicleCategories WHERE CategoryName = N''Sedan'';');
EXEC(N'INSERT INTO dbo.VehicleLongTermRates (VehicleId, DurationMonths, BaseMonthlyRate, DiscountPercent)
SELECT v.VehicleId, p.DurationMonths, CAST(v.DailyRate * 30 AS DECIMAL(18,2)), p.DiscountPercent
FROM dbo.Vehicles v
CROSS JOIN (VALUES (1,0.00),(3,2.00),(6,3.00),(9,4.00),(12,5.00)) p(DurationMonths, DiscountPercent)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.VehicleLongTermRates r
    WHERE r.VehicleId = v.VehicleId AND r.DurationMonths = p.DurationMonths
);');
-- Vehicle photos are assigned by the final bundled-photo section.
EXEC(N'-- Eight fictional drivers
IF NOT EXISTS (SELECT 1 FROM dbo.Drivers WHERE LicenseNumber=N''DEMO-DL-001'')
    INSERT INTO dbo.Drivers(FullName, Phone, LicenseNumber, IsActive, Notes)
    VALUES (N''Nguyen Van An'', ''0900000101'', N''DEMO-DL-001'', 1, N''Thong tin minh hoa, khong phai bang lai that'');
IF NOT EXISTS (SELECT 1 FROM dbo.Drivers WHERE LicenseNumber=N''DEMO-DL-002'')
    INSERT INTO dbo.Drivers(FullName, Phone, LicenseNumber, IsActive, Notes)
    VALUES (N''Tran Minh Binh'', ''0900000102'', N''DEMO-DL-002'', 1, N''Thong tin minh hoa, khong phai bang lai that'');
IF NOT EXISTS (SELECT 1 FROM dbo.Drivers WHERE LicenseNumber=N''DEMO-DL-003'')
    INSERT INTO dbo.Drivers(FullName, Phone, LicenseNumber, IsActive, Notes)
    VALUES (N''Le Hoang Phuc'', ''0900000103'', N''DEMO-DL-003'', 1, N''Thong tin minh hoa, khong phai bang lai that'');
IF NOT EXISTS (SELECT 1 FROM dbo.Drivers WHERE LicenseNumber=N''DEMO-DL-004'')
    INSERT INTO dbo.Drivers(FullName, Phone, LicenseNumber, IsActive, Notes)
    VALUES (N''Pham Quoc Khang'', ''0900000104'', N''DEMO-DL-004'', 1, N''Thong tin minh hoa, khong phai bang lai that'');
IF NOT EXISTS (SELECT 1 FROM dbo.Drivers WHERE LicenseNumber=N''DEMO-DL-005'')
    INSERT INTO dbo.Drivers(FullName, Phone, LicenseNumber, IsActive, Notes)
    VALUES (N''Vo Thanh Long'', ''0900000105'', N''DEMO-DL-005'', 1, N''Thong tin minh hoa, khong phai bang lai that'');
IF NOT EXISTS (SELECT 1 FROM dbo.Drivers WHERE LicenseNumber=N''DEMO-DL-006'')
    INSERT INTO dbo.Drivers(FullName, Phone, LicenseNumber, IsActive, Notes)
    VALUES (N''Bui Anh Tuan'', ''0900000106'', N''DEMO-DL-006'', 1, N''Thong tin minh hoa, khong phai bang lai that'');
IF NOT EXISTS (SELECT 1 FROM dbo.Drivers WHERE LicenseNumber=N''DEMO-DL-007'')
    INSERT INTO dbo.Drivers(FullName, Phone, LicenseNumber, IsActive, Notes)
    VALUES (N''Do Minh Quan'', ''0900000107'', N''DEMO-DL-007'', 1, N''Thong tin minh hoa, khong phai bang lai that'');
IF NOT EXISTS (SELECT 1 FROM dbo.Drivers WHERE LicenseNumber=N''DEMO-DL-008'')
    INSERT INTO dbo.Drivers(FullName, Phone, LicenseNumber, IsActive, Notes)
    VALUES (N''Hoang Bao Nam'', ''0900000108'', N''DEMO-DL-008'', 0, N''Thong tin minh hoa, khong phai bang lai that'');');
EXEC(N'-- Eight vouchers: percent, fixed, inactive, expired, not-yet-active
IF NOT EXISTS (SELECT 1 FROM dbo.Vouchers WHERE VoucherCode=N''WELCOME10'')
    INSERT INTO dbo.Vouchers(VoucherCode, DiscountType, DiscountValue, MaxDiscountAmount, MinimumOrderAmount, StartsAt, EndsAt, UsageLimit, PerCustomerLimit, IsActive)
    VALUES (N''WELCOME10'', ''Percent'', 10, 150000, 500000, DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, 365, SYSUTCDATETIME()), 100, 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Vouchers WHERE VoucherCode=N''TRIP15'')
    INSERT INTO dbo.Vouchers(VoucherCode, DiscountType, DiscountValue, MaxDiscountAmount, MinimumOrderAmount, StartsAt, EndsAt, UsageLimit, PerCustomerLimit, IsActive)
    VALUES (N''TRIP15'', ''Percent'', 15, 350000, 1500000, DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, 365, SYSUTCDATETIME()), 100, 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Vouchers WHERE VoucherCode=N''WEEKEND20'')
    INSERT INTO dbo.Vouchers(VoucherCode, DiscountType, DiscountValue, MaxDiscountAmount, MinimumOrderAmount, StartsAt, EndsAt, UsageLimit, PerCustomerLimit, IsActive)
    VALUES (N''WEEKEND20'', ''Percent'', 20, 250000, 2000000, DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, 180, SYSUTCDATETIME()), 60, 2, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Vouchers WHERE VoucherCode=N''SAVE50K'')
    INSERT INTO dbo.Vouchers(VoucherCode, DiscountType, DiscountValue, MaxDiscountAmount, MinimumOrderAmount, StartsAt, EndsAt, UsageLimit, PerCustomerLimit, IsActive)
    VALUES (N''SAVE50K'', ''Fixed'', 50000, NULL, 700000, DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, 365, SYSUTCDATETIME()), 150, 2, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Vouchers WHERE VoucherCode=N''SAVE100K'')
    INSERT INTO dbo.Vouchers(VoucherCode, DiscountType, DiscountValue, MaxDiscountAmount, MinimumOrderAmount, StartsAt, EndsAt, UsageLimit, PerCustomerLimit, IsActive)
    VALUES (N''SAVE100K'', ''Fixed'', 100000, NULL, 1500000, DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, 365, SYSUTCDATETIME()), 100, 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Vouchers WHERE VoucherCode=N''FAMILY200'')
    INSERT INTO dbo.Vouchers(VoucherCode, DiscountType, DiscountValue, MaxDiscountAmount, MinimumOrderAmount, StartsAt, EndsAt, UsageLimit, PerCustomerLimit, IsActive)
    VALUES (N''FAMILY200'', ''Fixed'', 200000, NULL, 3000000, DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, 365, SYSUTCDATETIME()), 40, 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Vouchers WHERE VoucherCode=N''OLDPROMO'')
    INSERT INTO dbo.Vouchers(VoucherCode, DiscountType, DiscountValue, MaxDiscountAmount, MinimumOrderAmount, StartsAt, EndsAt, UsageLimit, PerCustomerLimit, IsActive)
    VALUES (N''OLDPROMO'', ''Percent'', 5, 80000, 300000, DATEADD(DAY, -120, SYSUTCDATETIME()), DATEADD(DAY, -30, SYSUTCDATETIME()), 30, 1, 0);
IF NOT EXISTS (SELECT 1 FROM dbo.Vouchers WHERE VoucherCode=N''NEXTMONTH'')
    INSERT INTO dbo.Vouchers(VoucherCode, DiscountType, DiscountValue, MaxDiscountAmount, MinimumOrderAmount, StartsAt, EndsAt, UsageLimit, PerCustomerLimit, IsActive)
    VALUES (N''NEXTMONTH'', ''Percent'', 12, 250000, 1000000, DATEADD(DAY, 30, SYSUTCDATETIME()), DATEADD(DAY, 120, SYSUTCDATETIME()), 80, 1, 1);');
EXEC(N'-- Four maintenance records; requires an Admin demo user.
-- No active rental records are seeded. History remains testable from application.
DECLARE @DemoAdminId BIGINT =
    (SELECT TOP (1) UserId FROM dbo.Users WHERE Email=N''admin.demo@crms.example.com'' AND UserRole=''Admin'');
IF @DemoAdminId IS NOT NULL
BEGIN

    IF NOT EXISTS (SELECT 1 FROM dbo.MaintenanceRecords m JOIN dbo.Vehicles v ON m.VehicleId = v.VehicleId WHERE v.LicensePlate=''DEMO-CRMS-019'' AND m.Description=N''Bao duong dinh ky, thay dau va loc'')
        INSERT INTO dbo.MaintenanceRecords(VehicleId, CreatedByUserId, StartAt, EndAt, MaintenanceStatus, Description, Cost)
        SELECT VehicleId, @DemoAdminId, DATEADD(DAY, -70, SYSUTCDATETIME()), DATEADD(DAY, -69, SYSUTCDATETIME()), ''Completed'', N''Bao duong dinh ky, thay dau va loc'', 850000
        FROM dbo.Vehicles WHERE LicensePlate=''DEMO-CRMS-019'';
    IF NOT EXISTS (SELECT 1 FROM dbo.MaintenanceRecords m JOIN dbo.Vehicles v ON m.VehicleId = v.VehicleId WHERE v.LicensePlate=''DEMO-CRMS-020'' AND m.Description=N''Kiem tra he thong dieu hoa'')
        INSERT INTO dbo.MaintenanceRecords(VehicleId, CreatedByUserId, StartAt, EndAt, MaintenanceStatus, Description, Cost)
        SELECT VehicleId, @DemoAdminId, DATEADD(DAY, -20, SYSUTCDATETIME()), DATEADD(DAY, -19, SYSUTCDATETIME()), ''Completed'', N''Kiem tra he thong dieu hoa'', 650000
        FROM dbo.Vehicles WHERE LicensePlate=''DEMO-CRMS-020'';
    IF NOT EXISTS (SELECT 1 FROM dbo.MaintenanceRecords m JOIN dbo.Vehicles v ON m.VehicleId = v.VehicleId WHERE v.LicensePlate=''DEMO-CRMS-025'' AND m.Description=N''Kiem tra phanh va lop'')
        INSERT INTO dbo.MaintenanceRecords(VehicleId, CreatedByUserId, StartAt, EndAt, MaintenanceStatus, Description, Cost)
        SELECT VehicleId, @DemoAdminId, DATEADD(DAY, 45, SYSUTCDATETIME()), DATEADD(DAY, 46, SYSUTCDATETIME()), ''Scheduled'', N''Kiem tra phanh va lop'', 0
        FROM dbo.Vehicles WHERE LicensePlate=''DEMO-CRMS-025'';
    IF NOT EXISTS (SELECT 1 FROM dbo.MaintenanceRecords m JOIN dbo.Vehicles v ON m.VehicleId = v.VehicleId WHERE v.LicensePlate=''DEMO-CRMS-026'' AND m.Description=N''Kiem tra pin va he thong dien'')
        INSERT INTO dbo.MaintenanceRecords(VehicleId, CreatedByUserId, StartAt, EndAt, MaintenanceStatus, Description, Cost)
        SELECT VehicleId, @DemoAdminId, DATEADD(DAY, 55, SYSUTCDATETIME()), DATEADD(DAY, 56, SYSUTCDATETIME()), ''Scheduled'', N''Kiem tra pin va he thong dien'', 0
        FROM dbo.Vehicles WHERE LicensePlate=''DEMO-CRMS-026'';
END;');
EXEC(N'-- BOOKINGS / PAYMENTS / HANDOVERS / RETURNS / REVIEWS / AUDIT:
-- intentionally empty. Create these with the actual project workflow so
-- deposits, payment statuses, refunds and inventory checks stay coherent.
PRINT N''CRMS sample data inserted where missing; existing rows were preserved.'';');
EXEC(N'-- OPTIONAL SYNTHETIC TRANSACTIONS. Run after CRMS.Demo.sql only in a demo database.
-- SEARCH METADATA ON ORIGINAL 29 VEHICLES
/* Synthetic SEARCH metadata for the ORIGINAL 29 sample vehicles only.
   Vehicle facts/images/monthly rates/categories/vouchers are not changed here.
   Six histories support owner ratings; one confirmed booking and one owner block
   support availability tests. Development/sample data, not real transactions. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF EXISTS(SELECT 1 FROM dbo.Vehicles WHERE LicensePlate LIKE ''DEMO-FILTER-%'')
 THROW 52603,''Previous separate 10-car demo detected. Use a fresh test database; existing vehicles are not deleted.'',1;
IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE (class=0 OR (class=1 AND major_id=OBJECT_ID(N''dbo.SearchSettings'') AND minor_id=0)) AND name=N''CRMS.SearchOriginal29SeedVersion'')
BEGIN PRINT N''Search metadata for the original 29 cars is already installed; changes preserved.''; RETURN; END;
BEGIN TRY
BEGIN TRANSACTION;
DECLARE @staff bigint=(SELECT UserId FROM dbo.Users WHERE Email=N''admin.demo@crms.example.com'' AND UserRole=''Admin'' AND IsActive=1),
 @customer bigint=(SELECT UserId FROM dbo.Users WHERE Email=N''customer01@crms.example.com'' AND UserRole=''Customer'' AND IsActive=1);
IF @staff IS NULL OR @customer IS NULL THROW 52602,''Original demo Admin and Customer must exist and be active.'',1;
DECLARE @fleet TABLE (
 SampleNo int PRIMARY KEY,LicensePlate varchar(20) NOT NULL UNIQUE,CityKey varchar(40) NOT NULL,OwnerKey varchar(10) NOT NULL,
 SupportsDriver bit NOT NULL,SupportsLongTerm bit NOT NULL,Instant bit NOT NULL,NoCollateral bit NOT NULL,
 UnlimitedKm bit NOT NULL,Intercity bit NOT NULL,ChildSeat bit NOT NULL,Sport bit NOT NULL,
 HourlyRate decimal(18,2) NULL,Delivery bit NOT NULL,FreeKm decimal(7,2) NOT NULL
);
INSERT @fleet VALUES
(1,''65A-12345'',''DEMO-CT'',''AN'',1,1,1,1,1,1,1,0,120000,1,3),
(2,''65A-67890'',''DEMO-CT'',''BINH'',1,1,0,0,0,1,1,1,NULL,1,0),
(3,''65A-24680'',''DEMO-CT'',''CHI'',1,1,0,0,0,1,1,0,NULL,1,0),
(4,''DEMO-CRMS-001'',''DEMO-CT'',''AN'',1,1,1,1,1,1,1,0,120000,1,3),
(5,''DEMO-CRMS-002'',''DEMO-CT'',''AN'',1,1,1,0,0,1,1,0,150000,1,0),
(6,''DEMO-CRMS-003'',''DEMO-DN'',''CHI'',0,1,0,1,0,0,0,0,100000,0,0),
(7,''DEMO-CRMS-004'',''DEMO-CT'',''AN'',1,1,0,0,0,1,0,0,NULL,1,0),
(8,''DEMO-CRMS-005'',''DEMO-CT'',''CHI'',1,1,1,1,0,1,1,0,NULL,1,0),
(9,''DEMO-CRMS-006'',''DEMO-HCM'',''BINH'',1,1,0,0,0,1,1,1,NULL,1,3),
(10,''DEMO-CRMS-007'',''DEMO-HCM'',''BINH'',1,1,1,0,0,1,1,1,NULL,1,0),
(11,''DEMO-CRMS-008'',''DEMO-HCM'',''BINH'',1,1,0,1,1,1,1,0,NULL,1,3),
(12,''DEMO-CRMS-009'',''DEMO-CT'',''BINH'',1,0,0,0,0,1,0,0,NULL,1,0),
(13,''DEMO-CRMS-010'',''DEMO-HCM'',''CHI'',0,1,1,1,0,1,1,0,NULL,1,3),
(14,''DEMO-CRMS-011'',''DEMO-CT'',''CHI'',1,1,0,0,0,1,1,0,NULL,1,0),
(15,''DEMO-CRMS-012'',''DEMO-DN'',''CHI'',1,1,0,0,0,1,1,0,NULL,1,0),
(16,''DEMO-CRMS-013'',''DEMO-HCM'',''CHI'',1,1,1,0,0,1,1,0,NULL,1,0),
(17,''DEMO-CRMS-014'',''DEMO-HCM'',''BINH'',1,1,1,1,1,1,1,0,NULL,1,3),
(18,''DEMO-CRMS-015'',''DEMO-DN'',''CHI'',0,1,0,1,0,0,0,0,90000,0,0),
(19,''DEMO-CRMS-016'',''DEMO-CT'',''AN'',0,1,1,1,0,0,0,0,100000,0,0),
(20,''DEMO-CRMS-017'',''DEMO-CT'',''AN'',0,1,0,1,0,0,0,0,110000,0,0),
(21,''DEMO-CRMS-018'',''DEMO-DN'',''BINH'',1,1,0,0,0,1,0,1,NULL,1,0),
(22,''DEMO-CRMS-019'',''DEMO-DN'',''BINH'',1,0,0,0,0,1,0,0,NULL,1,0),
(23,''DEMO-CRMS-020'',''DEMO-HN'',''BINH'',1,1,1,0,0,1,0,1,NULL,1,0),
(24,''DEMO-CRMS-021'',''DEMO-HN'',''BINH'',1,1,0,0,0,1,0,1,NULL,1,0),
(25,''DEMO-CRMS-022'',''DEMO-HN'',''BINH'',1,1,1,0,1,1,0,0,NULL,1,3),
(26,''DEMO-CRMS-023'',''DEMO-HN'',''CHI'',0,1,1,1,0,0,0,0,NULL,0,0),
(27,''DEMO-CRMS-024'',''DEMO-CT'',''AN'',1,1,1,1,0,1,0,1,200000,1,3),
(28,''DEMO-CRMS-025'',''DEMO-HN'',''BINH'',1,1,0,0,0,1,1,0,NULL,1,0),
(29,''DEMO-CRMS-026'',''DEMO-HN'',''BINH'',1,1,1,0,0,1,0,0,NULL,1,0);
IF (SELECT COUNT(*) FROM @fleet f JOIN dbo.Vehicles v ON v.LicensePlate=f.LicensePlate)<>29
 THROW 52604,''All 29 original sample vehicles must exist first.'',1;
IF EXISTS(SELECT 1 FROM @fleet f JOIN dbo.Vehicles v ON v.LicensePlate=f.LicensePlate JOIN dbo.VehicleSearchProfiles p ON p.VehicleId=v.VehicleId)
 THROW 52605,''Original sample vehicles already have search profiles; review rather than overwrite them.'',1;
IF EXISTS(SELECT 1 FROM dbo.Cities WHERE CityKey IN(''DEMO-CT'',''DEMO-HCM'',''DEMO-DN'',''DEMO-HN''))
 THROW 52606,''Search cities already exist without the seed marker; review first.'',1;
INSERT dbo.Cities(CityKey,CityName,SourceReference) VALUES
 (''DEMO-CT'',N''Cần Thơ'',N''Original 29 CRMS samples: synthetic search metadata''),
 (''DEMO-HCM'',N''TP. Hồ Chí Minh'',N''Original 29 CRMS samples: synthetic search metadata''),
 (''DEMO-DN'',N''Đà Nẵng'',N''Original 29 CRMS samples: synthetic search metadata''),
 (''DEMO-HN'',N''Hà Nội'',N''Original 29 CRMS samples: synthetic search metadata'');
DECLARE @locations TABLE(CityKey varchar(40),Title nvarchar(160),Lat decimal(9,6),Lon decimal(9,6));
INSERT @locations VALUES
 (''DEMO-CT'',N''Điểm mẫu trung tâm Cần Thơ'',10.0452,105.7469),(''DEMO-HCM'',N''Điểm mẫu trung tâm TP. Hồ Chí Minh'',10.7769,106.7009),
 (''DEMO-DN'',N''Điểm mẫu trung tâm Đà Nẵng'',16.0544,108.2022),(''DEMO-HN'',N''Điểm mẫu trung tâm Hà Nội'',21.0278,105.8342);
INSERT dbo.RentalLocations(CityId,DisplayName,AddressLine,Latitude,Longitude)
SELECT c.CityId,l.Title,N''Địa chỉ minh họa dùng để thử search, không phải điểm kinh doanh thật'',l.Lat,l.Lon FROM @locations l JOIN dbo.Cities c ON c.CityKey=l.CityKey;
INSERT dbo.RentalLocations(CityId,DisplayName,AddressLine,Latitude,Longitude)
SELECT CityId,N''Điểm mẫu ngoài bán kính miễn phí Cần Thơ'',N''Điểm thử phí giao nhận'',10.1052,105.7469 FROM dbo.Cities WHERE CityKey=''DEMO-CT'';
DECLARE @owners TABLE(OwnerKey varchar(10) PRIMARY KEY,Title nvarchar(160));
INSERT @owners VALUES(''AN'',N''Chủ xe An · MẪU SEARCH''),(''BINH'',N''Chủ xe Bình · MẪU SEARCH''),(''CHI'',N''Chủ xe Chi · MẪU SEARCH'');
INSERT dbo.VehicleOwners(OwnerType,DisplayName,IsVerified,VerifiedAt)
SELECT CASE WHEN OwnerKey=''CHI'' THEN ''Individual'' ELSE ''Company'' END,Title,1,SYSUTCDATETIME() FROM @owners;
INSERT dbo.VehicleSearchProfiles(VehicleId,OwnerId,LocationId,IsPublished,VerifiedAt)
SELECT v.VehicleId,o.OwnerId,l.LocationId,1,SYSUTCDATETIME()
FROM @fleet f JOIN dbo.Vehicles v ON v.LicensePlate=f.LicensePlate
JOIN @owners names ON names.OwnerKey=f.OwnerKey JOIN dbo.VehicleOwners o ON o.DisplayName=names.Title
JOIN dbo.Cities c ON c.CityKey=f.CityKey JOIN @locations definition ON definition.CityKey=f.CityKey
JOIN dbo.RentalLocations l ON l.CityId=c.CityId AND l.DisplayName=definition.Title;
INSERT dbo.VehicleRentalModes(VehicleId,RentalMode,IsEnabled,VerifiedAt)
SELECT v.VehicleId,m.Mode,1,SYSUTCDATETIME() FROM @fleet f JOIN dbo.Vehicles v ON v.LicensePlate=f.LicensePlate
CROSS JOIN(VALUES(''SelfDrive''),(''WithDriver''),(''LongTerm''))m(Mode)
WHERE m.Mode=''SelfDrive'' OR (m.Mode=''WithDriver'' AND f.SupportsDriver=1) OR (m.Mode=''LongTerm'' AND f.SupportsLongTerm=1);
INSERT dbo.VehicleRentalPolicies(VehicleId,RentalMode,SupportsInstantBooking,RequiresCollateral,CollateralAmount,CollateralDescription,
 UnlimitedMileage,IncludedKm,MileagePeriod,ExcessKmFee,AllowsIntercityTravel)
SELECT v.VehicleId,m.RentalMode,f.Instant,CASE WHEN f.NoCollateral=1 THEN 0 ELSE 1 END,
 CASE WHEN f.NoCollateral=1 THEN 0 ELSE 10000000 END,N''Chính sách MẪU SEARCH trên xe gốc; thế chấp khác tiền cọc thanh toán'',
 f.UnlimitedKm,CASE WHEN f.UnlimitedKm=1 THEN NULL WHEN m.RentalMode=''LongTerm'' THEN 4000 ELSE 300 END,
 CASE WHEN f.UnlimitedKm=1 THEN NULL WHEN m.RentalMode=''LongTerm'' THEN ''Month'' ELSE ''Day'' END,
 CASE WHEN f.UnlimitedKm=1 THEN NULL ELSE 3000 END,f.Intercity
FROM @fleet f JOIN dbo.Vehicles v ON v.LicensePlate=f.LicensePlate JOIN dbo.VehicleRentalModes m ON m.VehicleId=v.VehicleId;
INSERT dbo.VehicleHourlyRates(VehicleId,RentalMode,HourlyRate,MinimumHours,MaximumHours,IsActive)
SELECT v.VehicleId,''SelfDrive'',f.HourlyRate,4,8,1 FROM @fleet f JOIN dbo.Vehicles v ON v.LicensePlate=f.LicensePlate WHERE f.HourlyRate IS NOT NULL;
-- Monthly rates remain exactly the original 145 rows. No new monthly prices.
INSERT dbo.VehicleAmenities(VehicleId,RentalMode,AmenityId,Quantity,AdditionalFee,IsActive)
SELECT v.VehicleId,m.RentalMode,a.AmenityId,1,0,1 FROM @fleet f JOIN dbo.Vehicles v ON v.LicensePlate=f.LicensePlate
JOIN dbo.VehicleRentalModes m ON m.VehicleId=v.VehicleId CROSS JOIN dbo.Amenities a
WHERE a.AmenityCode=''Bluetooth'' OR (a.AmenityCode=''ChildSeat'' AND f.ChildSeat=1) OR (a.AmenityCode=''SportPackage'' AND f.Sport=1);
INSERT dbo.VehicleRentalPurposes(VehicleId,RentalMode,PurposeId,VerifiedAt)
SELECT v.VehicleId,m.RentalMode,p.PurposeId,SYSUTCDATETIME() FROM @fleet f JOIN dbo.Vehicles v ON v.LicensePlate=f.LicensePlate
JOIN dbo.VehicleRentalModes m ON m.VehicleId=v.VehicleId CROSS JOIN dbo.RentalPurposes p
WHERE (p.PurposeCode=''NewDriver'' AND v.Transmission=''Automatic'') OR (p.PurposeCode=''Family'' AND f.ChildSeat=1)
 OR (p.PurposeCode=''Business'' AND f.Instant=1) OR (p.PurposeCode=''Friends'' AND v.SeatCount=7)
 OR (p.PurposeCode=''Camping'' AND f.Intercity=1 AND v.SeatCount=7) OR (p.PurposeCode=''Event'' AND f.Sport=1);
INSERT dbo.VehicleTravelAreas(VehicleId,RentalMode,CityId,IsActive)
SELECT v.VehicleId,m.RentalMode,c.CityId,1 FROM @fleet f JOIN dbo.Vehicles v ON v.LicensePlate=f.LicensePlate
JOIN dbo.VehicleRentalModes m ON m.VehicleId=v.VehicleId CROSS JOIN dbo.Cities c
WHERE f.Intercity=1 AND c.CityKey IN(''DEMO-CT'',''DEMO-HCM'',''DEMO-DN'',''DEMO-HN'');
INSERT dbo.VehicleDeliveryAreas(VehicleId,RentalMode,CityId,FeeModel,FeePerKm,MinimumFee,ServiceRadiusKm,FreeWithinKm,IsActive)
SELECT v.VehicleId,m.RentalMode,c.CityId,''PerKm'',15000,50000,20,f.FreeKm,1 FROM @fleet f JOIN dbo.Vehicles v ON v.LicensePlate=f.LicensePlate
JOIN dbo.VehicleRentalModes m ON m.VehicleId=v.VehicleId JOIN dbo.Cities c ON c.CityKey=f.CityKey WHERE f.Delivery=1;
INSERT dbo.VehicleDriverRoutes(VehicleId,RouteType,PickupCityId,DestinationCityId,PricingModel,IsActive)
SELECT v.VehicleId,r.Kind,pickup.CityId,destination.CityId,''DailyPolicy'',1 FROM @fleet f JOIN dbo.Vehicles v ON v.LicensePlate=f.LicensePlate
JOIN dbo.Cities pickup ON pickup.CityKey=f.CityKey CROSS JOIN dbo.Cities destination
CROSS JOIN(VALUES(''InnerCity''),(''InterCityRoundTrip''),(''InterCityOneWay''))r(Kind)
WHERE f.SupportsDriver=1 AND destination.CityKey IN(''DEMO-CT'',''DEMO-HCM'',''DEMO-DN'',''DEMO-HN'')
 AND ((r.Kind=''InnerCity'' AND pickup.CityId=destination.CityId) OR (r.Kind<>''InnerCity'' AND pickup.CityId<>destination.CityId));
-- Reuse the original vouchers; add eligibility only.
INSERT dbo.VehicleVoucherEligibility(VehicleId,RentalMode,VoucherId,IsActive)
SELECT v.VehicleId,m.RentalMode,voucher.VoucherId,1 FROM @fleet f JOIN dbo.Vehicles v ON v.LicensePlate=f.LicensePlate
JOIN dbo.VehicleRentalModes m ON m.VehicleId=v.VehicleId CROSS JOIN dbo.Vouchers voucher
WHERE f.SampleNo IN(1,4,5,9,10,17,27) AND voucher.VoucherCode=''WELCOME10'' AND m.RentalMode<>''LongTerm'';
INSERT dbo.VehicleVoucherEligibility(VehicleId,RentalMode,VoucherId,IsActive)
SELECT v.VehicleId,''SelfDrive'',voucher.VoucherId,1 FROM @fleet f JOIN dbo.Vehicles v ON v.LicensePlate=f.LicensePlate CROSS JOIN dbo.Vouchers voucher
WHERE (f.SampleNo=19 AND voucher.VoucherCode=''OLDPROMO'') OR (f.SampleNo=20 AND voucher.VoucherCode=''NEXTMONTH'');
-- Six synthetic completed histories. Readings end at the existing odometer;
-- the Vehicles row and its original odometer are never changed.
DECLARE @reviewVehicle bigint=(SELECT VehicleId FROM dbo.Vehicles WHERE LicensePlate=''DEMO-CRMS-001''),
 @owner bigint=(SELECT OwnerId FROM dbo.VehicleOwners WHERE DisplayName=N''Chủ xe An · MẪU SEARCH''),
 @odometer int=(SELECT OdometerKm FROM dbo.Vehicles WHERE LicensePlate=''DEMO-CRMS-001''),
 @rate decimal(18,2)=(SELECT DailyRate FROM dbo.Vehicles WHERE LicensePlate=''DEMO-CRMS-001''),@i int=0;
WHILE @i<6
BEGIN
 DECLARE @start datetime2(0)=DATEADD(day,-120+@i*3,SYSUTCDATETIME()),@end datetime2(0),@created datetime2(0),@booking bigint,
  @deposit decimal(18,2)=ROUND(@rate*0.5,2);
 SET @end=DATEADD(day,1,@start); SET @created=DATEADD(day,-2,@start);
 INSERT dbo.Bookings(BookingCode,RequestKey,CustomerId,VehicleId,RentalType,DepositMethod,StartAt,EndAt,RentalDays,
 VehicleDailyRate,DriverDailyRate,VehicleAmount,DriverAmount,DiscountAmount,TotalAmount,DepositAmount,DepositDeadline,BookingStatus,ConfirmedAt,CreatedAt,UpdatedAt)
 VALUES(CONCAT(''SEARCH29-HISTORY-'',@i),NEWID(),@customer,@reviewVehicle,''SelfDrive'',''Cash'',@start,@end,1,@rate,0,@rate,0,0,@rate,@deposit,
 DATEADD(hour,6,@created),''Confirmed'',DATEADD(hour,1,@created),@created,@end);
 SET @booking=SCOPE_IDENTITY();
 INSERT dbo.BookingOfferSnapshots(BookingId,VehicleId,OwnerId,RentalMode,PriceUnit,PolicySnapshotJson,CreatedAt)
 VALUES(@booking,@reviewVehicle,@owner,''SelfDrive'',''Day'',N''{"version":2,"demo":true,"source":"original29","scenario":"owner_rating"}'',@created);
 INSERT dbo.Payments(BookingId,PaymentType,PaymentMethod,Amount,PaymentStatus,VerifiedByUserId,PaidAt,CreatedAt,Note) VALUES
 (@booking,''Deposit'',''Cash'',@deposit,''Success'',@staff,DATEADD(hour,1,@created),@created,N''MẪU SEARCH: không phải giao dịch thật''),
 (@booking,''Balance'',''Cash'',@rate-@deposit,''Success'',@staff,@end,@end,N''MẪU SEARCH: không phải giao dịch thật'');
 INSERT dbo.VehicleHandovers(BookingId,StaffId,HandedOverAt,OdometerOutKm,FuelPercentOut,ConditionNotes)
 VALUES(@booking,@staff,@start,@odometer-(6-@i)*100,100,N''Lịch sử mẫu phục vụ bộ lọc đánh giá'');
 INSERT dbo.VehicleReturns(BookingId,StaffId,ReturnedAt,OdometerInKm,FuelPercentIn,InspectionNotes)
 VALUES(@booking,@staff,@end,@odometer-(5-@i)*100,100,N''Lịch sử mẫu phục vụ bộ lọc đánh giá'');
 INSERT dbo.BookingStatusHistory(BookingId,OldStatus,NewStatus,ChangedByUserId,ChangedAt,ChangeNote) VALUES
 (@booking,NULL,''PendingDeposit'',@staff,@created,N''MẪU SEARCH''),(@booking,''PendingDeposit'',''Confirmed'',@staff,DATEADD(hour,1,@created),N''MẪU SEARCH''),
 (@booking,''Confirmed'',''InUse'',@staff,@start,N''MẪU SEARCH''),(@booking,''InUse'',''Returned'',@staff,@end,N''MẪU SEARCH''),
 (@booking,''Returned'',''Completed'',@staff,DATEADD(minute,1,@end),N''MẪU SEARCH'');
 UPDATE dbo.Bookings SET BookingStatus=''Completed'' WHERE BookingId=@booking;
 INSERT dbo.Reviews(BookingId,Rating,Comment,CreatedAt) VALUES(@booking,CASE WHEN @i=5 THEN 4 ELSE 5 END,
 N''Đánh giá MẪU SEARCH để thử bộ lọc chủ xe; không phải đánh giá khách hàng thật.'',DATEADD(day,1,@end));
 SET @i+=1;
END;
-- Existing Swift: owner block on day +5 to +6 after installation.
INSERT dbo.VehicleCalendarBlocks(VehicleId,StartAt,EndAt,Reason,CreatedByUserId)
SELECT VehicleId,DATEADD(hour,2,DATEADD(day,5,CONVERT(datetime2(0),CONVERT(date,SYSUTCDATETIME())))),
 DATEADD(hour,2,DATEADD(day,6,CONVERT(datetime2(0),CONVERT(date,SYSUTCDATETIME())))),N''MẪU SEARCH29: chủ xe khóa lịch'',@staff
FROM dbo.Vehicles WHERE LicensePlate=''DEMO-CRMS-017'';
-- Existing Innova: one confirmed booking on day +8 to +9. Original procedures.
DECLARE @reservedVehicle bigint=(SELECT VehicleId FROM dbo.Vehicles WHERE LicensePlate=''65A-24680''),
 @reservedStart datetime2(0)=DATEADD(hour,2,DATEADD(day,8,CONVERT(datetime2(0),CONVERT(date,SYSUTCDATETIME())))),
 @reservedEnd datetime2(0),@reservedBooking bigint,@payment bigint,@refund bit,
 @request uniqueidentifier=''29000000-0000-0000-0000-000000000001'';
SET @reservedEnd=DATEADD(day,1,@reservedStart);
EXEC dbo.usp_CreateBooking @CustomerId=@customer,@VehicleId=@reservedVehicle,@RentalType=''SelfDrive'',@DepositMethod=''Cash'',
 @StartAt=@reservedStart,@EndAt=@reservedEnd,@RequestKey=@request,@NewBookingId=@reservedBooking OUTPUT;
INSERT dbo.Payments(BookingId,PaymentType,PaymentMethod,Amount,PaymentStatus,Note)
SELECT BookingId,''Deposit'',''Cash'',DepositAmount,''Pending'',N''MẪU SEARCH29: giữ xe để thử trùng lịch'' FROM dbo.Bookings WHERE BookingId=@reservedBooking;
SET @payment=SCOPE_IDENTITY();
EXEC dbo.usp_ConfirmDeposit @PaymentId=@payment,@VerifiedByUserId=@staff,@NeedsManualRefund=@refund OUTPUT;
IF @refund<>0 THROW 52607,''Search availability sample booking did not confirm correctly.'',1;
EXEC sys.sp_addextendedproperty @name=N''CRMS.SearchOriginal29SeedVersion'',@value=N''2.0 - search metadata on the original 29 sample vehicles only'',
 @level0type=N''SCHEMA'',@level0name=N''dbo'',@level1type=N''TABLE'',@level1name=N''SearchSettings'';
COMMIT TRANSACTION;
PRINT N''Đã bổ sung dữ liệu search trên đúng 29 xe gốc; không tạo thêm xe.'';
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;');
EXEC(N'/* OPTIONAL QA FIXTURES, not real transactions.
   Run in the SAME demo database after CRMS.sql, CRMS.Demo.sql, CRMS.SearchDemo.sql.
   26 named bookings cover the rental/payment lifecycle. Dates are relative to installation.
   This file builds known UI states directly; procedure correctness is tested separately.
   One transaction + version marker: reruns preserve edits and never duplicate fixtures.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF OBJECT_ID(''dbo.BookingOfferSnapshots'',''U'') IS NULL OR COL_LENGTH(''dbo.Bookings'',''PriceUnit'') IS NULL
 THROW 52700,''Install CRMS.sql v2.1 first.'',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE (class=0 OR (class=1 AND major_id=OBJECT_ID(N''dbo.SearchSettings'') AND minor_id=0)) AND name=N''CRMS.SearchOriginal29SeedVersion'')
 THROW 52701,''Run CRMS.Demo.sql and CRMS.SearchDemo.sql first.'',1;
IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE (class=0 OR (class=1 AND major_id=OBJECT_ID(N''dbo.SearchSettings'') AND minor_id=0)) AND name=N''CRMS.QaScenariosVersion'')
BEGIN PRINT N''QA scenarios already installed; existing changes preserved.''; RETURN; END;
IF EXISTS(SELECT 1 FROM dbo.Bookings WHERE BookingCode LIKE ''QA-%'')
 OR EXISTS(SELECT 1 FROM dbo.Vouchers WHERE VoucherCode LIKE ''QA-%'')
 OR EXISTS(SELECT 1 FROM dbo.Users WHERE Email IN(N''qa.inactive.customer@crms.example.com'',N''qa.inactive.staff@crms.example.com''))
 THROW 52702,''QA names already exist without the version marker. Review the database; no rows were overwritten.'',1;

BEGIN TRY
BEGIN TRANSACTION;
DECLARE @now datetime2(0)=SYSUTCDATETIME(),
 @admin bigint=(SELECT UserId FROM dbo.Users WHERE Email=N''admin.demo@crms.example.com'' AND UserRole=''Admin'' AND IsActive=1),
 @staff bigint=(SELECT UserId FROM dbo.Users WHERE Email=N''staff01@crms.example.com'' AND UserRole=''Staff'' AND IsActive=1),
 @customer bigint=(SELECT UserId FROM dbo.Users WHERE Email=N''customer01@crms.example.com'' AND UserRole=''Customer'' AND IsActive=1),
 @driver1 bigint=(SELECT DriverId FROM dbo.Drivers WHERE LicenseNumber=N''DEMO-DL-001'' AND IsActive=1),
 @driver2 bigint=(SELECT DriverId FROM dbo.Drivers WHERE LicenseNumber=N''DEMO-DL-002'' AND IsActive=1);
IF @admin IS NULL OR @staff IS NULL OR @customer IS NULL OR @driver1 IS NULL OR @driver2 IS NULL
 THROW 52703,''Required active demo users/drivers are missing.'',1;

-- These accounts use the same role passwords as the base demo, but login must fail.
INSERT dbo.Users(FullName,Email,PasswordHash,UserRole,IsActive)
SELECT N''QA - Customer inactive'',N''qa.inactive.customer@crms.example.com'',PasswordHash,''Customer'',0 FROM dbo.Users WHERE UserId=@customer;
INSERT dbo.Users(FullName,Email,PasswordHash,UserRole,IsActive)
SELECT N''QA - Staff inactive'',N''qa.inactive.staff@crms.example.com'',PasswordHash,''Staff'',0 FROM dbo.Users WHERE UserId=@staff;

-- Deterministic voucher boundaries, independent of the original campaign vouchers.
INSERT dbo.Vouchers(VoucherCode,DiscountType,DiscountValue,MaxDiscountAmount,MinimumOrderAmount,StartsAt,EndsAt,UsageLimit,PerCustomerLimit,IsActive) VALUES
 (''QA-GLOBAL-1'',''Fixed'',50000,NULL,0,DATEADD(day,-1,@now),DATEADD(day,365,@now),1,10,1),
 (''QA-CUSTOMER-1'',''Fixed'',50000,NULL,0,DATEADD(day,-1,@now),DATEADD(day,365,@now),100,1,1),
 (''QA-VALID'',''Percent'',10,100000,500000,DATEADD(day,-1,@now),DATEADD(day,365,@now),100,3,1),
 (''QA-EXPIRED'',''Fixed'',50000,NULL,0,DATEADD(day,-10,@now),DATEADD(day,-1,@now),100,3,1),
 (''QA-FUTURE'',''Fixed'',50000,NULL,0,DATEADD(day,30,@now),DATEADD(day,365,@now),100,3,1),
 (''QA-DISABLED'',''Fixed'',50000,NULL,0,DATEADD(day,-1,@now),DATEADD(day,365,@now),100,3,0);
INSERT dbo.VehicleVoucherEligibility(VehicleId,RentalMode,VoucherId,IsActive)
SELECT v.VehicleId,''SelfDrive'',q.VoucherId,1
FROM dbo.Vehicles v CROSS JOIN dbo.Vouchers q
WHERE v.LicensePlate IN(''65A-12345'',''65A-67890'',''65A-24680'',''DEMO-CRMS-001'',''DEMO-CRMS-025'') AND q.VoucherCode LIKE ''QA-%'';

DECLARE @cases TABLE(
 Id int PRIMARY KEY,Code varchar(30) UNIQUE,Plate varchar(20),Status varchar(16),Mode varchar(12),
 Unit varchar(5),StartHours int,DurationMinutes int,Months tinyint NULL,
 DepositState varchar(10),Method varchar(5),PayBalance bit,RefundFraction decimal(3,2),
 LateFee decimal(18,2),DamageFee decimal(18,2),OtherFee decimal(18,2),VoucherCode nvarchar(40) NULL,
 Expected nvarchar(300));
INSERT @cases VALUES
 (1,''QA-PENDING-CASH'',''DEMO-CRMS-001'',''PendingDeposit'',''SelfDrive'',''Day'',48,1440,NULL,''None'',''Cash'',0,0,0,0,0,NULL,N''Chờ thu cọc tiền mặt''),
 (2,''QA-PENDING-VNPAY'',''DEMO-CRMS-002'',''PendingDeposit'',''SelfDrive'',''Day'',48,1440,NULL,''Pending'',''VNPay'',0,0,0,0,0,NULL,N''Đang chờ kết quả VNPay mô phỏng''),
 (3,''QA-FAILED-VNPAY'',''DEMO-CRMS-003'',''PendingDeposit'',''SelfDrive'',''Day'',48,1440,NULL,''Failed'',''VNPay'',0,0,0,0,0,NULL,N''Thanh toán thất bại, đơn vẫn còn hạn giữ chỗ''),
 (4,''QA-CANCELLED-PAYMENT'',''DEMO-CRMS-004'',''PendingDeposit'',''SelfDrive'',''Day'',48,1440,NULL,''Cancelled'',''VNPay'',0,0,0,0,0,NULL,N''Hủy lần thanh toán, không hủy đơn thuê''),
 (5,''QA-EXPIRED'',''DEMO-CRMS-005'',''Expired'',''SelfDrive'',''Day'',48,1440,NULL,''None'',''Cash'',0,0,0,0,0,NULL,N''Hết hạn cọc, xe được giải phóng''),
 (6,''QA-CANCELLED-UNPAID'',''DEMO-CRMS-006'',''Cancelled'',''SelfDrive'',''Day'',48,1440,NULL,''None'',''Cash'',0,0,0,0,0,NULL,N''Hủy đơn chưa thanh toán''),
 (7,''QA-REFUND-DUE'',''DEMO-CRMS-007'',''Cancelled'',''SelfDrive'',''Day'',48,1440,NULL,''Success'',''Cash'',0,0,0,0,0,NULL,N''Hủy sau cọc, còn toàn bộ tiền cọc cần xử lý''),
 (8,''QA-REFUND-PARTIAL'',''DEMO-CRMS-008'',''Cancelled'',''SelfDrive'',''Day'',48,1440,NULL,''Success'',''Cash'',0,0.40,0,0,0,NULL,N''Đã hoàn 40% cọc, còn 60%''),
 (9,''QA-REFUND-FULL'',''DEMO-CRMS-009'',''Cancelled'',''SelfDrive'',''Day'',48,1440,NULL,''Success'',''Cash'',0,1,0,0,0,NULL,N''Đã hoàn đủ cọc qua chuyển khoản, NetPaid = 0''),
 (10,''QA-INUSE'',''DEMO-CRMS-010'',''InUse'',''SelfDrive'',''Day'',-4,1440,NULL,''Success'',''Cash'',0,0,0,0,0,NULL,N''Đã giao xe, chưa đến giờ trả''),
 (11,''QA-OVERDUE'',''DEMO-CRMS-011'',''InUse'',''SelfDrive'',''Day'',-48,1440,NULL,''Success'',''Cash'',0,0,0,0,0,NULL,N''Quá giờ trả, vẫn phải chặn các đơn sau''),
 (12,''QA-RETURNED-DUE'',''DEMO-CRMS-012'',''Returned'',''SelfDrive'',''Day'',-96,1440,NULL,''Success'',''Cash'',0,0,50000,100000,20000,NULL,N''Đã trả xe, còn tiền thuê và 170.000đ phụ phí''),
 (13,''QA-RETURNED-PAID'',''DEMO-CRMS-013'',''Returned'',''SelfDrive'',''Day'',-120,1440,NULL,''Success'',''Cash'',1,0,50000,0,0,NULL,N''Đã trả và thanh toán đủ, chờ bấm hoàn tất''),
 (14,''QA-COMPLETED'',''DEMO-CRMS-014'',''Completed'',''SelfDrive'',''Day'',-144,1440,NULL,''Success'',''Cash'',1,0,0,0,0,NULL,N''Đủ biên bản, thanh toán và đánh giá''),
 (15,''QA-DRIVER-CONFIRMED'',''DEMO-CRMS-018'',''Confirmed'',''WithDriver'',''Day'',96,1440,NULL,''Success'',''Cash'',0,0,0,0,0,NULL,N''Có tài xế, đã phân công, chưa giao xe''),
 (16,''QA-DRIVER-INUSE'',''DEMO-CRMS-019'',''InUse'',''WithDriver'',''Day'',-4,1440,NULL,''Success'',''Cash'',0,0,0,0,0,NULL,N''Có tài xế, đang trong chuyến thuê''),
 (17,''QA-HOURLY'',''DEMO-CRMS-016'',''Confirmed'',''SelfDrive'',''Hour'',48,270,NULL,''Success'',''Cash'',0,0,0,0,0,NULL,N''Thuê 4,5 giờ, làm tròn theo bước 60 phút thành 5 giờ''),
 (18,''QA-MONTH-1'',''DEMO-CRMS-020'',''Confirmed'',''LongTerm'',''Month'',168,0,1,''Success'',''Cash'',0,0,0,0,0,NULL,N''Thuê đúng 1 tháng lịch''),
 (19,''QA-MONTH-3'',''DEMO-CRMS-021'',''Confirmed'',''LongTerm'',''Month'',168,0,3,''Success'',''Cash'',0,0,0,0,0,NULL,N''Thuê 3 tháng, giá tháng đã giảm 2%''),
 (20,''QA-MONTH-12'',''DEMO-CRMS-022'',''Confirmed'',''LongTerm'',''Month'',168,0,12,''Success'',''Cash'',0,0,0,0,0,NULL,N''Thuê 12 tháng, giá tháng đã giảm 5%''),
 (21,''QA-VNPAY-SUCCESS'',''DEMO-CRMS-023'',''Confirmed'',''SelfDrive'',''Day'',96,1440,NULL,''Success'',''VNPay'',0,0,0,0,0,NULL,N''VNPay thành công mô phỏng, không gọi cổng thanh toán''),
 (22,''QA-VNPAY-LATE'',''DEMO-CRMS-024'',''Expired'',''SelfDrive'',''Day'',96,1440,NULL,''Success'',''VNPay'',0,0,0,0,0,NULL,N''Tiền về sau hạn cọc, đơn vẫn hết hạn, cần hoàn tiền''),
 (23,''QA-VOUCHER-GLOBAL'',''65A-12345'',''Confirmed'',''SelfDrive'',''Day'',240,1440,NULL,''Success'',''Cash'',0,0,0,0,0,''QA-GLOBAL-1'',N''Voucher đã dùng hết hạn mức toàn hệ thống''),
 (24,''QA-VOUCHER-CUSTOMER'',''65A-67890'',''Confirmed'',''SelfDrive'',''Day'',240,1440,NULL,''Success'',''Cash'',0,0,0,0,0,''QA-CUSTOMER-1'',N''customer01 đã dùng hết lượt riêng, khách khác còn dùng được''),
 (25,''QA-VOUCHER-RESERVED'',''65A-24680'',''PendingDeposit'',''SelfDrive'',''Day'',288,1440,NULL,''None'',''Cash'',0,0,0,0,0,''QA-VALID'',N''Đã giữ lượt voucher, chưa thanh toán''),
 (26,''QA-VOUCHER-RELEASED'',''DEMO-CRMS-025'',''Cancelled'',''SelfDrive'',''Day'',240,1440,NULL,''None'',''Cash'',0,0,0,0,0,''QA-VALID'',N''Hủy đơn, lượt voucher được giải phóng'');

DECLARE @i int=1;
WHILE @i<=26
BEGIN
 DECLARE @code varchar(30),@plate varchar(20),@status varchar(16),@mode varchar(12),@unit varchar(5),
 @startHours int,@minutes int,@months tinyint,@depositState varchar(10),@method varchar(5),@payBalance bit,
 @refundFraction decimal(3,2),@late decimal(18,2),@damage decimal(18,2),@other decimal(18,2),@voucherCode nvarchar(40),@expected nvarchar(300);
 SELECT @code=Code,@plate=Plate,@status=Status,@mode=Mode,@unit=Unit,@startHours=StartHours,@minutes=DurationMinutes,
 @months=Months,@depositState=DepositState,@method=Method,@payBalance=PayBalance,@refundFraction=RefundFraction,
 @late=LateFee,@damage=DamageFee,@other=OtherFee,@voucherCode=VoucherCode,@expected=Expected FROM @cases WHERE Id=@i;
 DECLARE @vehicle bigint=NULL,@owner bigint=NULL,@daily decimal(18,2)=NULL,@odometer int,
 @start datetime2(0)=DATEADD(hour,@startHours,@now),@end datetime2(0),@created datetime2(0),@deadline datetime2(0),
 @days int,@units decimal(10,2),@rate decimal(18,2)=NULL,@driverRate decimal(18,2)=0,@driver bigint=NULL,
 @carAmount decimal(18,2),@driverAmount decimal(18,2),@discount decimal(18,2)=0,@total decimal(18,2),@deposit decimal(18,2),@booking bigint,
 @voucher bigint=NULL,@voucherType varchar(10),@voucherValue decimal(18,2),@voucherMax decimal(18,2),@increment int;
 SELECT @vehicle=v.VehicleId,@owner=p.OwnerId,@daily=v.DailyRate,@odometer=v.OdometerKm
 FROM dbo.Vehicles v JOIN dbo.VehicleSearchProfiles p ON p.VehicleId=v.VehicleId AND p.IsPublished=1
 JOIN dbo.VehicleRentalModes m ON m.VehicleId=v.VehicleId AND m.RentalMode=@mode AND m.IsEnabled=1
 WHERE v.LicensePlate=@plate AND v.OperationalStatus=''Active'';
 IF @vehicle IS NULL OR @owner IS NULL THROW 52704,''A required vehicle/search mode is missing or disabled.'',1;
 SET @end=CASE WHEN @unit=''Month'' THEN DATEADD(month,@months,@start) ELSE DATEADD(minute,@minutes,@start) END;
 SET @days=CONVERT(int,(DATEDIFF_BIG(second,@start,@end)+86399)/86400);
 SET @created=CASE WHEN @start<@now THEN DATEADD(day,-2,@start) ELSE DATEADD(hour,-4,@now) END;
 SET @deadline=CASE WHEN @status=''Expired'' THEN DATEADD(hour,-2,@now) ELSE DATEADD(day,1,@created) END;
 IF @unit=''Month''
 BEGIN
  SELECT @rate=MonthlyRate FROM dbo.VehicleLongTermRates WHERE VehicleId=@vehicle AND DurationMonths=@months AND IsActive=1;
  SET @units=@months;
 END
 ELSE IF @unit=''Hour''
 BEGIN
  SELECT @rate=HourlyRate,@increment=BillingIncrementMinutes FROM dbo.VehicleHourlyRates WHERE VehicleId=@vehicle AND IsActive=1;
  SET @units=CEILING(@minutes*1.0/@increment)*@increment/60.0;
 END
 ELSE BEGIN SET @rate=@daily; SET @units=@days; END;
 IF @rate IS NULL THROW 52705,''Required hourly/monthly rate is unavailable.'',1;
 IF @mode=''WithDriver''
 BEGIN
  SELECT @driverRate=DriverDailyFee FROM dbo.RentalPolicies WHERE PolicyId=1;
  SET @driver=CASE WHEN @status=''InUse'' THEN @driver2 ELSE @driver1 END;
 END;
 SET @carAmount=ROUND(@rate*@units,2); SET @driverAmount=@driverRate*@days;
 IF @voucherCode IS NOT NULL
 BEGIN
  SELECT @voucher=VoucherId,@voucherType=DiscountType,@voucherValue=DiscountValue,@voucherMax=MaxDiscountAmount
   FROM dbo.Vouchers WHERE VoucherCode=@voucherCode;
  SET @discount=CASE WHEN @voucherType=''Percent'' THEN ROUND((@carAmount+@driverAmount)*@voucherValue/100,2) ELSE @voucherValue END;
  IF @voucherMax IS NOT NULL AND @discount>@voucherMax SET @discount=@voucherMax;
 END;
 SET @total=@carAmount+@driverAmount-@discount; SET @deposit=ROUND(@total*0.5,2);
 INSERT dbo.Bookings(BookingCode,RequestKey,CustomerId,VehicleId,DriverId,RentalType,DepositMethod,StartAt,EndAt,RentalDays,
  VehicleDailyRate,DriverDailyRate,VehicleAmount,DriverAmount,DiscountAmount,TotalAmount,DepositAmount,DepositDeadline,
  BookingStatus,CreatedAt,PriceUnit,RentalUnits,UnitRate)
 VALUES(@code,NEWID(),@customer,@vehicle,@driver,@mode,@method,@start,@end,@days,@daily,@driverRate,@carAmount,@driverAmount,
  @discount,@total,@deposit,@deadline,''PendingDeposit'',@created,@unit,@units,@rate);
 SET @booking=SCOPE_IDENTITY();
 DECLARE @json nvarchar(max)=(SELECT CAST(1 AS bit) AS demo,@code AS scenario,@unit AS priceUnit,@units AS rentalUnits,@rate AS unitRate FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.BookingOfferSnapshots(BookingId,VehicleId,OwnerId,RentalMode,PriceUnit,PolicySnapshotJson,CreatedAt)
 VALUES(@booking,@vehicle,@owner,@mode,@unit,@json,@created);
 INSERT dbo.BookingStatusHistory(BookingId,NewStatus,ChangedByUserId,ChangedAt,ChangeNote)
 VALUES(@booking,''PendingDeposit'',@customer,@created,CONCAT(N''QA SAMPLE: '',@expected));

 IF @depositState<>''None''
 INSERT dbo.Payments(BookingId,PaymentType,PaymentMethod,Amount,PaymentStatus,GatewayTxnRef,GatewayTransactionNo,GatewayResponseCode,
  IdempotencyKey,VerifiedByUserId,PaidAt,CreatedAt,Note)
 VALUES(@booking,''Deposit'',@method,@deposit,@depositState,
  CASE WHEN @method=''VNPay'' THEN CONCAT(''SIM-'',@code) END,
  CASE WHEN @method=''VNPay'' AND @depositState=''Success'' THEN CONCAT(''SIM-TXN-'',@code) END,
  CASE WHEN @method=''VNPay'' THEN CASE WHEN @depositState=''Success'' THEN ''00'' WHEN @depositState=''Failed'' THEN ''24'' END END,
  NEWID(),CASE WHEN @method=''Cash'' AND @depositState=''Success'' THEN @staff END,
  CASE WHEN @depositState=''Success'' THEN CASE WHEN @status=''Expired'' THEN DATEADD(hour,-1,@now) ELSE DATEADD(hour,1,@created) END END,
  DATEADD(minute,10,@created),N''QA SAMPLE ONLY - no real money / no gateway call'');
 IF @depositState=''Success'' AND @status<>''Expired''
 BEGIN
  UPDATE dbo.Bookings SET ConfirmedAt=DATEADD(hour,1,@created) WHERE BookingId=@booking;
  INSERT dbo.BookingStatusHistory(BookingId,OldStatus,NewStatus,ChangedByUserId,ChangedAt,ChangeNote)
   VALUES(@booking,''PendingDeposit'',''Confirmed'',@staff,DATEADD(hour,1,@created),N''QA SAMPLE: deposit confirmed'');
 END;
 IF @status IN(''InUse'',''Returned'',''Completed'')
 BEGIN
  INSERT dbo.VehicleHandovers(BookingId,StaffId,HandedOverAt,OdometerOutKm,FuelPercentOut,ConditionNotes)
   VALUES(@booking,@staff,@start,CASE WHEN @status=''InUse'' THEN @odometer ELSE CASE WHEN @odometer>=100 THEN @odometer-100 ELSE 0 END END,100,N''QA SAMPLE: handover'');
  INSERT dbo.BookingStatusHistory(BookingId,OldStatus,NewStatus,ChangedByUserId,ChangedAt,ChangeNote)
   VALUES(@booking,''Confirmed'',''InUse'',@staff,@start,N''QA SAMPLE: handover'');
 END;
 IF @status IN(''Returned'',''Completed'')
 BEGIN
  INSERT dbo.VehicleReturns(BookingId,StaffId,ReturnedAt,OdometerInKm,FuelPercentIn,LateFee,DamageFee,OtherFee,InspectionNotes)
   VALUES(@booking,@staff,DATEADD(hour,1,@end),@odometer,70,@late,@damage,@other,N''QA SAMPLE: returned with inspection'');
  INSERT dbo.BookingStatusHistory(BookingId,OldStatus,NewStatus,ChangedByUserId,ChangedAt,ChangeNote)
   VALUES(@booking,''InUse'',''Returned'',@staff,DATEADD(hour,1,@end),N''QA SAMPLE: return'');
 END;
 IF @payBalance=1
 INSERT dbo.Payments(BookingId,PaymentType,PaymentMethod,Amount,PaymentStatus,IdempotencyKey,VerifiedByUserId,PaidAt,CreatedAt,Note)
  VALUES(@booking,''Balance'',''Cash'',@total+@late+@damage+@other-@deposit,''Success'',NEWID(),@staff,DATEADD(hour,2,@end),DATEADD(hour,2,@end),N''QA SAMPLE: final payment includes fees'');
 IF @refundFraction>0
 INSERT dbo.Payments(BookingId,PaymentType,PaymentMethod,Amount,PaymentStatus,IdempotencyKey,VerifiedByUserId,PaidAt,CreatedAt,Note)
  VALUES(@booking,''Refund'',CASE WHEN @refundFraction=1 THEN ''BankTransfer'' ELSE ''Cash'' END,ROUND(@deposit*@refundFraction,2),
   ''Success'',NEWID(),@staff,DATEADD(hour,-1,@now),DATEADD(hour,-1,@now),CONCAT(''QA-REFUND-APPROVAL-'',@i));
 IF @status IN(''Cancelled'',''Expired'') AND @depositState=''Success'' AND @refundFraction<1
 INSERT dbo.Payments(BookingId,PaymentType,PaymentMethod,Amount,PaymentStatus,CreatedAt,Note)
  VALUES(@booking,''Refund'',''BankTransfer'',@deposit-ROUND(@deposit*@refundFraction,2),''Pending'',@now,N''QA SAMPLE: manual refund awaiting approval'');
 IF @status IN(''Cancelled'',''Expired'',''Completed'')
 INSERT dbo.BookingStatusHistory(BookingId,OldStatus,NewStatus,ChangedByUserId,ChangedAt,ChangeNote)
  VALUES(@booking,CASE WHEN @status=''Completed'' THEN ''Returned'' WHEN @status=''Cancelled'' AND @depositState=''Success'' THEN ''Confirmed'' ELSE ''PendingDeposit'' END,
   @status,@staff,CASE WHEN @status=''Completed'' THEN DATEADD(hour,3,@end) WHEN @status=''Expired'' THEN @deadline ELSE DATEADD(hour,-2,@now) END,N''QA SAMPLE: final state'');
 UPDATE dbo.Bookings SET BookingStatus=@status,
  CancelledAt=CASE WHEN @status=''Cancelled'' THEN DATEADD(hour,-2,@now) END,
  CancelReason=CASE WHEN @status=''Cancelled'' THEN N''QA SAMPLE: cancellation/refund test'' END,UpdatedAt=@now WHERE BookingId=@booking;
 IF @voucher IS NOT NULL
 INSERT dbo.BookingVouchers(BookingId,VoucherId,DiscountSnapshot,RedemptionStatus,CreatedAt,AppliedAt)
 VALUES(@booking,@voucher,@discount,CASE WHEN @status IN(''Cancelled'',''Expired'') THEN ''Released'' WHEN @depositState=''Success'' THEN ''Applied'' ELSE ''Reserved'' END,
  @created,CASE WHEN @depositState=''Success'' THEN DATEADD(hour,1,@created) END);
 IF @status=''Completed''
 INSERT dbo.Reviews(BookingId,Rating,Comment,CreatedAt) VALUES(@booking,5,N''QA SAMPLE: completed rental review'',DATEADD(hour,4,@end));
 SET @i+=1;
END;

-- Complement the existing Scheduled/Completed maintenance samples.
INSERT dbo.MaintenanceRecords(VehicleId,CreatedByUserId,StartAt,EndAt,MaintenanceStatus,Description,Cost)
SELECT VehicleId,@admin,DATEADD(hour,-2,@now),DATEADD(hour,22,@now),''InProgress'',N''QA SAMPLE: maintenance in progress'',300000
FROM dbo.Vehicles WHERE LicensePlate=''DEMO-CRMS-015'';
INSERT dbo.MaintenanceRecords(VehicleId,CreatedByUserId,StartAt,EndAt,MaintenanceStatus,Description,Cost)
SELECT VehicleId,@admin,DATEADD(day,4,@now),DATEADD(day,5,@now),''Cancelled'',N''QA SAMPLE: cancelled maintenance must not block availability'',0
FROM dbo.Vehicles WHERE LicensePlate=''DEMO-CRMS-015'';
INSERT dbo.VehicleCalendarBlocks(VehicleId,StartAt,EndAt,Reason,IsActive,CreatedByUserId)
SELECT VehicleId,DATEADD(day,9,@now),DATEADD(day,10,@now),N''QA SAMPLE: inactive block must not block availability'',0,@admin
FROM dbo.Vehicles WHERE LicensePlate=''DEMO-CRMS-017'';

EXEC sys.sp_addextendedproperty @name=N''CRMS.QaScenariosVersion'',@value=N''2.1 - 26 rental/payment fixtures'',
 @level0type=N''SCHEMA'',@level0name=N''dbo'',@level1type=N''TABLE'',@level1name=N''SearchSettings'';
EXEC sys.sp_addextendedproperty @name=N''CRMS.QaScenariosInstalledUtc'',@value=@now,
 @level0type=N''SCHEMA'',@level0name=N''dbo'',@level1type=N''TABLE'',@level1name=N''SearchSettings'';
COMMIT TRANSACTION;
SELECT c.Code,c.Expected,b.BookingStatus,b.PriceUnit,b.RentalUnits,b.UnitRate,b.TotalAmount,b.DepositAmount,b.StartAt,b.EndAt
FROM @cases c JOIN dbo.Bookings b ON b.BookingCode=c.Code ORDER BY c.Id;
PRINT N''26 QA scenarios installed. VNPay records are simulations, not gateway tests.'';
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;');
IF @DemoAlreadyInstalled=0
BEGIN
EXEC(N'/* Read-only verification immediately after all three seed files.
   Run BEFORE changing demo records in the application.
   Throws on missing/inconsistent fixtures, then prints a useful UI checklist.
*/
SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE (class=0 OR (class=1 AND major_id=OBJECT_ID(N''dbo.SearchSettings'') AND minor_id=0)) AND name=N''CRMS.QaScenariosVersion'')
 THROW 52800,''Run CRMS.Scenarios.sql first.'',1;
IF (SELECT COUNT(*) FROM dbo.Bookings WHERE BookingCode LIKE ''QA-%'')<>26
 THROW 52801,''Expected exactly 26 QA bookings.'',1;
IF (SELECT COUNT(DISTINCT BookingStatus) FROM dbo.Bookings WHERE BookingCode LIKE ''QA-%'')<>7
 THROW 52802,''QA fixtures must cover all 7 booking states.'',1;
IF (SELECT COUNT(DISTINCT p.PaymentStatus) FROM dbo.Payments p JOIN dbo.Bookings b ON b.BookingId=p.BookingId WHERE b.BookingCode LIKE ''QA-%'')<>4
 THROW 52803,''QA fixtures must cover all 4 payment states.'',1;
IF (SELECT COUNT(DISTINCT PriceUnit) FROM dbo.Bookings WHERE BookingCode LIKE ''QA-%'')<>3
 THROW 52804,''QA fixtures must cover Hour, Day and Month.'',1;
IF (SELECT COUNT(DISTINCT MaintenanceStatus) FROM dbo.MaintenanceRecords)<>4
 THROW 52805,''Maintenance samples must cover all 4 states.'',1;
IF EXISTS(SELECT 1 FROM dbo.Bookings WHERE BookingCode LIKE ''QA-%'' AND
 (UnitRate IS NULL OR RentalUnits IS NULL OR VehicleAmount<>ROUND(UnitRate*RentalUnits,2)
 OR TotalAmount<>VehicleAmount+DriverAmount-DiscountAmount OR DepositAmount<>ROUND(TotalAmount*0.5,2)))
 THROW 52806,''Inconsistent billing totals in a QA booking.'',1;
IF EXISTS(SELECT 1 FROM dbo.Bookings b LEFT JOIN dbo.BookingOfferSnapshots s ON s.BookingId=b.BookingId
 WHERE b.BookingCode LIKE ''QA-%'' AND (s.BookingId IS NULL OR s.VehicleId<>b.VehicleId OR s.RentalMode<>b.RentalType OR s.PriceUnit<>b.PriceUnit
 OR TRY_CONVERT(decimal(18,2),JSON_VALUE(s.PolicySnapshotJson,''$.unitRate'')) IS NULL
 OR TRY_CONVERT(decimal(18,2),JSON_VALUE(s.PolicySnapshotJson,''$.unitRate''))<>b.UnitRate))
 THROW 52807,''Missing or inconsistent immutable offer snapshot.'',1;
IF EXISTS(SELECT 1 FROM dbo.Bookings b WHERE b.BookingCode LIKE ''QA-%'' AND b.BookingStatus IN(''Confirmed'',''InUse'',''Returned'',''Completed'')
 AND NOT EXISTS(SELECT 1 FROM dbo.Payments p WHERE p.BookingId=b.BookingId AND p.PaymentType=''Deposit'' AND p.PaymentStatus=''Success'' AND p.Amount=b.DepositAmount))
 THROW 52808,''Confirmed/active booking is missing a successful exact deposit.'',1;
IF EXISTS(SELECT 1 FROM dbo.Bookings b WHERE b.BookingCode LIKE ''QA-%'' AND b.BookingStatus IN(''InUse'',''Returned'',''Completed'')
 AND NOT EXISTS(SELECT 1 FROM dbo.VehicleHandovers h WHERE h.BookingId=b.BookingId))
 THROW 52809,''Missing handover for an occupied/returned vehicle.'',1;
IF EXISTS(SELECT 1 FROM dbo.Bookings b WHERE b.BookingCode LIKE ''QA-%'' AND b.BookingStatus IN(''Returned'',''Completed'')
 AND NOT EXISTS(SELECT 1 FROM dbo.VehicleReturns r WHERE r.BookingId=b.BookingId))
 THROW 52810,''Missing return inspection.'',1;
IF EXISTS(SELECT 1 FROM dbo.Bookings b WHERE b.BookingCode LIKE ''QA-%''
 AND NOT EXISTS(SELECT 1 FROM dbo.BookingStatusHistory h WHERE h.BookingId=b.BookingId AND h.NewStatus=b.BookingStatus))
 THROW 52811,''Missing audit trail for current booking state.'',1;
IF EXISTS(SELECT 1 FROM dbo.Bookings b WHERE b.BookingCode LIKE ''QA-%'' AND b.BookingStatus IN(''InUse'',''Returned'',''Completed'')
 AND b.RentalType=''WithDriver'' AND b.DriverId IS NULL)
 THROW 52812,''Chauffeur rental has no assigned driver.'',1;
IF NOT EXISTS(SELECT 1 FROM dbo.Bookings WHERE BookingCode=''QA-HOURLY'' AND RentalUnits=5 AND PriceUnit=''Hour'')
 THROW 52813,''The hourly rounding fixture should bill 5 hours for 4.5 hours.'',1;
IF EXISTS(SELECT 1 FROM dbo.Bookings WHERE BookingCode IN(''QA-MONTH-1'',''QA-MONTH-3'',''QA-MONTH-12'')
 AND (EndAt<>DATEADD(month,CONVERT(int,RentalUnits),StartAt) OR PriceUnit<>''Month''))
 THROW 52814,''Monthly dates must use calendar months.'',1;
IF NOT EXISTS(SELECT 1 FROM dbo.Bookings b JOIN dbo.Payments p ON p.BookingId=b.BookingId
 WHERE b.BookingCode=''QA-VNPAY-LATE'' AND b.BookingStatus=''Expired'' AND p.PaymentStatus=''Success'' AND p.PaidAt>b.DepositDeadline)
 THROW 52815,''Late VNPay fixture should remain expired despite receiving money.'',1;
IF NOT EXISTS(SELECT 1 FROM dbo.Bookings b JOIN dbo.BookingVouchers v ON v.BookingId=b.BookingId WHERE b.BookingCode=''QA-VOUCHER-RESERVED'' AND v.RedemptionStatus=''Reserved'')
 OR NOT EXISTS(SELECT 1 FROM dbo.Bookings b JOIN dbo.BookingVouchers v ON v.BookingId=b.BookingId WHERE b.BookingCode=''QA-VOUCHER-RELEASED'' AND v.RedemptionStatus=''Released'')
 THROW 52816,''Missing reserved/released voucher fixtures.'',1;

DECLARE @balances TABLE(BookingCode varchar(30) PRIMARY KEY,Status varchar(16),Total decimal(18,2),Deposit decimal(18,2),
 Extras decimal(18,2),Received decimal(18,2),Refunded decimal(18,2),NetPaid decimal(18,2));
INSERT @balances
SELECT b.BookingCode,b.BookingStatus,b.TotalAmount,b.DepositAmount,
 COALESCE(r.LateFee+r.DamageFee+r.OtherFee,0),
 COALESCE(SUM(CASE WHEN p.PaymentStatus=''Success'' AND p.PaymentType<>''Refund'' THEN p.Amount ELSE 0 END),0),
 COALESCE(SUM(CASE WHEN p.PaymentStatus=''Success'' AND p.PaymentType=''Refund'' THEN p.Amount ELSE 0 END),0),
 COALESCE(SUM(CASE WHEN p.PaymentStatus=''Success'' THEN CASE WHEN p.PaymentType=''Refund'' THEN -p.Amount ELSE p.Amount END ELSE 0 END),0)
FROM dbo.Bookings b LEFT JOIN dbo.VehicleReturns r ON r.BookingId=b.BookingId LEFT JOIN dbo.Payments p ON p.BookingId=b.BookingId
WHERE b.BookingCode LIKE ''QA-%''
GROUP BY b.BookingCode,b.BookingStatus,b.TotalAmount,b.DepositAmount,r.LateFee,r.DamageFee,r.OtherFee;
IF EXISTS(SELECT 1 FROM @balances WHERE Refunded>Received OR NetPaid<0)
 THROW 52817,''Refund must not exceed money received.'',1;
IF NOT EXISTS(SELECT 1 FROM @balances WHERE BookingCode=''QA-REFUND-FULL'' AND NetPaid=0 AND Refunded=Deposit)
 OR NOT EXISTS(SELECT 1 FROM @balances WHERE BookingCode=''QA-REFUND-PARTIAL'' AND Refunded=ROUND(Deposit*0.4,2) AND NetPaid=Deposit-Refunded)
 THROW 52818,''Full/partial refund fixtures do not reconcile.'',1;
IF NOT EXISTS(SELECT 1 FROM @balances WHERE BookingCode=''QA-RETURNED-DUE'' AND Extras=170000 AND NetPaid=Deposit AND Total+Extras-NetPaid>0)
 OR NOT EXISTS(SELECT 1 FROM @balances WHERE BookingCode=''QA-RETURNED-PAID'' AND NetPaid=Total+Extras)
 OR EXISTS(SELECT 1 FROM @balances WHERE Status=''Completed'' AND NetPaid<Total+Extras)
 THROW 52819,''Final payment/extra-fee fixtures do not reconcile.'',1;

SELECT BookingCode,Status,Total,Deposit,Extras,Received,Refunded,NetPaid,
 CASE WHEN Status IN(''Returned'',''Completed'') THEN Total+Extras-NetPaid END AS RemainingBalance,
 CASE WHEN Status IN(''Cancelled'',''Expired'') THEN NetPaid END AS MoneyToReviewForRefund
FROM @balances ORDER BY BookingCode;
SELECT BookingStatus,COUNT(*) AS Bookings FROM dbo.Bookings WHERE BookingCode LIKE ''QA-%'' GROUP BY BookingStatus;
SELECT UserRole,IsActive,COUNT(*) AS Accounts FROM dbo.Users GROUP BY UserRole,IsActive;
PRINT N''PASS: QA fixture structure, states, billing, snapshots, audit trails and payment totals.'';
PRINT N''Run DatabaseV2Checks to test procedures, rejection paths, retries and concurrency.'';');
END;
ELSE PRINT N'Demo fixtures already installed; existing edits preserved; strict initial-fixture checks skipped.';
END;
ELSE PRINT N'Demo data disabled. Existing users, vehicles and financial history preserved.';
COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
GO
SELECT DB_NAME() AS DatabaseName,USER_NAME() AS DatabaseUser,
 (SELECT COUNT(*) FROM dbo.Vehicles) AS Vehicles,
 (SELECT COUNT(*) FROM dbo.VehicleSearchProfiles) AS SearchProfiles,
 (SELECT COUNT(*) FROM dbo.Bookings WHERE BookingCode LIKE 'QA-%' OR BookingCode LIKE 'SEARCH29-HISTORY-%' OR RequestKey='29000000-0000-0000-0000-000000000001') AS ExistingSyntheticBookings;
PRINT N'CRMS SQL upgrade completed atomically. No original records were deleted.';
GO

-- DỮ LIỆU MINH HỌA GHẾ TRẺ EM — CHỈ DÙNG DATABASE DEMO
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Sample TABLE
    (
        LicensePlate varchar(20) PRIMARY KEY,
        AgeCode varchar(40),
        AgeName nvarchar(100)
    );

    INSERT @Sample VALUES
    ('65A-24680',     'ChildSeat:0-12', N'Ghế trẻ em 0–12 tuổi (đa năng) · MẪU'),
    ('65A-12345',     'ChildSeat:0-1',  N'Ghế trẻ em 0–1 tuổi · MẪU'),
    ('DEMO-CRMS-001', 'ChildSeat:1-4',  N'Ghế trẻ em 1–4 tuổi · MẪU'),
    ('DEMO-CRMS-002', 'ChildSeat:4-7',  N'Ghế trẻ em 4–7 tuổi · MẪU'),
    ('65A-67890',     'ChildSeat:7-12', N'Ghế trẻ em 7–12 tuổi · MẪU');

    -- Kiểm tra đủ xe và dữ liệu ghế trẻ em đã có.
    IF (
        SELECT COUNT(*)
        FROM @Sample s
        JOIN dbo.Vehicles v
            ON v.LicensePlate = s.LicensePlate
        JOIN dbo.VehicleRentalModes m
            ON m.VehicleId = v.VehicleId
            AND m.RentalMode = 'SelfDrive'
            AND m.IsEnabled = 1
        JOIN dbo.VehicleAmenities a
            ON a.VehicleId = v.VehicleId
            AND a.RentalMode = 'SelfDrive'
            AND a.IsActive = 1
            AND a.Quantity > 0
        JOIN dbo.Amenities t
            ON t.AmenityId = a.AmenityId
            AND t.AmenityCode = 'ChildSeat'
            AND t.IsActive = 1
    ) <> 5
        THROW 53810,
            'Missing demo vehicles, self-drive mode or child-seat metadata. Nothing inserted.',
            1;

    -- Thêm 5 nhóm tuổi còn thiếu.
    INSERT dbo.Amenities(AmenityCode, AmenityName, IsActive)
    SELECT s.AgeCode, s.AgeName, 1
    FROM @Sample s
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.Amenities t WITH (UPDLOCK, HOLDLOCK)
        WHERE t.AmenityCode = s.AgeCode
    );

    -- Gắn nhóm tuổi vào xe, giữ số lượng và phí ghế đã khai báo.
    INSERT dbo.VehicleAmenities
    (
        VehicleId, RentalMode, AmenityId,
        Quantity, AdditionalFee, FeeUnit, IsActive
    )
    SELECT
        v.VehicleId,
        'SelfDrive',
        age.AmenityId,
        original.Quantity,
        original.AdditionalFee,
        original.FeeUnit,
        1
    FROM @Sample s
    JOIN dbo.Vehicles v
        ON v.LicensePlate = s.LicensePlate
    JOIN dbo.Amenities age
        ON age.AmenityCode = s.AgeCode
    JOIN dbo.Amenities generic
        ON generic.AmenityCode = 'ChildSeat'
    JOIN dbo.VehicleAmenities original
        ON original.VehicleId = v.VehicleId
        AND original.RentalMode = 'SelfDrive'
        AND original.AmenityId = generic.AmenityId
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.VehicleAmenities a WITH (UPDLOCK, HOLDLOCK)
        WHERE a.VehicleId = v.VehicleId
          AND a.RentalMode = 'SelfDrive'
          AND a.AmenityId = age.AmenityId
    );

    COMMIT TRANSACTION;

    -- Hiển thị kết quả để kiểm tra.
    SELECT
        v.LicensePlate,
        v.Brand,
        v.Model,
        t.AmenityName,
        a.AdditionalFee,
        a.FeeUnit,
        a.IsActive
    FROM @Sample s
    JOIN dbo.Vehicles v
        ON v.LicensePlate = s.LicensePlate
    JOIN dbo.Amenities t
        ON t.AmenityCode = s.AgeCode
    JOIN dbo.VehicleAmenities a
        ON a.VehicleId = v.VehicleId
        AND a.RentalMode = 'SelfDrive'
        AND a.AmenityId = t.AmenityId;

    PRINT N'Đã bổ sung mẫu độ tuổi ghế trên 5 xe cũ. Chạy lại không thêm trùng.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- BUNDLED VEHICLE PHOTOS
-- User-supplied exterior/interior images packaged in CRMS/Assets/Vehicles.
-- Match by plate, brand, model and year. Re-running does not duplicate images.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @photos TABLE(Plate varchar(20), Brand nvarchar(80), Model nvarchar(100), Year smallint, ImageUrl nvarchar(1000), SortOrder int);
 INSERT @photos VALUES
(N'65A-67890',N'Ford',N'Everest',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-02-exterior.jpg',0),
(N'DEMO-CRMS-007',N'Ford',N'Everest',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-02-exterior.jpg',0),
(N'65A-67890',N'Ford',N'Everest',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-02-interior.jpg',1),
(N'DEMO-CRMS-007',N'Ford',N'Everest',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-02-interior.jpg',1),
(N'DEMO-CRMS-021',N'BMW',N'320i',2023,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-24-exterior.jpg',0),
(N'DEMO-CRMS-021',N'BMW',N'320i',2023,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-24-interior.jpg',1),
(N'65A-12345',N'Toyota',N'Vios',2023,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-01-exterior.jpg',0),
(N'DEMO-CRMS-001',N'Toyota',N'Vios',2023,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-01-exterior.jpg',0),
(N'65A-12345',N'Toyota',N'Vios',2023,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-01-interior.jpg',1),
(N'DEMO-CRMS-001',N'Toyota',N'Vios',2023,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-01-interior.jpg',1),
(N'65A-24680',N'Toyota',N'Innova',2022,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-03-exterior.png',0),
(N'DEMO-CRMS-012',N'Toyota',N'Innova',2022,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-03-exterior.png',0),
(N'65A-24680',N'Toyota',N'Innova',2022,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-03-interior.jpg',1),
(N'DEMO-CRMS-012',N'Toyota',N'Innova',2022,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-03-interior.jpg',1),
(N'DEMO-CRMS-002',N'Honda',N'City',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-05-exterior.jpg',0),
(N'DEMO-CRMS-002',N'Honda',N'City',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-05-interior.jpg',1),
(N'DEMO-CRMS-003',N'Hyundai',N'Accent',2022,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-06-exterior.jpg',0),
(N'DEMO-CRMS-003',N'Hyundai',N'Accent',2022,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-06-interior.jpg',1),
(N'DEMO-CRMS-004',N'Mazda',N'Mazda 3',2023,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-07-exterior.png',0),
(N'DEMO-CRMS-004',N'Mazda',N'Mazda 3',2023,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-07-interior.jpg',1),
(N'DEMO-CRMS-005',N'Kia',N'K3',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-08-exterior.jpg',0),
(N'DEMO-CRMS-005',N'Kia',N'K3',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-08-interior.jpg',1),
(N'DEMO-CRMS-006',N'Mazda',N'CX-5',2023,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-09-exterior.jpg',0),
(N'DEMO-CRMS-006',N'Mazda',N'CX-5',2023,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-09-interior.jpg',1),
(N'DEMO-CRMS-008',N'Hyundai',N'Santa Fe',2023,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-11-exterior.jpg',0),
(N'DEMO-CRMS-008',N'Hyundai',N'Santa Fe',2023,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-11-interior.jpg',1),
(N'DEMO-CRMS-009',N'Toyota',N'Fortuner',2022,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-12-exterior.jpg',0),
(N'DEMO-CRMS-009',N'Toyota',N'Fortuner',2022,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-12-interior.jpg',1),
(N'DEMO-CRMS-010',N'Kia',N'Seltos',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-13-exterior.jpg',0),
(N'DEMO-CRMS-010',N'Kia',N'Seltos',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-13-interior.jpg',1),
(N'DEMO-CRMS-011',N'Mitsubishi',N'Xpander',2023,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-14-exterior.jpg',0),
(N'DEMO-CRMS-011',N'Mitsubishi',N'Xpander',2023,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-14-interior.jpg',1),
(N'DEMO-CRMS-013',N'Toyota',N'Veloz Cross',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-16-exterior.jpg',0),
(N'DEMO-CRMS-013',N'Toyota',N'Veloz Cross',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-16-interior.jpg',1),
(N'DEMO-CRMS-014',N'Kia',N'Carnival',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-17-exterior.jpg',0),
(N'DEMO-CRMS-014',N'Kia',N'Carnival',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-17-interior.jpg',1),
(N'DEMO-CRMS-015',N'Hyundai',N'Grand i10',2022,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-18-exterior.jpg',0),
(N'DEMO-CRMS-015',N'Hyundai',N'Grand i10',2022,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-18-interior.jpg',1),
(N'DEMO-CRMS-016',N'Toyota',N'Wigo',2023,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-19-exterior.jpg',0),
(N'DEMO-CRMS-016',N'Toyota',N'Wigo',2023,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-19-interior.jpg',1),
(N'DEMO-CRMS-017',N'Suzuki',N'Swift',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-20-exterior.jpg',0),
(N'DEMO-CRMS-017',N'Suzuki',N'Swift',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-20-interior.jpg',1),
(N'DEMO-CRMS-018',N'Ford',N'Ranger',2023,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-21-exterior.jpg',0),
(N'DEMO-CRMS-018',N'Ford',N'Ranger',2023,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-21-interior.jpg',1),
(N'DEMO-CRMS-019',N'Mitsubishi',N'Triton',2022,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-22-exterior.jpg',0),
(N'DEMO-CRMS-019',N'Mitsubishi',N'Triton',2022,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-22-interior.jpg',1),
(N'DEMO-CRMS-020',N'Mercedes-Benz',N'C 200',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-23-exterior.jpg',0),
(N'DEMO-CRMS-020',N'Mercedes-Benz',N'C 200',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-23-interior.jpg',1),
(N'DEMO-CRMS-022',N'Lexus',N'ES 250',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-25-exterior.jpg',0),
(N'DEMO-CRMS-022',N'Lexus',N'ES 250',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-25-interior.jpg',1),
(N'DEMO-CRMS-023',N'VinFast',N'VF 5',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-26-exterior.jpg',0),
(N'DEMO-CRMS-023',N'VinFast',N'VF 5',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-26-interior.jpg',1),
(N'DEMO-CRMS-024',N'VinFast',N'VF 8',2025,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-27-exterior.jpg',0),
(N'DEMO-CRMS-024',N'VinFast',N'VF 8',2025,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-27-interior.jpg',1),
(N'DEMO-CRMS-025',N'VinFast',N'VF 9',2025,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-28-exterior.jpg',0),
(N'DEMO-CRMS-025',N'VinFast',N'VF 9',2025,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-28-interior.jpg',1),
(N'DEMO-CRMS-026',N'Toyota',N'Camry Hybrid',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-29-exterior.jpg',0),
(N'DEMO-CRMS-026',N'Toyota',N'Camry Hybrid',2024,N'pack://application:,,,/CRMS;component/Assets/Vehicles/vehicle-29-interior.jpg',1);
 IF EXISTS(SELECT 1 FROM @photos p WHERE NOT EXISTS(
  SELECT 1 FROM dbo.Vehicles v WHERE v.LicensePlate=p.Plate AND v.Brand=p.Brand AND v.Model=p.Model AND v.ManufactureYear=p.Year))
  THROW 54001,'Photo import stopped: missing vehicle or mismatched brand/model/year. No changes saved.',1;

 -- Lock the affected images until this entire import commits.
 SELECT ImageId INTO #existing FROM dbo.VehicleImages WITH(UPDLOCK,HOLDLOCK)
 WHERE VehicleId IN(SELECT v.VehicleId FROM dbo.Vehicles v JOIN @photos p ON p.Plate=v.LicensePlate);
 UPDATE dbo.VehicleImages SET IsPrimary=0
 WHERE VehicleId IN(SELECT v.VehicleId FROM dbo.Vehicles v JOIN @photos p ON p.Plate=v.LicensePlate) AND IsPrimary=1;
 DELETE im FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON v.VehicleId=im.VehicleId
 WHERE EXISTS(SELECT 1 FROM @photos p WHERE p.Plate=v.LicensePlate)
   AND im.ImageUrl LIKE N'https://placehold.co/%';

 INSERT dbo.VehicleImages(VehicleId,ImageUrl,IsPrimary,SortOrder)
 SELECT v.VehicleId,p.ImageUrl,0,p.SortOrder FROM @photos p JOIN dbo.Vehicles v ON v.LicensePlate=p.Plate
 WHERE NOT EXISTS(SELECT 1 FROM dbo.VehicleImages im WHERE im.VehicleId=v.VehicleId AND im.ImageUrl=p.ImageUrl);

 UPDATE im SET IsPrimary=CASE WHEN p.SortOrder=0 THEN 1 ELSE 0 END, SortOrder=p.SortOrder
 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON v.VehicleId=im.VehicleId
 JOIN @photos p ON p.Plate=v.LicensePlate AND p.ImageUrl=im.ImageUrl;
 DROP TABLE #existing;
 COMMIT;
 SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
 SELECT COUNT(*) AS ImportedPhotos,COUNT(DISTINCT v.VehicleId) AS Vehicles,
 SUM(CASE WHEN im.IsPrimary=1 THEN 1 ELSE 0 END) AS PrimaryPhotos
 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON v.VehicleId=im.VehicleId
 JOIN @photos p ON p.Plate=v.LicensePlate AND p.ImageUrl=im.ImageUrl;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
 THROW;
END CATCH;
GO
