-- Run once on the selected CRMS database. Re-running preserves existing records.
-- No simulated deposit is inserted into the real Payments ledger.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() IN ('master','model','msdb','tempdb') THROW 55000, 'Select the CRMS application database first.', 1;
IF OBJECT_ID(N'dbo.Bookings', N'U') IS NULL THROW 55000, 'Install CRMS.sql first.', 1;
BEGIN TRY
    BEGIN TRANSACTION;
    IF COL_LENGTH(N'dbo.Bookings', N'IsDemo') IS NULL
        ALTER TABLE dbo.Bookings ADD IsDemo bit NOT NULL CONSTRAINT DF_Bookings_IsDemo DEFAULT(0) WITH VALUES;
    IF OBJECT_ID(N'dbo.DemoPayments', N'U') IS NULL
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
            PaymentStatus varchar(10) NOT NULL CONSTRAINT DF_DemoPayments_Status DEFAULT('Pending'),
            BankTransactionReference nvarchar(100) NULL,
            ConfirmationRequestKey uniqueidentifier NULL,
            VerifiedByUserId bigint NULL REFERENCES dbo.Users(UserId),
            PaidAt datetime2(0) NULL,
            ConfirmedBooking bit NOT NULL CONSTRAINT DF_DemoPayments_Confirmed DEFAULT(0),
            CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_DemoPayments_Created DEFAULT SYSUTCDATETIME(),
            RowVersion rowversion NOT NULL,
            CONSTRAINT CK_DemoPayments_State CHECK
            (
                (PaymentStatus='Pending' AND ActualAmount=0 AND PaidAt IS NULL AND VerifiedByUserId IS NULL
                 AND BankTransactionReference IS NULL AND ConfirmationRequestKey IS NULL AND ConfirmedBooking=0)
                OR
                (PaymentStatus='Success' AND ActualAmount=1000 AND PaidAt IS NOT NULL AND VerifiedByUserId IS NOT NULL
                 AND NULLIF(LTRIM(RTRIM(BankTransactionReference)),'') IS NOT NULL AND ConfirmationRequestKey IS NOT NULL)
            )
        );
        CREATE UNIQUE INDEX UX_DemoPayments_Booking ON dbo.DemoPayments(BookingId);
        CREATE UNIQUE INDEX UX_DemoPayments_TransferContent ON dbo.DemoPayments(TransferContent);
        CREATE UNIQUE INDEX UX_DemoPayments_BankReference ON dbo.DemoPayments(BankTransactionReference) WHERE BankTransactionReference IS NOT NULL;
        CREATE UNIQUE INDEX UX_DemoPayments_RequestKey ON dbo.DemoPayments(ConfirmationRequestKey) WHERE ConfirmationRequestKey IS NOT NULL;
    END;
    COMMIT;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;
