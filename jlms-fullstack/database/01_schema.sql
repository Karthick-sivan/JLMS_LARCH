/* ============================================================
   JLMS — Jewel Loan Management System
   SQL Server Database Schema (empty — no sample data)
   Target: SQL Server 2017+ / Azure SQL
   ============================================================
   HOW TO RUN:
   1. Open SQL Server Management Studio (SSMS)
   2. Connect to your SQL Server instance (SQL Auth)
   3. Open this file and click "Execute" (or press F5)
   4. This creates a database called JLMS_DB plus all tables
   ============================================================ */

IF DB_ID('JLMS_DB') IS NULL
BEGIN
    CREATE DATABASE JLMS_DB;
END
GO

USE JLMS_DB;
GO

/* ============================================================
   1. BRANCHES
   ============================================================ */
CREATE TABLE Branches (
    BranchId        INT IDENTITY(1,1) PRIMARY KEY,
    BranchCode      VARCHAR(20)   NOT NULL UNIQUE,
    BranchName      NVARCHAR(150) NOT NULL,
    City            NVARCHAR(100) NULL,
    State           NVARCHAR(100) NULL,
    IsActive        BIT NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

/* ============================================================
   2. ROLES
   ============================================================ */
CREATE TABLE Roles (
    RoleId          INT IDENTITY(1,1) PRIMARY KEY,
    RoleName        NVARCHAR(80) NOT NULL UNIQUE,
    Description     NVARCHAR(300) NULL,
    IsActive        BIT NOT NULL DEFAULT 1
);
GO

/* ============================================================
   3. USERS (system users / employees)
   ============================================================ */
CREATE TABLE Users (
    UserId          INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeCode    VARCHAR(20)   NOT NULL UNIQUE,
    FullName        NVARCHAR(150) NOT NULL,
    Username        VARCHAR(80)   NOT NULL UNIQUE,
    PasswordHash    NVARCHAR(256) NOT NULL,
    RoleId          INT NOT NULL REFERENCES Roles(RoleId),
    BranchId        INT NOT NULL REFERENCES Branches(BranchId),
    Mobile          VARCHAR(15)   NULL,
    Email           NVARCHAR(150) NULL,
    IsActive        BIT NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

/* ============================================================
   4. CUSTOMERS
   ============================================================ */
CREATE TABLE Customers (
    CustomerId      INT IDENTITY(1,1) PRIMARY KEY,
    CustomerCode    VARCHAR(20)   NOT NULL UNIQUE,
    CustomerName    NVARCHAR(150) NOT NULL,
    Gender          VARCHAR(10)   NULL,             -- Male / Female / Other
    DateOfBirth     DATE          NULL,
    Mobile          VARCHAR(15)   NOT NULL,
    AlternateMobile VARCHAR(15)   NULL,
    Address         NVARCHAR(400) NULL,
    City            NVARCHAR(100) NULL,
    State           NVARCHAR(100) NULL,
    Pincode         VARCHAR(10)   NULL,
    AadhaarNumber   VARCHAR(20)   NULL,
    PanNumber       VARCHAR(20)   NULL,
    PhotoPath       NVARCHAR(300) NULL,
    AadhaarDocPath  NVARCHAR(300) NULL,
    PanDocPath      NVARCHAR(300) NULL,
    KycVerified     BIT NOT NULL DEFAULT 0,
    BranchId        INT NULL REFERENCES Branches(BranchId),
    CreatedBy       INT NULL REFERENCES Users(UserId),
    CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2 NULL
);
GO
CREATE INDEX IX_Customers_Mobile ON Customers(Mobile);
CREATE INDEX IX_Customers_Aadhaar ON Customers(AadhaarNumber);
GO

/* ============================================================
   5. JEWEL TYPE MASTER
   ============================================================ */
CREATE TABLE JewelTypes (
    JewelTypeId     INT IDENTITY(1,1) PRIMARY KEY,
    JewelTypeName   NVARCHAR(100) NOT NULL,
    Category        VARCHAR(30)   NOT NULL DEFAULT 'Ornament',  -- Ornament / Coin / Bar
    DefaultPurity   VARCHAR(20)   NULL,             -- e.g. 22K, 18K, 24K
    WastagePercent  DECIMAL(5,2)  NOT NULL DEFAULT 0,
    IsActive        BIT NOT NULL DEFAULT 1
);
GO

/* ============================================================
   6. GOLD RATE MASTER (daily rate history)
   ============================================================ */
CREATE TABLE GoldRates (
    GoldRateId      INT IDENTITY(1,1) PRIMARY KEY,
    EffectiveDate   DATE NOT NULL,
    Rate24K         DECIMAL(10,2) NOT NULL,
    Rate22K         DECIMAL(10,2) NOT NULL,
    Rate18K         DECIMAL(10,2) NOT NULL,
    UpdatedBy       INT NULL REFERENCES Users(UserId),
    CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_GoldRates_Date UNIQUE (EffectiveDate)
);
GO

/* ============================================================
   7. LOAN SCHEME MASTER
   ============================================================ */
CREATE TABLE LoanSchemes (
    LoanSchemeId      INT IDENTITY(1,1) PRIMARY KEY,
    SchemeName        NVARCHAR(100) NOT NULL,
    InterestRatePct   DECIMAL(5,2) NOT NULL,         -- per annum
    TenureMonths      INT NOT NULL,
    MaxLtvPercent     DECIMAL(5,2) NOT NULL,          -- max loan-to-value
    ProcessingFee     DECIMAL(10,2) NOT NULL DEFAULT 0,
    PenaltyRatePerDay DECIMAL(5,2) NOT NULL DEFAULT 0,
    IsActive          BIT NOT NULL DEFAULT 1,
    CreatedAt         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

/* ============================================================
   8. LOANS (main loan record)
   ============================================================ */
CREATE TABLE Loans (
    LoanId            INT IDENTITY(1,1) PRIMARY KEY,
    LoanNumber        VARCHAR(30)   NOT NULL UNIQUE,
    CustomerId        INT NOT NULL REFERENCES Customers(CustomerId),
    LoanSchemeId      INT NOT NULL REFERENCES LoanSchemes(LoanSchemeId),
    BranchId          INT NOT NULL REFERENCES Branches(BranchId),

    LoanDate          DATE NULL,
    MaturityDate      DATE NULL,

    InterestRatePct   DECIMAL(5,2) NOT NULL,
    TenureMonths      INT NOT NULL,

    MarketValue       DECIMAL(12,2) NOT NULL DEFAULT 0,   -- total appraised jewel value
    EligibleAmount    DECIMAL(12,2) NOT NULL DEFAULT 0,   -- market value * LTV
    LoanAmount        DECIMAL(12,2) NOT NULL DEFAULT 0,   -- amount actually granted
    ProcessingFee     DECIMAL(10,2) NOT NULL DEFAULT 0,

    OutstandingPrincipal DECIMAL(12,2) NOT NULL DEFAULT 0,
    OutstandingInterest  DECIMAL(12,2) NOT NULL DEFAULT 0,
    PenaltyAccrued       DECIMAL(12,2) NOT NULL DEFAULT 0,

    Status            VARCHAR(20) NOT NULL DEFAULT 'Draft',
        -- Draft / PendingApproval / Approved / Rejected / Disbursed / Active / Closed / Auctioned

    Remarks           NVARCHAR(500) NULL,

    CreatedBy         INT NULL REFERENCES Users(UserId),
    ApprovedBy        INT NULL REFERENCES Users(UserId),
    ApprovedAt        DATETIME2 NULL,
    DisbursedBy       INT NULL REFERENCES Users(UserId),
    DisbursedAt       DATETIME2 NULL,
    ClosedBy          INT NULL REFERENCES Users(UserId),
    ClosedAt          DATETIME2 NULL,

    CreatedAt         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2 NULL
);
GO
CREATE INDEX IX_Loans_CustomerId ON Loans(CustomerId);
CREATE INDEX IX_Loans_Status ON Loans(Status);
CREATE INDEX IX_Loans_MaturityDate ON Loans(MaturityDate);
GO

/* ============================================================
   9. JEWEL ITEMS (appraisal line items per loan)
   ============================================================ */
CREATE TABLE JewelItems (
    JewelItemId     INT IDENTITY(1,1) PRIMARY KEY,
    LoanId          INT NOT NULL REFERENCES Loans(LoanId) ON DELETE CASCADE,
    JewelTypeId     INT NOT NULL REFERENCES JewelTypes(JewelTypeId),
    Quantity        INT NOT NULL DEFAULT 1,
    GrossWeightGrams  DECIMAL(8,3) NOT NULL DEFAULT 0,
    StoneWeightGrams  DECIMAL(8,3) NOT NULL DEFAULT 0,
    NetWeightGrams    DECIMAL(8,3) NOT NULL DEFAULT 0,
    Purity            VARCHAR(20) NULL,
    MarketValue       DECIMAL(12,2) NOT NULL DEFAULT 0,
    PhotoPath         NVARCHAR(300) NULL,
    ConditionVerified BIT NOT NULL DEFAULT 0,
    CreatedAt         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
CREATE INDEX IX_JewelItems_LoanId ON JewelItems(LoanId);
GO

/* ============================================================
   10. LOAN TRANSACTIONS (collections, disbursement, renewal, closure)
   ============================================================ */
CREATE TABLE LoanTransactions (
    TransactionId     INT IDENTITY(1,1) PRIMARY KEY,
    LoanId            INT NOT NULL REFERENCES Loans(LoanId),
    TransactionType   VARCHAR(30) NOT NULL,
        -- Disbursement / InterestCollection / PrincipalCollection / Renewal / Closure
    ReceiptNumber     VARCHAR(30) NULL UNIQUE,
    TransactionDate   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    PrincipalAmount   DECIMAL(12,2) NOT NULL DEFAULT 0,
    InterestAmount    DECIMAL(12,2) NOT NULL DEFAULT 0,
    PenaltyAmount     DECIMAL(12,2) NOT NULL DEFAULT 0,
    ChargesAmount     DECIMAL(12,2) NOT NULL DEFAULT 0,
    TotalAmount       DECIMAL(12,2) NOT NULL DEFAULT 0,

    PaymentMode       VARCHAR(20) NULL,        -- Cash / UPI / BankTransfer
    ReferenceNo       VARCHAR(80) NULL,

    BalancePrincipalAfter DECIMAL(12,2) NULL,
    NextDueDate           DATE NULL,

    ProcessedBy       INT NULL REFERENCES Users(UserId),
    BranchId          INT NULL REFERENCES Branches(BranchId),

    Remarks           NVARCHAR(300) NULL,
    CreatedAt         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
CREATE INDEX IX_LoanTransactions_LoanId ON LoanTransactions(LoanId);
CREATE INDEX IX_LoanTransactions_Type ON LoanTransactions(TransactionType);
CREATE INDEX IX_LoanTransactions_Date ON LoanTransactions(TransactionDate);
GO

/* ============================================================
   11. JEWEL RELEASE (release record after closure)
   ============================================================ */
CREATE TABLE JewelReleases (
    JewelReleaseId    INT IDENTITY(1,1) PRIMARY KEY,
    LoanId            INT NOT NULL REFERENCES Loans(LoanId),
    ReleaseDate       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ReleasedBy        INT NULL REFERENCES Users(UserId),
    CustomerAcknowledged BIT NOT NULL DEFAULT 0,
    SignaturePath     NVARCHAR(300) NULL,
    Remarks           NVARCHAR(300) NULL
);
GO

/* ============================================================
   12. AUCTIONS
   ============================================================ */
CREATE TABLE Auctions (
    AuctionId         INT IDENTITY(1,1) PRIMARY KEY,
    LoanId            INT NOT NULL REFERENCES Loans(LoanId),
    OverdueDays       INT NOT NULL DEFAULT 0,
    Status            VARCHAR(20) NOT NULL DEFAULT 'Eligible',
        -- Eligible / NoticeSent / Approved / Scheduled / Completed / Cancelled
    NoticeSentAt      DATETIME2 NULL,
    ApprovedBy        INT NULL REFERENCES Users(UserId),
    ApprovedAt        DATETIME2 NULL,
    AuctionDate       DATE NULL,
    RecoveredAmount   DECIMAL(12,2) NULL,
    Remarks           NVARCHAR(300) NULL,
    CreatedAt         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

/* ============================================================
   13. ROLE PERMISSIONS (module-level access matrix)
   ============================================================ */
CREATE TABLE Permissions (
    PermissionId      INT IDENTITY(1,1) PRIMARY KEY,
    ModuleName        NVARCHAR(80) NOT NULL,     -- e.g. "Loan Operations"
    ActionName        NVARCHAR(120) NOT NULL,    -- e.g. "Approve Loan"
    ActionKey         VARCHAR(80) NOT NULL UNIQUE -- e.g. "loan.approve"
);
GO

CREATE TABLE RolePermissions (
    RoleId            INT NOT NULL REFERENCES Roles(RoleId),
    PermissionId      INT NOT NULL REFERENCES Permissions(PermissionId),
    IsAllowed         BIT NOT NULL DEFAULT 0,
    PRIMARY KEY (RoleId, PermissionId)
);
GO

/* ============================================================
   14. AUDIT LOGS
   ============================================================ */
CREATE TABLE AuditLogs (
    AuditLogId        BIGINT IDENTITY(1,1) PRIMARY KEY,
    EventTime         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UserId            INT NULL REFERENCES Users(UserId),
    ModuleName        NVARCHAR(80) NULL,
    ActionDescription NVARCHAR(300) NOT NULL,
    RecordReference   NVARCHAR(60) NULL,         -- e.g. loan number, customer code
    IpAddress         VARCHAR(45) NULL
);
GO
CREATE INDEX IX_AuditLogs_EventTime ON AuditLogs(EventTime);
GO

/* ============================================================
   15. SYSTEM SETTINGS (key-value config)
   ============================================================ */
CREATE TABLE SystemSettings (
    SettingKey        VARCHAR(100) PRIMARY KEY,
    SettingValue      NVARCHAR(500) NULL,
    Category          VARCHAR(50) NULL,          -- Interest / Penalty / Renewal / Auction / Notification / Security
    Description       NVARCHAR(300) NULL,
    UpdatedAt         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

/* ============================================================
   DONE — Database is empty and ready.
   No INSERT statements included on purpose, per request.
   ============================================================ */
PRINT 'JLMS_DB schema created successfully. All tables are empty.';
GO
