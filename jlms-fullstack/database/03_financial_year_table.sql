/* ============================================================
   JLMS — Financial Year Table
   Master table that drives numbering series (Loan Number / Customer Code) per financial year.
   One row per (Code, GoldLoanType) — e.g. Code="2026-2027", GoldLoanType="LoanNumber", Prefix="BR2627".
   ============================================================ */

USE JLMS_DB;
GO

-- Drop table if exists (for clean recreation)
IF OBJECT_ID('dbo.FinancialYear', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.FinancialYear;
END
GO

/* ============================================================
   FINANCIAL YEAR TABLE
   ============================================================ */
CREATE TABLE FinancialYear (
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    Code                VARCHAR(20)   NOT NULL,       -- e.g. "2026-2027"
    GoldLoanType        VARCHAR(30)   NOT NULL,       -- "LoanNumber" or "CustomerCode"
    FromDt              DATE          NOT NULL,
    ToDt                DATE          NOT NULL,
    GoldLoanNoStarts    INT           NOT NULL DEFAULT 1,  -- First sequence number to use
    Prefix              VARCHAR(20)   NOT NULL,
    Suffix              VARCHAR(20)   NULL,
    Status              VARCHAR(1)    NOT NULL DEFAULT 'A',  -- "A" = Active, "I" = Inactive
    History             NVARCHAR(MAX) NULL,
    CreatedDt           DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy           INT           NULL,
    UpdatedDt           DATETIME2     NULL,
    UpdatedBy           INT           NULL,
    BranchId            INT           NULL
);
GO

-- Create index for efficient lookups by branch, type, and status
CREATE INDEX IX_FinancialYear_BranchTypeStatus ON FinancialYear(BranchId, GoldLoanType, Status);
GO

-- Create index for date range queries
CREATE INDEX IX_FinancialYear_DateRange ON FinancialYear(FromDt, ToDt);
GO

PRINT 'FinancialYear table created successfully with IDENTITY column on Id.';
GO
