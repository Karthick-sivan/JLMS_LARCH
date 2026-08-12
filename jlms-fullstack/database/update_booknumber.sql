-- Update BookNo column in Loans table
-- Logic:
-- 1. If BranchId = 2, then BookNo = LoanNumber (full loan number)
-- 2. Otherwise, extract the numeric suffix from LoanNumber (e.g., 'BR262700001' -> '1')

USE JLMS_DB;
GO

UPDATE Loans
SET BookNo = 
    CASE 
        WHEN BranchId = 2 THEN LoanNumber
        ELSE CAST(
            CAST(
                RIGHT(LoanNumber, PATINDEX('%[0-9]%', REVERSE(LoanNumber))) 
            AS INT) 
        AS VARCHAR(10))
    END;
GO

-- Verify the update
SELECT LoanId, LoanNumber, BranchId, BookNo
FROM Loans
ORDER BY LoanId;
GO
