-- Convert BookNo from computed column to regular column
-- This allows the C# application to set BookNo manually using the ExtractBookNo method
-- The logic extracts the last 5 digits (book number portion) and converts to integer
-- Examples:
-- BR262700001 → BookNo = 1
-- BR262700010 → BookNo = 10
-- BR262700100 → BookNo = 100
-- BR262701000 → BookNo = 1000
-- BR262710000 → BookNo = 10000

USE JLMS_DB;
GO

-- Drop the existing computed column
ALTER TABLE Loans DROP COLUMN IF EXISTS BookNo;
GO

-- Add BookNo as a regular column (not computed)
ALTER TABLE Loans ADD BookNo VARCHAR(10);
GO

-- Populate BookNo for existing loans
-- Extract last 5 digits (the book number portion) and convert to integer to strip leading zeros
UPDATE Loans
SET BookNo = CASE
                WHEN BranchId = 2 THEN LoanNumber
                ELSE CAST(RIGHT(LoanNumber, 5) AS INT)
             END
WHERE BookNo IS NULL;
GO

-- Verify the column works correctly
SELECT LoanId, LoanNumber, BranchId, BookNo
FROM Loans
ORDER BY LoanId;
GO
