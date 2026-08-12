-- Convert BookNo to a computed column (auto-calculated on insert/update)
-- This replaces the manual column with a computed one

USE JLMS_DB;
GO

-- First, drop the existing BookNo column if it exists
ALTER TABLE Loans DROP COLUMN IF EXISTS BookNo;
GO

-- Add BookNo as a computed column
ALTER TABLE Loans
ADD BookNo AS (CASE
                    WHEN BranchId = 2 THEN LoanNumber
                    ELSE CAST(CAST(RIGHT(LoanNumber, PATINDEX('%[0-9]%', REVERSE(LoanNumber))) AS INT) AS VARCHAR(10))
                  END) PERSISTED;
GO

-- Verify the computed column works
SELECT LoanId, LoanNumber, BranchId, BookNo
FROM Loans
ORDER BY LoanId;
GO

-- Test with a sample insert (uncomment to test)
-- INSERT INTO Loans (LoanNumber, CustomerId, LoanSchemeId, BranchId, LoanDate, MaturityDate, InterestRatePct, TenureMonths, MarketValue, EligibleAmount, LoanAmount, ProcessingFee, OutstandingPrincipal, OutstandingInterest, PenaltyAccrued, Status)
-- VALUES ('BR262700010', 1, 1, 1, GETDATE(), DATEADD(MONTH, 12, GETDATE()), 12.5, 12, 50000, 40000, 40000, 500, 40000, 0, 0, 'Draft');
-- SELECT LoanId, LoanNumber, BranchId, BookNo FROM Loans WHERE LoanNumber = 'BR262700010';
