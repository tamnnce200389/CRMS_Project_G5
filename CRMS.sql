/*
  PRN212 - CAR RENTAL MANAGEMENT SYSTEM - ORIGINAL ERD + RICH SEED
  SQL SERVER 2019+ | 15 TABLES | 10 STORED PROCEDURES | ONE DATABASE

  This is based on the ORIGINAL 15-table schema and the corrected
  sp_getapplock procedures. No modules or columns have been added.

  Non-destructive setup:
  * Creates database CRMS_PRN212 ONLY if missing.
  * Creates missing tables and indexes; existing data is untouched.
  * Updates procedure definitions with CREATE OR ALTER.
  * INSERTs missing demo records ONLY; no deletes/resets.
  * Does NOT migrate existing table definitions. Use ALTER/Migrations later.

  Demo accounts (LOCAL DEV ONLY):
    admin.demo@crms.example.com     Admin@12345!
    staff01@crms.example.com        Staff@12345!
    customer01@crms.example.com     Customer@12345!
  Staff02 and customer02..customer05 use their respective role's password.
  PasswordHash is a valid ASP.NET Core PasswordHasher V3 hash, not plaintext.
  Change/remove demo accounts before any non-demo deployment.

  Images use placeholder URLs. Real vehicle photos must be provided by the team.
  NO Bookings or Payments are seeded: create them through real workflows.
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
-- ================================================================
-- 01. TABLES / INDEXES (original schema)
-- 02. STORED PROCEDURES (original logic, syntax fixed)
-- ================================================================
-- 1. USERS: Customer, Staff, Admin. Guest is an unauthenticated visitor.
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users (
        UserId              BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        FullName            NVARCHAR(120) NOT NULL,
        Email               NVARCHAR(256) NOT NULL,
        Phone               VARCHAR(20) NULL,
        PasswordHash        NVARCHAR(500) NOT NULL, -- Use ASP.NET PasswordHasher, never plaintext
        UserRole            VARCHAR(10) NOT NULL CONSTRAINT DF_Users_UserRole DEFAULT ('Customer'),
        IsActive            BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
        CreatedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt           DATETIME2(0) NULL,
        RowVersion          ROWVERSION NOT NULL,
        CONSTRAINT CK_Users_Role CHECK (UserRole IN ('Customer','Staff','Admin'))
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'UX_Users_Email')
BEGIN
    CREATE UNIQUE INDEX UX_Users_Email ON dbo.Users(Email);
END;
GO


-- 2. VEHICLE CATALOG
IF OBJECT_ID(N'dbo.VehicleCategories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VehicleCategories (
        CategoryId          INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CategoryName        NVARCHAR(80) NOT NULL,
        Description         NVARCHAR(500) NULL,
        IsActive            BIT NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT (1)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.VehicleCategories') AND name = N'UX_Categories_Name')
BEGIN
    CREATE UNIQUE INDEX UX_Categories_Name ON dbo.VehicleCategories(CategoryName);
END;
GO

IF OBJECT_ID(N'dbo.Vehicles', N'U') IS NULL
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
        OperationalStatus   VARCHAR(12) NOT NULL CONSTRAINT DF_Vehicles_Status DEFAULT ('Active'),
        Description         NVARCHAR(2000) NULL,
        CreatedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_Vehicles_CreatedAt DEFAULT SYSUTCDATETIME(),
        RowVersion          ROWVERSION NOT NULL,
        CONSTRAINT FK_Vehicles_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.VehicleCategories(CategoryId),
        CONSTRAINT CK_Vehicles_Year CHECK (ManufactureYear BETWEEN 1990 AND 2100),
        CONSTRAINT CK_Vehicles_Seats CHECK (SeatCount BETWEEN 1 AND 30),
        CONSTRAINT CK_Vehicles_Transmission CHECK (Transmission IN ('Automatic','Manual')),
        CONSTRAINT CK_Vehicles_Fuel CHECK (FuelType IN ('Gasoline','Diesel','Electric','Hybrid')),
        CONSTRAINT CK_Vehicles_Price CHECK (DailyRate > 0),
        CONSTRAINT CK_Vehicles_Odometer CHECK (OdometerKm >= 0),
        CONSTRAINT CK_Vehicles_Status CHECK (OperationalStatus IN ('Active','Suspended'))
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Vehicles') AND name = N'UX_Vehicles_Plate')
BEGIN
    CREATE UNIQUE INDEX UX_Vehicles_Plate ON dbo.Vehicles(LicensePlate);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Vehicles') AND name = N'IX_Vehicles_Filter')
BEGIN
    CREATE INDEX IX_Vehicles_Filter ON dbo.Vehicles(CategoryId, OperationalStatus, DailyRate);
END;
GO

-- Gia thue dai han theo goi 1/3/6/9/12 thang
IF OBJECT_ID(N'dbo.VehicleLongTermRates', N'U') IS NULL
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
END;
GO

IF OBJECT_ID(N'dbo.VehicleImages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VehicleImages (
        ImageId             BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        VehicleId           BIGINT NOT NULL,
        ImageUrl            NVARCHAR(1000) NOT NULL,
        IsPrimary           BIT NOT NULL CONSTRAINT DF_Images_IsPrimary DEFAULT (0),
        SortOrder           INT NOT NULL CONSTRAINT DF_Images_SortOrder DEFAULT (0),
        CONSTRAINT FK_Images_Vehicle FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles(VehicleId) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.VehicleImages') AND name = N'IX_Images_Vehicle')
BEGIN
    CREATE INDEX IX_Images_Vehicle ON dbo.VehicleImages(VehicleId, SortOrder);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.VehicleImages') AND name = N'UX_Images_OnePrimary')
BEGIN
    CREATE UNIQUE INDEX UX_Images_OnePrimary ON dbo.VehicleImages(VehicleId) WHERE IsPrimary = 1;
END;
GO


-- 3. DRIVERS AND RENTAL POLICY
IF OBJECT_ID(N'dbo.Drivers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Drivers (
        DriverId            BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        FullName            NVARCHAR(120) NOT NULL,
        Phone               VARCHAR(20) NULL,
        LicenseNumber       NVARCHAR(40) NULL,
        IsActive            BIT NOT NULL CONSTRAINT DF_Drivers_IsActive DEFAULT (1),
        Notes               NVARCHAR(1000) NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Drivers') AND name = N'UX_Drivers_LicenseNumber')
BEGIN
    CREATE UNIQUE INDEX UX_Drivers_LicenseNumber ON dbo.Drivers(LicenseNumber) WHERE LicenseNumber IS NOT NULL;
END;
GO

IF OBJECT_ID(N'dbo.RentalPolicies', N'U') IS NULL
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
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.RentalPolicies WHERE PolicyId = 1)
    INSERT INTO dbo.RentalPolicies(PolicyId) VALUES (1);
GO


-- 4. VOUCHERS
IF OBJECT_ID(N'dbo.Vouchers', N'U') IS NULL
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
        CONSTRAINT CK_Voucher_Type CHECK (DiscountType IN ('Percent','Fixed')),
        CONSTRAINT CK_Voucher_Amount CHECK (DiscountValue > 0 AND (DiscountType <> 'Percent' OR DiscountValue <= 100)),
        CONSTRAINT CK_Voucher_Max CHECK (MaxDiscountAmount IS NULL OR MaxDiscountAmount > 0),
        CONSTRAINT CK_Voucher_Min CHECK (MinimumOrderAmount >= 0),
        CONSTRAINT CK_Voucher_Dates CHECK (EndsAt > StartsAt),
        CONSTRAINT CK_Voucher_Limits CHECK ((UsageLimit IS NULL OR UsageLimit > 0) AND PerCustomerLimit > 0)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Vouchers') AND name = N'UX_Vouchers_Code')
BEGIN
    CREATE UNIQUE INDEX UX_Vouchers_Code ON dbo.Vouchers(VoucherCode);
END;
GO


-- 5. BOOKINGS: prices are snapshotted to preserve history when catalog prices change.
-- Date/time convention: UTC; rental interval [StartAt, EndAt) - EndAt is exclusive.
IF OBJECT_ID(N'dbo.Bookings', N'U') IS NULL
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
        BookingStatus         VARCHAR(16) NOT NULL CONSTRAINT DF_Booking_Status DEFAULT ('PendingDeposit'),
        ConfirmedAt           DATETIME2(0) NULL,
        CancelledAt           DATETIME2(0) NULL,
        CancelReason          NVARCHAR(500) NULL,
        CreatedAt             DATETIME2(0) NOT NULL CONSTRAINT DF_Booking_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt             DATETIME2(0) NULL,
        RowVersion            ROWVERSION NOT NULL,
        CONSTRAINT FK_Bookings_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_Bookings_Vehicle FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles(VehicleId),
        CONSTRAINT FK_Bookings_Driver FOREIGN KEY (DriverId) REFERENCES dbo.Drivers(DriverId),
        CONSTRAINT CK_Bookings_RentalType CHECK (RentalType IN ('SelfDrive','WithDriver')),
        CONSTRAINT CK_Bookings_DepositMethod CHECK (DepositMethod IN ('Cash','VNPay')),
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
            (RentalType = 'SelfDrive' AND DriverId IS NULL AND DriverDailyRate = 0 AND DriverAmount = 0)
            OR RentalType = 'WithDriver'
        ),
        CONSTRAINT CK_Bookings_Status CHECK (
            BookingStatus IN ('PendingDeposit','Confirmed','InUse','Returned','Completed','Cancelled','Expired')
        )
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Bookings') AND name = N'UX_Bookings_Code')
BEGIN
    CREATE UNIQUE INDEX UX_Bookings_Code ON dbo.Bookings(BookingCode);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Bookings') AND name = N'UX_Bookings_RequestKey')
BEGIN
    CREATE UNIQUE INDEX UX_Bookings_RequestKey ON dbo.Bookings(RequestKey);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Bookings') AND name = N'IX_Bookings_VehicleRange')
BEGIN
    CREATE INDEX IX_Bookings_VehicleRange ON dbo.Bookings(VehicleId, StartAt, EndAt) INCLUDE (BookingStatus, DepositDeadline);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Bookings') AND name = N'IX_Bookings_Customer')
BEGIN
    CREATE INDEX IX_Bookings_Customer ON dbo.Bookings(CustomerId, CreatedAt DESC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Bookings') AND name = N'IX_Bookings_DriverRange')
BEGIN
    CREATE INDEX IX_Bookings_DriverRange ON dbo.Bookings(DriverId, StartAt, EndAt) WHERE DriverId IS NOT NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Bookings') AND name = N'IX_Bookings_PendingExpiry')
BEGIN
    CREATE INDEX IX_Bookings_PendingExpiry ON dbo.Bookings(DepositDeadline) WHERE BookingStatus = 'PendingDeposit';
END;
GO

IF OBJECT_ID(N'dbo.BookingVouchers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BookingVouchers (
        BookingId             BIGINT NOT NULL PRIMARY KEY,
        VoucherId             BIGINT NOT NULL,
        DiscountSnapshot      DECIMAL(18,2) NOT NULL,
        RedemptionStatus      VARCHAR(10) NOT NULL CONSTRAINT DF_BookingVoucher_Status DEFAULT ('Reserved'),
        CreatedAt             DATETIME2(0) NOT NULL CONSTRAINT DF_BookingVoucher_Created DEFAULT SYSUTCDATETIME(),
        AppliedAt             DATETIME2(0) NULL,
        CONSTRAINT FK_BookingVoucher_Booking FOREIGN KEY (BookingId) REFERENCES dbo.Bookings(BookingId),
        CONSTRAINT FK_BookingVoucher_Voucher FOREIGN KEY (VoucherId) REFERENCES dbo.Vouchers(VoucherId),
        CONSTRAINT CK_BookingVoucher_Discount CHECK (DiscountSnapshot >= 0),
        CONSTRAINT CK_BookingVoucher_Status CHECK (RedemptionStatus IN ('Reserved','Applied','Released'))
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BookingVouchers') AND name = N'IX_BookingVoucher_Usage')
BEGIN
    CREATE INDEX IX_BookingVoucher_Usage ON dbo.BookingVouchers(VoucherId, RedemptionStatus);
END;
GO


-- 6. PAYMENT HISTORY. Refund is a positive amount with PaymentType='Refund'.
IF OBJECT_ID(N'dbo.Payments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Payments (
        PaymentId             BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        BookingId             BIGINT NOT NULL,
        PaymentType           VARCHAR(10) NOT NULL,
        PaymentMethod         VARCHAR(12) NOT NULL,
        Amount                DECIMAL(18,2) NOT NULL,
        PaymentStatus         VARCHAR(10) NOT NULL CONSTRAINT DF_Payments_Status DEFAULT ('Pending'),
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
        CONSTRAINT CK_Payments_Type CHECK (PaymentType IN ('Deposit','Balance','Refund')),
        CONSTRAINT CK_Payments_Method CHECK (PaymentMethod IN ('Cash','VNPay','BankTransfer')),
        CONSTRAINT CK_Payments_Status CHECK (PaymentStatus IN ('Pending','Success','Failed','Cancelled')),
        CONSTRAINT CK_Payments_Amount CHECK (Amount > 0),
        CONSTRAINT CK_Payments_GatewayRef CHECK (PaymentMethod <> 'VNPay' OR GatewayTxnRef IS NOT NULL),
        CONSTRAINT CK_Payments_BankTransfer CHECK (PaymentType = 'Refund' OR PaymentMethod <> 'BankTransfer'),
        CONSTRAINT CK_Payments_SuccessPaidAt CHECK (PaymentStatus <> 'Success' OR PaidAt IS NOT NULL)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Payments') AND name = N'IX_Payments_Booking')
BEGIN
    CREATE INDEX IX_Payments_Booking ON dbo.Payments(BookingId, CreatedAt DESC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Payments') AND name = N'UX_Payments_GatewayRef')
BEGIN
    CREATE UNIQUE INDEX UX_Payments_GatewayRef ON dbo.Payments(GatewayTxnRef) WHERE GatewayTxnRef IS NOT NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Payments') AND name = N'UX_Payments_GatewayTxnNo')
BEGIN
    CREATE UNIQUE INDEX UX_Payments_GatewayTxnNo ON dbo.Payments(GatewayTransactionNo) WHERE GatewayTransactionNo IS NOT NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Payments') AND name = N'UX_Payments_Idempotency')
BEGIN
    CREATE UNIQUE INDEX UX_Payments_Idempotency ON dbo.Payments(IdempotencyKey) WHERE IdempotencyKey IS NOT NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Payments') AND name = N'UX_Payments_OneDepositSuccess')
BEGIN
    CREATE UNIQUE INDEX UX_Payments_OneDepositSuccess ON dbo.Payments(BookingId)
        WHERE PaymentType = 'Deposit' AND PaymentStatus = 'Success';
END;
GO


-- 7. PICKUP, RETURN, AND MAINTENANCE
IF OBJECT_ID(N'dbo.VehicleHandovers', N'U') IS NULL
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
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.VehicleHandovers') AND name = N'UX_Handover_Booking')
BEGIN
    CREATE UNIQUE INDEX UX_Handover_Booking ON dbo.VehicleHandovers(BookingId);
END;
GO

IF OBJECT_ID(N'dbo.VehicleReturns', N'U') IS NULL
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
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.VehicleReturns') AND name = N'UX_Returns_Booking')
BEGIN
    CREATE UNIQUE INDEX UX_Returns_Booking ON dbo.VehicleReturns(BookingId);
END;
GO

IF OBJECT_ID(N'dbo.MaintenanceRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MaintenanceRecords (
        MaintenanceId           BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        VehicleId               BIGINT NOT NULL,
        CreatedByUserId         BIGINT NOT NULL,
        StartAt                 DATETIME2(0) NOT NULL,
        EndAt                   DATETIME2(0) NOT NULL,
        MaintenanceStatus       VARCHAR(12) NOT NULL CONSTRAINT DF_Maintenance_Status DEFAULT ('Scheduled'),
        Description             NVARCHAR(1500) NOT NULL,
        Cost                    DECIMAL(18,2) NOT NULL CONSTRAINT DF_Maintenance_Cost DEFAULT (0),
        CONSTRAINT FK_Maintenance_Vehicle FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles(VehicleId),
        CONSTRAINT FK_Maintenance_User FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_Maintenance_Time CHECK (EndAt > StartAt),
        CONSTRAINT CK_Maintenance_Cost CHECK (Cost >= 0),
        CONSTRAINT CK_Maintenance_Status CHECK (MaintenanceStatus IN ('Scheduled','InProgress','Completed','Cancelled'))
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MaintenanceRecords') AND name = N'IX_Maintenance_VehicleRange')
BEGIN
    CREATE INDEX IX_Maintenance_VehicleRange ON dbo.MaintenanceRecords(VehicleId, StartAt, EndAt);
END;
GO


-- 8. REVIEWS AND BOOKING AUDIT
IF OBJECT_ID(N'dbo.Reviews', N'U') IS NULL
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
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Reviews') AND name = N'UX_Reviews_Booking')
BEGIN
    CREATE UNIQUE INDEX UX_Reviews_Booking ON dbo.Reviews(BookingId);
END;
GO

IF OBJECT_ID(N'dbo.BookingStatusHistory', N'U') IS NULL
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
        CONSTRAINT CK_History_Old CHECK (OldStatus IS NULL OR OldStatus IN ('PendingDeposit','Confirmed','InUse','Returned','Completed','Cancelled','Expired')),
        CONSTRAINT CK_History_New CHECK (NewStatus IN ('PendingDeposit','Confirmed','InUse','Returned','Completed','Cancelled','Expired'))
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BookingStatusHistory') AND name = N'IX_History_BookingTime')
BEGIN
    CREATE INDEX IX_History_BookingTime ON dbo.BookingStatusHistory(BookingId, ChangedAt DESC);
END;
GO

-- 9. ATOMIC BOOKING CREATION: sp_getapplock prevents simultaneous bookings for one vehicle.
-- The same lock convention must be used by maintenance scheduling and booking date changes.
-- A reservation blocks availability while PendingDeposit AND DepositDeadline > NOW.
CREATE OR ALTER PROCEDURE dbo.usp_CreateBooking
    @CustomerId BIGINT,
    @VehicleId BIGINT,
    @RentalType VARCHAR(12),
    @DepositMethod VARCHAR(5),
    @StartAt DATETIME2(0),
    @EndAt DATETIME2(0),
    @RequestKey UNIQUEIDENTIFIER,
    @NewBookingId BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Now DATETIME2(0) = SYSUTCDATETIME();
    DECLARE @VehicleRate DECIMAL(18,2), @DriverRate DECIMAL(18,2), @DepositPercent DECIMAL(5,2);
    DECLARE @Days INT, @CarAmount DECIMAL(18,2), @DriverAmount DECIMAL(18,2);
    DECLARE @Total DECIMAL(18,2), @Deposit DECIMAL(18,2), @Deadline DATETIME2(0);
    DECLARE @OnlineHold INT, @CashHold INT, @LockResult INT, @ExistingId BIGINT;
    DECLARE @LockResource NVARCHAR(255);

    IF @StartAt <= @Now OR @EndAt <= @StartAt OR @RentalType NOT IN ('SelfDrive','WithDriver')
        OR @DepositMethod NOT IN ('Cash','VNPay')
        THROW 51000, 'Invalid rental dates or rental type/payment method.', 1;

    BEGIN TRY
      BEGIN TRANSACTION;
      -- The request key is stable across client retries; return the existing booking if present.
      SELECT @ExistingId = BookingId FROM dbo.Bookings WITH (UPDLOCK, HOLDLOCK) WHERE RequestKey = @RequestKey AND CustomerId = @CustomerId
        AND VehicleId = @VehicleId AND StartAt = @StartAt AND EndAt = @EndAt
        AND RentalType = @RentalType AND DepositMethod = @DepositMethod;
      IF @ExistingId IS NOT NULL
      BEGIN
          SET @NewBookingId = @ExistingId;
          COMMIT TRANSACTION;
          RETURN;
      END;
      IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = @CustomerId AND UserRole = 'Customer' AND IsActive = 1)
          THROW 51001, 'Customer is not active.', 1;
      SET @LockResource = CONCAT(N'CRMS_VEHICLE_', @VehicleId);
      EXEC @LockResult = sys.sp_getapplock
          @Resource = @LockResource,
          @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
      IF @LockResult < 0 THROW 51003, 'Could not acquire vehicle reservation lock.', 1;
      SELECT @VehicleRate = DailyRate FROM dbo.Vehicles WITH (UPDLOCK, HOLDLOCK)
          WHERE VehicleId = @VehicleId AND OperationalStatus = 'Active';
      IF @VehicleRate IS NULL THROW 51002, 'Vehicle is not rentable.', 1;

      IF EXISTS (
          SELECT 1 FROM dbo.Bookings WITH (UPDLOCK, HOLDLOCK)
          WHERE VehicleId = @VehicleId
            AND ((BookingStatus = 'InUse' AND StartAt < @EndAt) -- late return still occupies the vehicle
              OR (BookingStatus = 'Confirmed' AND StartAt < @EndAt AND EndAt > @StartAt)
              OR (BookingStatus = 'PendingDeposit' AND DepositDeadline > @Now AND StartAt < @EndAt AND EndAt > @StartAt))
      ) THROW 51004, 'Vehicle already booked during requested dates.', 1;

      IF EXISTS (
          SELECT 1 FROM dbo.MaintenanceRecords WITH (UPDLOCK, HOLDLOCK)
          WHERE VehicleId = @VehicleId AND StartAt < @EndAt AND EndAt > @StartAt
            AND MaintenanceStatus IN ('Scheduled','InProgress')
      ) THROW 51005, 'Vehicle is unavailable due to maintenance.', 1;

      SELECT @DepositPercent = DepositPercent, @DriverRate = DriverDailyFee,
             @OnlineHold = OnlineHoldMinutes, @CashHold = CashHoldHours
        FROM dbo.RentalPolicies WHERE PolicyId = 1;
      SET @Days = (DATEDIFF_BIG(SECOND, @StartAt, @EndAt) + 86399) / 86400;
      IF @Days <= 0 OR @Days > 365 THROW 51006, 'Rental period must be between 1 and 365 days.', 1;
      IF @RentalType = 'SelfDrive' SET @DriverRate = 0;
      SET @CarAmount = @VehicleRate * @Days;
      SET @DriverAmount = @DriverRate * @Days;
      SET @Total = @CarAmount + @DriverAmount;
      SET @Deposit = ROUND(@Total * @DepositPercent / 100.0, 2);
      SET @Deadline = CASE WHEN @DepositMethod = 'VNPay'
          THEN DATEADD(MINUTE, @OnlineHold, @Now)
          ELSE DATEADD(HOUR, @CashHold, @Now) END;

      INSERT INTO dbo.Bookings
        (BookingCode, RequestKey, CustomerId, VehicleId, RentalType, DepositMethod,
         StartAt, EndAt, RentalDays, VehicleDailyRate, DriverDailyRate, VehicleAmount,
         DriverAmount, DiscountAmount, TotalAmount, DepositAmount, DepositDeadline)
      VALUES
        (CONCAT('CR-', CONVERT(VARCHAR(8), @Now, 112), '-', LEFT(REPLACE(CONVERT(VARCHAR(36), NEWID()), '-', ''), 12)),
         @RequestKey, @CustomerId, @VehicleId, @RentalType, @DepositMethod,
         @StartAt, @EndAt, @Days, @VehicleRate, @DriverRate, @CarAmount,
         @DriverAmount, 0, @Total, @Deposit, @Deadline);
      SET @NewBookingId = SCOPE_IDENTITY();
      INSERT INTO dbo.BookingStatusHistory(BookingId, NewStatus, ChangeNote)
      VALUES (@NewBookingId, 'PendingDeposit', N'Reservation created');
      COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
      IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
      THROW;
    END CATCH
END;
GO

/*
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
    BEGIN TRY
      BEGIN TRANSACTION;
      SELECT @Gross = VehicleAmount + DriverAmount
      FROM dbo.Bookings WITH (UPDLOCK, HOLDLOCK)
      WHERE BookingId = @BookingId AND CustomerId = @CustomerId
        AND BookingStatus = 'PendingDeposit' AND DepositDeadline > @Now;
      IF @Gross IS NULL THROW 51100, 'Booking is not eligible for a voucher.', 1;
      -- Price is frozen once any payment attempt exists.
      IF EXISTS (SELECT 1 FROM dbo.Payments WITH (UPDLOCK, HOLDLOCK) WHERE BookingId = @BookingId)
        THROW 51101, 'Voucher must be applied before initiating payment.', 1;
      SELECT @ExistingVoucherId = VoucherId FROM dbo.BookingVouchers WHERE BookingId = @BookingId;
      IF @ExistingVoucherId IS NOT NULL THROW 51102, 'Booking already has a voucher.', 1;
      SELECT @VoucherId = VoucherId, @Type = DiscountType, @Value = DiscountValue,
             @Max = MaxDiscountAmount, @Minimum = MinimumOrderAmount,
             @GlobalLimit = UsageLimit, @PerCustomerLimit = PerCustomerLimit
      FROM dbo.Vouchers WITH (UPDLOCK, HOLDLOCK)
      WHERE VoucherCode = @VoucherCode AND IsActive = 1 AND StartsAt <= @Now AND EndsAt > @Now;
      IF @VoucherId IS NULL THROW 51103, 'Voucher is invalid or expired.', 1;
      IF @Gross < @Minimum THROW 51104, 'Minimum order amount not reached.', 1;
      SELECT @ActiveCount = COUNT_BIG(*) FROM dbo.BookingVouchers
      WHERE VoucherId = @VoucherId AND RedemptionStatus IN ('Reserved','Applied');
      SELECT @CustomerCount = COUNT_BIG(*) FROM dbo.BookingVouchers bv
      JOIN dbo.Bookings b ON b.BookingId = bv.BookingId
      WHERE bv.VoucherId = @VoucherId AND b.CustomerId = @CustomerId
        AND bv.RedemptionStatus IN ('Reserved','Applied');
      IF (@GlobalLimit IS NOT NULL AND @ActiveCount >= @GlobalLimit)
         OR @CustomerCount >= @PerCustomerLimit
          THROW 51105, 'Voucher usage quota exceeded.', 1;
      SET @DiscountAmount = CASE WHEN @Type = 'Percent'
          THEN ROUND(@Gross * @Value / 100, 2) ELSE @Value END;
      IF @Max IS NOT NULL AND @DiscountAmount > @Max SET @DiscountAmount = @Max;
      -- Require a positive post-discount amount for the mandatory 50% deposit.
      IF @DiscountAmount >= @Gross THROW 51106, 'Voucher cannot make a rental free.', 1;
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
END;
GO

-- 11. CONFIRM DEPOSIT (after server-side VNPay HMAC and amount validation, or Staff cash receipt).
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
      WHERE PaymentId = @PaymentId AND PaymentType = 'Deposit';
      IF @BookingId IS NULL THROW 51200, 'Deposit payment does not exist.', 1;
      SELECT @Required = DepositAmount, @BookingStatus = BookingStatus, @Deadline = DepositDeadline
      FROM dbo.Bookings WITH (UPDLOCK, HOLDLOCK) WHERE BookingId = @BookingId;
      IF @PaymentStatus = 'Success'
      BEGIN
          IF @BookingStatus IN ('Expired','Cancelled') SET @NeedsManualRefund = 1;
          COMMIT TRANSACTION;
          RETURN;
      END;
      IF @PaymentStatus <> 'Pending' THROW 51201, 'Payment is not pending.', 1;
      IF @PayAmount <> @Required THROW 51202, 'Paid amount differs from deposit amount.', 1;
      IF @Method = 'Cash'
      BEGIN
          IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = @VerifiedByUserId
                AND UserRole IN ('Staff','Admin') AND IsActive = 1)
              THROW 51203, 'Cash deposit requires an authorized Staff/Admin confirmation.', 1;
      END
      ELSE IF @Method = 'VNPay'
      BEGIN
          IF @GatewayTransactionNo IS NULL OR @GatewayResponseCode <> '00'
              THROW 51204, 'VNPay success details missing/invalid.', 1;
          -- WARNING: HMAC and payment reference MUST already be validated by backend.
      END
      ELSE THROW 51205, 'Invalid deposit payment method.', 1;

      UPDATE dbo.Payments SET PaymentStatus = 'Success', PaidAt = @Now,
          VerifiedByUserId = @VerifiedByUserId,
          GatewayTransactionNo = CASE WHEN @Method = 'VNPay' THEN @GatewayTransactionNo ELSE GatewayTransactionNo END,
          GatewayResponseCode = CASE WHEN @Method = 'VNPay' THEN @GatewayResponseCode ELSE GatewayResponseCode END
      WHERE PaymentId = @PaymentId;

      IF @BookingStatus = 'PendingDeposit' AND @Deadline > @Now
      BEGIN
          UPDATE dbo.Bookings SET BookingStatus = 'Confirmed', ConfirmedAt = @Now, UpdatedAt = @Now
          WHERE BookingId = @BookingId;
          UPDATE dbo.BookingVouchers SET RedemptionStatus = 'Applied', AppliedAt = @Now
          WHERE BookingId = @BookingId AND RedemptionStatus = 'Reserved';
          INSERT INTO dbo.BookingStatusHistory(BookingId, OldStatus, NewStatus, ChangedByUserId, ChangeNote)
          VALUES (@BookingId, 'PendingDeposit', 'Confirmed', @VerifiedByUserId, N'Deposit confirmed');
      END
      ELSE
      BEGIN
          SET @NeedsManualRefund = 1;
          IF @BookingStatus = 'PendingDeposit'
          BEGIN
              UPDATE dbo.Bookings SET BookingStatus='Expired', UpdatedAt=@Now WHERE BookingId=@BookingId;
              UPDATE dbo.BookingVouchers SET RedemptionStatus='Released'
              WHERE BookingId=@BookingId AND RedemptionStatus='Reserved';
              INSERT INTO dbo.BookingStatusHistory(BookingId, OldStatus, NewStatus, ChangeNote)
              VALUES (@BookingId, 'PendingDeposit', 'Expired', N'Deposit received after reservation deadline');
          END
      END;
      COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
      IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
      THROW;
    END CATCH
END;
GO

-- 12. RELEASE EXPIRED HOLDS (call from recurring background worker).
CREATE OR ALTER PROCEDURE dbo.usp_ExpirePendingBookings
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Expired TABLE(BookingId BIGINT PRIMARY KEY);
    BEGIN TRANSACTION;
    UPDATE dbo.Bookings SET BookingStatus = 'Expired', UpdatedAt = SYSUTCDATETIME()
      OUTPUT inserted.BookingId INTO @Expired(BookingId)
    WHERE BookingStatus = 'PendingDeposit' AND DepositDeadline <= SYSUTCDATETIME();
    UPDATE bv SET RedemptionStatus = 'Released'
    FROM dbo.BookingVouchers bv JOIN @Expired x ON x.BookingId = bv.BookingId
    WHERE bv.RedemptionStatus = 'Reserved';
    INSERT INTO dbo.BookingStatusHistory(BookingId, OldStatus, NewStatus, ChangeNote)
    SELECT BookingId, 'PendingDeposit', 'Expired', N'Deposit deadline elapsed'
    FROM @Expired;
    COMMIT TRANSACTION;
END;
GO

-- 13. DRIVER ASSIGNMENT: prevent two active bookings using the same driver at once.
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
                   AND UserRole IN ('Staff','Admin') AND IsActive = 1)
          THROW 51300, 'Only Staff/Admin may assign a driver.', 1;
      SET @LockResource = CONCAT(N'CRMS_DRIVER_', @DriverId);
      EXEC @LockResult = sys.sp_getapplock
          @Resource = @LockResource,
          @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
      IF @LockResult < 0 THROW 51301, 'Driver lock unavailable.', 1;
      SELECT @Start = StartAt, @End = EndAt, @CurrentDriver = DriverId
      FROM dbo.Bookings WITH (UPDLOCK, HOLDLOCK)
      WHERE BookingId = @BookingId AND BookingStatus = 'Confirmed' AND RentalType = 'WithDriver';
      IF @Start IS NULL THROW 51302, 'Booking does not need/allow driver assignment.', 1;
      IF NOT EXISTS (SELECT 1 FROM dbo.Drivers WHERE DriverId = @DriverId AND IsActive = 1)
          THROW 51303, 'Driver is inactive or missing.', 1;
      IF EXISTS (SELECT 1 FROM dbo.Bookings WITH (UPDLOCK, HOLDLOCK)
         WHERE DriverId = @DriverId AND BookingId <> @BookingId
         AND ((BookingStatus = 'Confirmed' AND StartAt < @End AND EndAt > @Start)
              OR (BookingStatus = 'InUse' AND StartAt < @End)))
          THROW 51304, 'Driver already assigned in this period.', 1;
      UPDATE dbo.Bookings SET DriverId = @DriverId, UpdatedAt = SYSUTCDATETIME()
      WHERE BookingId = @BookingId;
      COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
      IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
      THROW;
    END CATCH
END;
GO

-- 14. MAINTENANCE SCHEDULING: same per-vehicle lock as booking creation.
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
    IF @EndAt <= @StartAt OR @StartAt <= @Now THROW 51400, 'Invalid maintenance period.', 1;
    BEGIN TRY
      BEGIN TRANSACTION;
      IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = @CreatedByUserId
                     AND UserRole = 'Admin' AND IsActive = 1)
          THROW 51401, 'Only Admin may schedule maintenance.', 1;
      SET @LockResource = CONCAT(N'CRMS_VEHICLE_', @VehicleId);
      EXEC @LockResult = sys.sp_getapplock
          @Resource = @LockResource,
          @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
      IF @LockResult < 0 THROW 51402, 'Vehicle lock unavailable.', 1;
      IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE VehicleId = @VehicleId)
          THROW 51403, 'Vehicle not found.', 1;
      IF EXISTS (SELECT 1 FROM dbo.Bookings WITH (UPDLOCK, HOLDLOCK)
                WHERE VehicleId = @VehicleId
                  AND ((BookingStatus = 'InUse' AND StartAt < @EndAt)
                     OR (BookingStatus = 'Confirmed' AND StartAt < @EndAt AND EndAt > @StartAt)
                     OR (BookingStatus = 'PendingDeposit' AND DepositDeadline > @Now AND StartAt < @EndAt AND EndAt > @StartAt)))
          THROW 51404, 'Vehicle already booked for maintenance period.', 1;
      IF EXISTS (SELECT 1 FROM dbo.MaintenanceRecords WITH (UPDLOCK, HOLDLOCK)
                WHERE VehicleId = @VehicleId AND StartAt < @EndAt AND EndAt > @StartAt
                  AND MaintenanceStatus IN ('Scheduled','InProgress'))
          THROW 51405, 'Overlapping maintenance schedule.', 1;
      INSERT INTO dbo.MaintenanceRecords(VehicleId, CreatedByUserId, StartAt, EndAt, Description)
      VALUES (@VehicleId, @CreatedByUserId, @StartAt, @EndAt, @Description);
      SET @MaintenanceId = SCOPE_IDENTITY();
      COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
      IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
      THROW;
    END CATCH
END;
GO

-- 15. CANCELLATION: only before vehicle handover; manual refund is a separate action.
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
      IF @CustomerId IS NULL THROW 51500, 'Booking not found.', 1;
      IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId=@CancelledByUserId AND IsActive=1
          AND (UserId=@CustomerId OR UserRole IN ('Staff','Admin')))
          THROW 51501, 'Not authorized to cancel this booking.', 1;
      IF @OldStatus='Cancelled'
      BEGIN
          COMMIT TRANSACTION;
          RETURN;
      END;
      IF @OldStatus NOT IN ('PendingDeposit','Confirmed')
          THROW 51502, 'Only uncollected bookings can be cancelled.', 1;
      IF EXISTS (SELECT 1 FROM dbo.VehicleHandovers WHERE BookingId=@BookingId)
          THROW 51503, 'Vehicle has already been handed over.', 1;
      UPDATE dbo.Bookings SET BookingStatus='Cancelled', CancelledAt=@Now,
          CancelReason=@Reason, UpdatedAt=@Now WHERE BookingId=@BookingId;
      UPDATE dbo.BookingVouchers SET RedemptionStatus='Released' WHERE BookingId=@BookingId
          AND RedemptionStatus IN ('Reserved','Applied');
      INSERT INTO dbo.BookingStatusHistory(BookingId,OldStatus,NewStatus,ChangedByUserId,ChangeNote)
      VALUES (@BookingId,@OldStatus,'Cancelled',@CancelledByUserId,@Reason);
      -- A paid deposit does NOT automatically disappear; Staff must refund it.
      SELECT @Paid = COALESCE(SUM(CASE WHEN PaymentType='Refund' THEN -Amount ELSE Amount END),0)
      FROM dbo.Payments WHERE BookingId=@BookingId AND PaymentStatus='Success';
      IF @Paid>0 AND NOT EXISTS (SELECT 1 FROM dbo.Payments
                               WHERE BookingId=@BookingId AND PaymentType='Refund' AND PaymentStatus='Pending')
          INSERT INTO dbo.Payments(BookingId,PaymentType,PaymentMethod,Amount,Note)
          VALUES(@BookingId,'Refund','BankTransfer',@Paid,N'Manual refund pending after cancellation');
      COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
      IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
      THROW;
    END CATCH
END;
GO

-- 16. STAFF HANDOVER: booking must be confirmed; chauffeur requires an assigned driver.
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
                     AND UserRole IN ('Staff','Admin'))
          THROW 51600, 'Only Staff/Admin may hand over a vehicle.', 1;
      SELECT @Status=BookingStatus,@RentalType=RentalType,@DriverId=DriverId,@VehicleId=VehicleId
      FROM dbo.Bookings WITH (UPDLOCK, HOLDLOCK) WHERE BookingId=@BookingId;
      IF @Status <> 'Confirmed' OR @Status IS NULL
          THROW 51601, 'Booking is not confirmed.', 1;
      IF @RentalType='WithDriver' AND @DriverId IS NULL
          THROW 51602, 'Driver must be assigned before handover.', 1;
      IF EXISTS (SELECT 1 FROM dbo.VehicleHandovers WHERE BookingId=@BookingId)
          THROW 51603, 'Vehicle was already handed over.', 1;
      IF @OdometerOutKm < (SELECT OdometerKm FROM dbo.Vehicles WHERE VehicleId=@VehicleId)
          THROW 51604, 'Odometer must not decrease.', 1;
      INSERT INTO dbo.VehicleHandovers(BookingId,StaffId,OdometerOutKm,FuelPercentOut,ConditionNotes)
      VALUES(@BookingId,@StaffUserId,@OdometerOutKm,@FuelPercentOut,@ConditionNotes);
      UPDATE dbo.Bookings SET BookingStatus='InUse',UpdatedAt=SYSUTCDATETIME() WHERE BookingId=@BookingId;
      INSERT INTO dbo.BookingStatusHistory(BookingId,OldStatus,NewStatus,ChangedByUserId,ChangeNote)
      VALUES(@BookingId,'Confirmed','InUse',@StaffUserId,N'Vehicle handover completed');
      COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
      IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
      THROW;
    END CATCH
END;
GO

-- 17. STAFF RETURN: separate vehicle-returned from paid-and-completed.
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
                     AND UserRole IN ('Staff','Admin'))
          THROW 51700, 'Only Staff/Admin may receive a vehicle.', 1;
      SELECT @Status=BookingStatus,@VehicleId=VehicleId FROM dbo.Bookings WITH (UPDLOCK,HOLDLOCK)
      WHERE BookingId=@BookingId;
      IF @Status<>'InUse' OR @Status IS NULL THROW 51701, 'Vehicle is not in use.', 1;
      SELECT @OdometerOut=OdometerOutKm FROM dbo.VehicleHandovers WHERE BookingId=@BookingId;
      IF @OdometerOut IS NULL OR @OdometerInKm<@OdometerOut
          THROW 51702, 'Invalid return odometer reading.', 1;
      INSERT INTO dbo.VehicleReturns
          (BookingId,StaffId,OdometerInKm,FuelPercentIn,LateFee,DamageFee,OtherFee,InspectionNotes)
      VALUES(@BookingId,@StaffUserId,@OdometerInKm,@FuelPercentIn,@LateFee,@DamageFee,@OtherFee,@InspectionNotes);
      UPDATE dbo.Vehicles SET OdometerKm=@OdometerInKm WHERE VehicleId=@VehicleId;
      UPDATE dbo.Bookings SET BookingStatus='Returned',UpdatedAt=SYSUTCDATETIME() WHERE BookingId=@BookingId;
      INSERT INTO dbo.BookingStatusHistory(BookingId,OldStatus,NewStatus,ChangedByUserId,ChangeNote)
      VALUES(@BookingId,'InUse','Returned',@StaffUserId,N'Vehicle returned, final balance pending');
      COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
      IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
      THROW;
    END CATCH
END;
GO

-- 18. FINISH BOOKING after sufficient paid balance + return fees.
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
                     AND UserRole IN ('Staff','Admin'))
          THROW 51800, 'Only Staff/Admin may complete booking.', 1;
      SELECT @BaseAmount=TotalAmount FROM dbo.Bookings WITH (UPDLOCK,HOLDLOCK)
      WHERE BookingId=@BookingId AND BookingStatus='Returned';
      IF @BaseAmount IS NULL THROW 51801, 'Booking is not returned.', 1;
      SELECT @Extra=LateFee+DamageFee+OtherFee FROM dbo.VehicleReturns WHERE BookingId=@BookingId;
      SELECT @Paid=COALESCE(SUM(CASE WHEN PaymentType='Refund' THEN -Amount ELSE Amount END),0)
      FROM dbo.Payments WHERE BookingId=@BookingId AND PaymentStatus='Success';
      IF @Paid < @BaseAmount+@Extra THROW 51802, 'Remaining balance has not been paid.', 1;
      UPDATE dbo.Bookings SET BookingStatus='Completed',UpdatedAt=SYSUTCDATETIME()
      WHERE BookingId=@BookingId;
      INSERT INTO dbo.BookingStatusHistory(BookingId,OldStatus,NewStatus,ChangedByUserId,ChangeNote)
      VALUES(@BookingId,'Returned','Completed',@StaffUserId,N'All payments reconciled');
      COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
      IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
      THROW;
    END CATCH
END;
GO

-- ================================================================
-- 03. DU LIEU MAU (SEED DATA) - CHI THEM BAN GHI CON THIEU
-- Khong DROP / DELETE / TRUNCATE / UPDATE du lieu cu.
-- Demo data is fictional. Images are placeholders, not real car photos.
-- All times stored as UTC, rental prices in VND.
-- ================================================================

-- Rental policy - 50 percent deposit and driver fee per day
IF NOT EXISTS (SELECT 1 FROM dbo.RentalPolicies WHERE PolicyId = 1)
BEGIN
    INSERT INTO dbo.RentalPolicies
        (PolicyId, DepositPercent, DriverDailyFee, OnlineHoldMinutes, CashHoldHours)
    VALUES (1, 50, 350000, 30, 24);
END;
GO

-- Account demo (ASP.NET Core PasswordHasher V3 compatible hashes).
-- Only for LOCAL DEVELOPMENT. Change passwords before deployment.
-- Admin: Admin@12345! | Staff: Staff@12345! | Customer: Customer@12345!

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'admin.demo@crms.example.com')
    INSERT INTO dbo.Users(FullName, Email, PasswordHash, UserRole, IsActive)
    VALUES (N'Admin Demo', N'admin.demo@crms.example.com', N'AQAAAAIAAYagAAAAENAttIFARiGhxy2y2QPPLcub7ePM7kSfWpdDnZI8jHyAlaGxgS39ARbeltG8osQtKw==', 'Admin', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'staff01@crms.example.com')
    INSERT INTO dbo.Users(FullName, Email, PasswordHash, UserRole, IsActive)
    VALUES (N'Staff Demo 01', N'staff01@crms.example.com', N'AQAAAAIAAYagAAAAEEUoALyZBYcdvnmRhl6a3vGiFM80QugI7nCUvKbgdVfhS2jPWimkkVBv8y8AC8lG0g==', 'Staff', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'staff02@crms.example.com')
    INSERT INTO dbo.Users(FullName, Email, PasswordHash, UserRole, IsActive)
    VALUES (N'Staff Demo 02', N'staff02@crms.example.com', N'AQAAAAIAAYagAAAAEL9B5wJqoHa7bRRYZuues7yxGMZmL/mKQzD9Y+W6t6cOJTZc7kZ19wsKOWseWPI0wQ==', 'Staff', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'customer01@crms.example.com')
    INSERT INTO dbo.Users(FullName, Email, PasswordHash, UserRole, IsActive)
    VALUES (N'Customer Demo 01', N'customer01@crms.example.com', N'AQAAAAIAAYagAAAAENgGBqW8pvRHTMQtOgMewDJuzV75VdQBazNg28ASyCQnlPMK0nv4dY72RPnieEUihA==', 'Customer', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'customer02@crms.example.com')
    INSERT INTO dbo.Users(FullName, Email, PasswordHash, UserRole, IsActive)
    VALUES (N'Customer Demo 02', N'customer02@crms.example.com', N'AQAAAAIAAYagAAAAEIb7S8utzVL+warzYfTHbKV7UTYGA18XYeM7SG8W5tfsgNomb0AvdKRANiKhojnF6g==', 'Customer', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'customer03@crms.example.com')
    INSERT INTO dbo.Users(FullName, Email, PasswordHash, UserRole, IsActive)
    VALUES (N'Customer Demo 03', N'customer03@crms.example.com', N'AQAAAAIAAYagAAAAELzzZ7ATi1XgV/rOo1SDMp59hG0W2eZWLSya9GLn3RRSxq3w0ezjyhr2nSEEG5pMpw==', 'Customer', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'customer04@crms.example.com')
    INSERT INTO dbo.Users(FullName, Email, PasswordHash, UserRole, IsActive)
    VALUES (N'Customer Demo 04', N'customer04@crms.example.com', N'AQAAAAIAAYagAAAAEICjxbOyX7Ydy6r1xrQcQcRblulb0hQ6oD4T1Lh7Cy7OhZD0cuCWBGsZ6sCznXZLpw==', 'Customer', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'customer05@crms.example.com')
    INSERT INTO dbo.Users(FullName, Email, PasswordHash, UserRole, IsActive)
    VALUES (N'Customer Demo 05', N'customer05@crms.example.com', N'AQAAAAIAAYagAAAAEHv9NlbxdtagizlN02hzgmLnPtxCPb+iFJ8CA4730xv2Kl3RMCZahb6DZJwwqdxk6Q==', 'Customer', 1);
GO

-- Seven vehicle categories
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleCategories WHERE CategoryName = N'Sedan')
    INSERT INTO dbo.VehicleCategories(CategoryName, Description) VALUES (N'Sedan', N'Xe sedan 4-5 cho, phu hop di pho va cong tac');
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleCategories WHERE CategoryName = N'SUV')
    INSERT INTO dbo.VehicleCategories(CategoryName, Description) VALUES (N'SUV', N'Xe gam cao cho gia dinh va du lich');
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleCategories WHERE CategoryName = N'MPV')
    INSERT INTO dbo.VehicleCategories(CategoryName, Description) VALUES (N'MPV', N'Xe da dung 7-8 cho');
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleCategories WHERE CategoryName = N'Hatchback')
    INSERT INTO dbo.VehicleCategories(CategoryName, Description) VALUES (N'Hatchback', N'Xe nho gon di chuyen trong do thi');
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleCategories WHERE CategoryName = N'Pickup')
    INSERT INTO dbo.VehicleCategories(CategoryName, Description) VALUES (N'Pickup', N'Xe ban tai phu hop duong xa');
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleCategories WHERE CategoryName = N'Luxury')
    INSERT INTO dbo.VehicleCategories(CategoryName, Description) VALUES (N'Luxury', N'Xe hang sang cho su kien va cong tac');
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleCategories WHERE CategoryName = N'Electric')
    INSERT INTO dbo.VehicleCategories(CategoryName, Description) VALUES (N'Electric', N'Xe dien tiet kiem nhien lieu');
GO

-- Three vehicles retained from the first 02_Demo_Data.sql sample
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate='65A-12345')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, Description)
    SELECT CategoryId, '65A-12345', N'Toyota', N'Vios', 2023, 5, 'Automatic', 'Gasoline', 700000, N'Xe demo tu bo du lieu goc'
    FROM dbo.VehicleCategories WHERE CategoryName = N'Sedan';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate='65A-67890')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, Description)
    SELECT CategoryId, '65A-67890', N'Ford', N'Everest', 2024, 7, 'Automatic', 'Diesel', 1500000, N'Xe demo tu bo du lieu goc'
    FROM dbo.VehicleCategories WHERE CategoryName = N'SUV';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate='65A-24680')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, Description)
    SELECT CategoryId, '65A-24680', N'Toyota', N'Innova', 2022, 7, 'Manual', 'Gasoline', 1100000, N'Xe demo tu bo du lieu goc'
    FROM dbo.VehicleCategories WHERE CategoryName = N'MPV';
GO

-- 26 additional fictional vehicles, including one Suspended for status filtering
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-001')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-001', N'Toyota', N'Vios', 2023, 5, 'Automatic', 'Gasoline', 700000, 18000, 'Active', N'Xe demo Toyota Vios; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'Sedan';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-002')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-002', N'Honda', N'City', 2024, 5, 'Automatic', 'Gasoline', 820000, 11000, 'Active', N'Xe demo Honda City; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'Sedan';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-003')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-003', N'Hyundai', N'Accent', 2022, 5, 'Manual', 'Gasoline', 620000, 34000, 'Active', N'Xe demo Hyundai Accent; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'Sedan';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-004')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-004', N'Mazda', N'Mazda 3', 2023, 5, 'Automatic', 'Gasoline', 950000, 21000, 'Active', N'Xe demo Mazda Mazda 3; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'Sedan';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-005')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-005', N'Kia', N'K3', 2024, 5, 'Automatic', 'Gasoline', 850000, 9000, 'Active', N'Xe demo Kia K3; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'Sedan';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-006')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-006', N'Mazda', N'CX-5', 2023, 5, 'Automatic', 'Gasoline', 1200000, 25000, 'Active', N'Xe demo Mazda CX-5; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'SUV';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-007')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-007', N'Ford', N'Everest', 2024, 7, 'Automatic', 'Diesel', 1550000, 14000, 'Active', N'Xe demo Ford Everest; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'SUV';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-008')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-008', N'Hyundai', N'Santa Fe', 2023, 7, 'Automatic', 'Diesel', 1450000, 23000, 'Active', N'Xe demo Hyundai Santa Fe; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'SUV';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-009')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-009', N'Toyota', N'Fortuner', 2022, 7, 'Automatic', 'Diesel', 1400000, 41000, 'Active', N'Xe demo Toyota Fortuner; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'SUV';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-010')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-010', N'Kia', N'Seltos', 2024, 5, 'Automatic', 'Gasoline', 980000, 15000, 'Active', N'Xe demo Kia Seltos; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'SUV';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-011')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-011', N'Mitsubishi', N'Xpander', 2023, 7, 'Automatic', 'Gasoline', 1000000, 28000, 'Active', N'Xe demo Mitsubishi Xpander; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'MPV';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-012')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-012', N'Toyota', N'Innova', 2022, 7, 'Manual', 'Gasoline', 1100000, 39000, 'Active', N'Xe demo Toyota Innova; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'MPV';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-013')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-013', N'Toyota', N'Veloz Cross', 2024, 7, 'Automatic', 'Gasoline', 1120000, 9000, 'Active', N'Xe demo Toyota Veloz Cross; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'MPV';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-014')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-014', N'Kia', N'Carnival', 2024, 7, 'Automatic', 'Diesel', 1900000, 12000, 'Active', N'Xe demo Kia Carnival; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'MPV';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-015')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-015', N'Hyundai', N'Grand i10', 2022, 5, 'Manual', 'Gasoline', 520000, 33000, 'Active', N'Xe demo Hyundai Grand i10; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'Hatchback';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-016')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-016', N'Toyota', N'Wigo', 2023, 5, 'Automatic', 'Gasoline', 580000, 18000, 'Active', N'Xe demo Toyota Wigo; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'Hatchback';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-017')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-017', N'Suzuki', N'Swift', 2024, 5, 'Automatic', 'Gasoline', 680000, 7000, 'Active', N'Xe demo Suzuki Swift; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'Hatchback';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-018')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-018', N'Ford', N'Ranger', 2023, 5, 'Automatic', 'Diesel', 1300000, 35000, 'Active', N'Xe demo Ford Ranger; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'Pickup';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-019')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-019', N'Mitsubishi', N'Triton', 2022, 5, 'Automatic', 'Diesel', 1180000, 42000, 'Active', N'Xe demo Mitsubishi Triton; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'Pickup';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-020')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-020', N'Mercedes-Benz', N'C 200', 2024, 5, 'Automatic', 'Gasoline', 2400000, 8000, 'Active', N'Xe demo Mercedes-Benz C 200; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'Luxury';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-021')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-021', N'BMW', N'320i', 2023, 5, 'Automatic', 'Gasoline', 2300000, 15000, 'Active', N'Xe demo BMW 320i; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'Luxury';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-022')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-022', N'Lexus', N'ES 250', 2024, 5, 'Automatic', 'Gasoline', 3200000, 6000, 'Active', N'Xe demo Lexus ES 250; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'Luxury';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-023')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-023', N'VinFast', N'VF 5', 2024, 5, 'Automatic', 'Electric', 800000, 12000, 'Active', N'Xe demo VinFast VF 5; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'Electric';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-024')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-024', N'VinFast', N'VF 8', 2025, 5, 'Automatic', 'Electric', 1350000, 6500, 'Active', N'Xe demo VinFast VF 8; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'Electric';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-025')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-025', N'VinFast', N'VF 9', 2025, 7, 'Automatic', 'Electric', 2000000, 4200, 'Active', N'Xe demo VinFast VF 9; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'Electric';
IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles WHERE LicensePlate = 'DEMO-CRMS-026')
    INSERT INTO dbo.Vehicles(CategoryId, LicensePlate, Brand, Model, ManufactureYear, SeatCount, Transmission, FuelType, DailyRate, OdometerKm, OperationalStatus, Description)
    SELECT CategoryId, 'DEMO-CRMS-026', N'Toyota', N'Camry Hybrid', 2024, 5, 'Automatic', 'Hybrid', 1750000, 11500, 'Suspended', N'Xe demo Toyota Camry Hybrid; gia/ngay chi de minh hoa, khong phai bang gia chinh thuc.'
    FROM dbo.VehicleCategories WHERE CategoryName = N'Sedan';
GO

INSERT INTO dbo.VehicleLongTermRates (VehicleId, DurationMonths, BaseMonthlyRate, DiscountPercent)
SELECT v.VehicleId, p.DurationMonths, CAST(v.DailyRate * 30 AS DECIMAL(18,2)), p.DiscountPercent
FROM dbo.Vehicles v
CROSS JOIN (VALUES (1,0.00),(3,2.00),(6,3.00),(9,4.00),(12,5.00)) p(DurationMonths, DiscountPercent)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.VehicleLongTermRates r
    WHERE r.VehicleId = v.VehicleId AND r.DurationMonths = p.DurationMonths
);
GO

-- 58 illustrative image URLs, 2 per vehicle (NOT real car photos)
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='65A-12345' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Toyota+Vios+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Toyota+Vios+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='65A-12345';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='65A-12345' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Toyota+Vios+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Toyota+Vios+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='65A-12345';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='65A-67890' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Ford+Everest+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Ford+Everest+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='65A-67890';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='65A-67890' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Ford+Everest+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Ford+Everest+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='65A-67890';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='65A-24680' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Toyota+Innova+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Toyota+Innova+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='65A-24680';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='65A-24680' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Toyota+Innova+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Toyota+Innova+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='65A-24680';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-001' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Toyota+Vios+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Toyota+Vios+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-001';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-001' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Toyota+Vios+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Toyota+Vios+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-001';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-002' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Honda+City+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Honda+City+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-002';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-002' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Honda+City+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Honda+City+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-002';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-003' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Hyundai+Accent+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Hyundai+Accent+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-003';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-003' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Hyundai+Accent+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Hyundai+Accent+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-003';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-004' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Mazda+Mazda+3+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Mazda+Mazda+3+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-004';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-004' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Mazda+Mazda+3+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Mazda+Mazda+3+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-004';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-005' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Kia+K3+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Kia+K3+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-005';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-005' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Kia+K3+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Kia+K3+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-005';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-006' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Mazda+CX5+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Mazda+CX5+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-006';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-006' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Mazda+CX5+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Mazda+CX5+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-006';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-007' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Ford+Everest+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Ford+Everest+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-007';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-007' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Ford+Everest+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Ford+Everest+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-007';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-008' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Hyundai+Santa+Fe+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Hyundai+Santa+Fe+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-008';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-008' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Hyundai+Santa+Fe+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Hyundai+Santa+Fe+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-008';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-009' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Toyota+Fortuner+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Toyota+Fortuner+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-009';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-009' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Toyota+Fortuner+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Toyota+Fortuner+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-009';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-010' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Kia+Seltos+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Kia+Seltos+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-010';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-010' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Kia+Seltos+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Kia+Seltos+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-010';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-011' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Mitsubishi+Xpander+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Mitsubishi+Xpander+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-011';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-011' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Mitsubishi+Xpander+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Mitsubishi+Xpander+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-011';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-012' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Toyota+Innova+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Toyota+Innova+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-012';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-012' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Toyota+Innova+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Toyota+Innova+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-012';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-013' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Toyota+Veloz+Cross+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Toyota+Veloz+Cross+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-013';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-013' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Toyota+Veloz+Cross+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Toyota+Veloz+Cross+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-013';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-014' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Kia+Carnival+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Kia+Carnival+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-014';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-014' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Kia+Carnival+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Kia+Carnival+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-014';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-015' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Hyundai+Grand+i10+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Hyundai+Grand+i10+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-015';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-015' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Hyundai+Grand+i10+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Hyundai+Grand+i10+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-015';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-016' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Toyota+Wigo+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Toyota+Wigo+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-016';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-016' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Toyota+Wigo+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Toyota+Wigo+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-016';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-017' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Suzuki+Swift+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Suzuki+Swift+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-017';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-017' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Suzuki+Swift+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Suzuki+Swift+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-017';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-018' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Ford+Ranger+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Ford+Ranger+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-018';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-018' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Ford+Ranger+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Ford+Ranger+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-018';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-019' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Mitsubishi+Triton+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Mitsubishi+Triton+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-019';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-019' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Mitsubishi+Triton+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Mitsubishi+Triton+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-019';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-020' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=MercedesBenz+C+200+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=MercedesBenz+C+200+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-020';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-020' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=MercedesBenz+C+200+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=MercedesBenz+C+200+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-020';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-021' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=BMW+320i+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=BMW+320i+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-021';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-021' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=BMW+320i+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=BMW+320i+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-021';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-022' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Lexus+ES+250+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Lexus+ES+250+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-022';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-022' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Lexus+ES+250+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Lexus+ES+250+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-022';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-023' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=VinFast+VF+5+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=VinFast+VF+5+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-023';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-023' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=VinFast+VF+5+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=VinFast+VF+5+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-023';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-024' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=VinFast+VF+8+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=VinFast+VF+8+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-024';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-024' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=VinFast+VF+8+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=VinFast+VF+8+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-024';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-025' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=VinFast+VF+9+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=VinFast+VF+9+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-025';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-025' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=VinFast+VF+9+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=VinFast+VF+9+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-025';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-026' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Toyota+Camry+Hybrid+Front')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Toyota+Camry+Hybrid+Front', 1, 0
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-026';
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleImages im JOIN dbo.Vehicles v ON im.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-026' AND im.ImageUrl=N'https://placehold.co/960x540/png?text=Toyota+Camry+Hybrid+Side')
    INSERT INTO dbo.VehicleImages(VehicleId, ImageUrl, IsPrimary, SortOrder)
    SELECT VehicleId, N'https://placehold.co/960x540/png?text=Toyota+Camry+Hybrid+Side', 0, 1
    FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-026';
GO

-- Eight fictional drivers
IF NOT EXISTS (SELECT 1 FROM dbo.Drivers WHERE LicenseNumber=N'DEMO-DL-001')
    INSERT INTO dbo.Drivers(FullName, Phone, LicenseNumber, IsActive, Notes)
    VALUES (N'Nguyen Van An', '0900000101', N'DEMO-DL-001', 1, N'Thong tin minh hoa, khong phai bang lai that');
IF NOT EXISTS (SELECT 1 FROM dbo.Drivers WHERE LicenseNumber=N'DEMO-DL-002')
    INSERT INTO dbo.Drivers(FullName, Phone, LicenseNumber, IsActive, Notes)
    VALUES (N'Tran Minh Binh', '0900000102', N'DEMO-DL-002', 1, N'Thong tin minh hoa, khong phai bang lai that');
IF NOT EXISTS (SELECT 1 FROM dbo.Drivers WHERE LicenseNumber=N'DEMO-DL-003')
    INSERT INTO dbo.Drivers(FullName, Phone, LicenseNumber, IsActive, Notes)
    VALUES (N'Le Hoang Phuc', '0900000103', N'DEMO-DL-003', 1, N'Thong tin minh hoa, khong phai bang lai that');
IF NOT EXISTS (SELECT 1 FROM dbo.Drivers WHERE LicenseNumber=N'DEMO-DL-004')
    INSERT INTO dbo.Drivers(FullName, Phone, LicenseNumber, IsActive, Notes)
    VALUES (N'Pham Quoc Khang', '0900000104', N'DEMO-DL-004', 1, N'Thong tin minh hoa, khong phai bang lai that');
IF NOT EXISTS (SELECT 1 FROM dbo.Drivers WHERE LicenseNumber=N'DEMO-DL-005')
    INSERT INTO dbo.Drivers(FullName, Phone, LicenseNumber, IsActive, Notes)
    VALUES (N'Vo Thanh Long', '0900000105', N'DEMO-DL-005', 1, N'Thong tin minh hoa, khong phai bang lai that');
IF NOT EXISTS (SELECT 1 FROM dbo.Drivers WHERE LicenseNumber=N'DEMO-DL-006')
    INSERT INTO dbo.Drivers(FullName, Phone, LicenseNumber, IsActive, Notes)
    VALUES (N'Bui Anh Tuan', '0900000106', N'DEMO-DL-006', 1, N'Thong tin minh hoa, khong phai bang lai that');
IF NOT EXISTS (SELECT 1 FROM dbo.Drivers WHERE LicenseNumber=N'DEMO-DL-007')
    INSERT INTO dbo.Drivers(FullName, Phone, LicenseNumber, IsActive, Notes)
    VALUES (N'Do Minh Quan', '0900000107', N'DEMO-DL-007', 1, N'Thong tin minh hoa, khong phai bang lai that');
IF NOT EXISTS (SELECT 1 FROM dbo.Drivers WHERE LicenseNumber=N'DEMO-DL-008')
    INSERT INTO dbo.Drivers(FullName, Phone, LicenseNumber, IsActive, Notes)
    VALUES (N'Hoang Bao Nam', '0900000108', N'DEMO-DL-008', 0, N'Thong tin minh hoa, khong phai bang lai that');
GO

-- Eight vouchers: percent, fixed, inactive, expired, not-yet-active
IF NOT EXISTS (SELECT 1 FROM dbo.Vouchers WHERE VoucherCode=N'WELCOME10')
    INSERT INTO dbo.Vouchers(VoucherCode, DiscountType, DiscountValue, MaxDiscountAmount, MinimumOrderAmount, StartsAt, EndsAt, UsageLimit, PerCustomerLimit, IsActive)
    VALUES (N'WELCOME10', 'Percent', 10, 150000, 500000, DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, 365, SYSUTCDATETIME()), 100, 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Vouchers WHERE VoucherCode=N'TRIP15')
    INSERT INTO dbo.Vouchers(VoucherCode, DiscountType, DiscountValue, MaxDiscountAmount, MinimumOrderAmount, StartsAt, EndsAt, UsageLimit, PerCustomerLimit, IsActive)
    VALUES (N'TRIP15', 'Percent', 15, 350000, 1500000, DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, 365, SYSUTCDATETIME()), 100, 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Vouchers WHERE VoucherCode=N'WEEKEND20')
    INSERT INTO dbo.Vouchers(VoucherCode, DiscountType, DiscountValue, MaxDiscountAmount, MinimumOrderAmount, StartsAt, EndsAt, UsageLimit, PerCustomerLimit, IsActive)
    VALUES (N'WEEKEND20', 'Percent', 20, 250000, 2000000, DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, 180, SYSUTCDATETIME()), 60, 2, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Vouchers WHERE VoucherCode=N'SAVE50K')
    INSERT INTO dbo.Vouchers(VoucherCode, DiscountType, DiscountValue, MaxDiscountAmount, MinimumOrderAmount, StartsAt, EndsAt, UsageLimit, PerCustomerLimit, IsActive)
    VALUES (N'SAVE50K', 'Fixed', 50000, NULL, 700000, DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, 365, SYSUTCDATETIME()), 150, 2, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Vouchers WHERE VoucherCode=N'SAVE100K')
    INSERT INTO dbo.Vouchers(VoucherCode, DiscountType, DiscountValue, MaxDiscountAmount, MinimumOrderAmount, StartsAt, EndsAt, UsageLimit, PerCustomerLimit, IsActive)
    VALUES (N'SAVE100K', 'Fixed', 100000, NULL, 1500000, DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, 365, SYSUTCDATETIME()), 100, 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Vouchers WHERE VoucherCode=N'FAMILY200')
    INSERT INTO dbo.Vouchers(VoucherCode, DiscountType, DiscountValue, MaxDiscountAmount, MinimumOrderAmount, StartsAt, EndsAt, UsageLimit, PerCustomerLimit, IsActive)
    VALUES (N'FAMILY200', 'Fixed', 200000, NULL, 3000000, DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, 365, SYSUTCDATETIME()), 40, 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Vouchers WHERE VoucherCode=N'OLDPROMO')
    INSERT INTO dbo.Vouchers(VoucherCode, DiscountType, DiscountValue, MaxDiscountAmount, MinimumOrderAmount, StartsAt, EndsAt, UsageLimit, PerCustomerLimit, IsActive)
    VALUES (N'OLDPROMO', 'Percent', 5, 80000, 300000, DATEADD(DAY, -120, SYSUTCDATETIME()), DATEADD(DAY, -30, SYSUTCDATETIME()), 30, 1, 0);
IF NOT EXISTS (SELECT 1 FROM dbo.Vouchers WHERE VoucherCode=N'NEXTMONTH')
    INSERT INTO dbo.Vouchers(VoucherCode, DiscountType, DiscountValue, MaxDiscountAmount, MinimumOrderAmount, StartsAt, EndsAt, UsageLimit, PerCustomerLimit, IsActive)
    VALUES (N'NEXTMONTH', 'Percent', 12, 250000, 1000000, DATEADD(DAY, 30, SYSUTCDATETIME()), DATEADD(DAY, 120, SYSUTCDATETIME()), 80, 1, 1);
GO

-- Four maintenance records; requires an Admin demo user.
-- No active rental records are seeded. History remains testable from application.
DECLARE @DemoAdminId BIGINT =
    (SELECT TOP (1) UserId FROM dbo.Users WHERE Email=N'admin.demo@crms.example.com' AND UserRole='Admin');
IF @DemoAdminId IS NOT NULL
BEGIN

    IF NOT EXISTS (SELECT 1 FROM dbo.MaintenanceRecords m JOIN dbo.Vehicles v ON m.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-019' AND m.Description=N'Bao duong dinh ky, thay dau va loc')
        INSERT INTO dbo.MaintenanceRecords(VehicleId, CreatedByUserId, StartAt, EndAt, MaintenanceStatus, Description, Cost)
        SELECT VehicleId, @DemoAdminId, DATEADD(DAY, -70, SYSUTCDATETIME()), DATEADD(DAY, -69, SYSUTCDATETIME()), 'Completed', N'Bao duong dinh ky, thay dau va loc', 850000
        FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-019';
    IF NOT EXISTS (SELECT 1 FROM dbo.MaintenanceRecords m JOIN dbo.Vehicles v ON m.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-020' AND m.Description=N'Kiem tra he thong dieu hoa')
        INSERT INTO dbo.MaintenanceRecords(VehicleId, CreatedByUserId, StartAt, EndAt, MaintenanceStatus, Description, Cost)
        SELECT VehicleId, @DemoAdminId, DATEADD(DAY, -20, SYSUTCDATETIME()), DATEADD(DAY, -19, SYSUTCDATETIME()), 'Completed', N'Kiem tra he thong dieu hoa', 650000
        FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-020';
    IF NOT EXISTS (SELECT 1 FROM dbo.MaintenanceRecords m JOIN dbo.Vehicles v ON m.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-025' AND m.Description=N'Kiem tra phanh va lop')
        INSERT INTO dbo.MaintenanceRecords(VehicleId, CreatedByUserId, StartAt, EndAt, MaintenanceStatus, Description, Cost)
        SELECT VehicleId, @DemoAdminId, DATEADD(DAY, 45, SYSUTCDATETIME()), DATEADD(DAY, 46, SYSUTCDATETIME()), 'Scheduled', N'Kiem tra phanh va lop', 0
        FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-025';
    IF NOT EXISTS (SELECT 1 FROM dbo.MaintenanceRecords m JOIN dbo.Vehicles v ON m.VehicleId = v.VehicleId WHERE v.LicensePlate='DEMO-CRMS-026' AND m.Description=N'Kiem tra pin va he thong dien')
        INSERT INTO dbo.MaintenanceRecords(VehicleId, CreatedByUserId, StartAt, EndAt, MaintenanceStatus, Description, Cost)
        SELECT VehicleId, @DemoAdminId, DATEADD(DAY, 55, SYSUTCDATETIME()), DATEADD(DAY, 56, SYSUTCDATETIME()), 'Scheduled', N'Kiem tra pin va he thong dien', 0
        FROM dbo.Vehicles WHERE LicensePlate='DEMO-CRMS-026';
END;
GO


-- BOOKINGS / PAYMENTS / HANDOVERS / RETURNS / REVIEWS / AUDIT:
-- intentionally empty. Create these with the actual project workflow so
-- deposits, payment statuses, refunds and inventory checks stay coherent.
PRINT N'CRMS sample data inserted where missing; existing rows were preserved.';
GO
