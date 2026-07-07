/* ============================================================
   JLMS — OPTIONAL Reference Data Seed
   ============================================================
   This script is OPTIONAL. Run it only if you want the dropdowns
   (Branch, Role, Loan Scheme, Jewel Type) to have at least one
   option to pick from while testing.

   This does NOT insert any Customers, Loans, Transactions, or
   other "real" business data — those tables stay completely
   empty so you can test creating everything from scratch.

   Safe to skip this script entirely if you want a 100% blank
   database, including blank dropdowns (you'd then need to add
   branches/roles/schemes yourself via the Masters screens or
   directly in SSMS before testing transactions).
   ============================================================ */

USE JLMS_DB;
GO

-- ---- Branches (minimum: one branch is required to create users/loans) ----
IF NOT EXISTS (SELECT 1 FROM Branches)
BEGIN
    INSERT INTO Branches (BranchCode, BranchName, City, State, IsActive)
    VALUES ('BR-001', N'Madurai Main Branch', N'Madurai', N'Tamil Nadu', 1);
END
GO

-- ---- Roles ----
IF NOT EXISTS (SELECT 1 FROM Roles)
BEGIN
    INSERT INTO Roles (RoleName, Description, IsActive) VALUES
    (N'Branch Manager', N'Full branch operations, approvals & reports', 1),
    (N'Loan Officer',   N'Customer registration, appraisal & loan entry', 1),
    (N'Cashier',        N'Collections, disbursements & receipts', 1),
    (N'Administrator',  N'Full system access incl. settings & audit', 1);
END
GO

-- ---- Jewel Types ----
IF NOT EXISTS (SELECT 1 FROM JewelTypes)
BEGIN
    INSERT INTO JewelTypes (JewelTypeName, Category, DefaultPurity, WastagePercent, IsActive) VALUES
    (N'Gold Chain',    'Ornament', '22K', 2.0, 1),
    (N'Gold Bangle',   'Ornament', '22K', 1.5, 1),
    (N'Gold Ring',     'Ornament', '22K', 2.5, 1),
    (N'Gold Earrings', 'Ornament', '22K', 2.0, 1),
    (N'Gold Coin',     'Coin',     '24K', 0.5, 1);
END
GO

-- ---- Loan Schemes ----
IF NOT EXISTS (SELECT 1 FROM LoanSchemes)
BEGIN
    INSERT INTO LoanSchemes (SchemeName, InterestRatePct, TenureMonths, MaxLtvPercent, ProcessingFee, PenaltyRatePerDay, IsActive) VALUES
    (N'Gold Plus',    12.00, 12, 75.00, 750.00, 0.05, 1),
    (N'Gold Saver',   10.00, 6,  70.00, 500.00, 0.05, 1),
    (N'Gold Express', 15.00, 3,  80.00, 250.00, 0.05, 1);
END
GO

-- ---- Today's Gold Rate (required before any jewel appraisal can be calculated) ----
IF NOT EXISTS (SELECT 1 FROM GoldRates WHERE EffectiveDate = CAST(SYSUTCDATETIME() AS DATE))
BEGIN
    INSERT INTO GoldRates (EffectiveDate, Rate24K, Rate22K, Rate18K)
    VALUES (CAST(SYSUTCDATETIME() AS DATE), 6765.00, 6200.00, 5074.00);
END
GO

-- ---- One Administrator login so you can sign in on day 1 ----
-- Username: admin   Password: Admin@123
-- (PasswordHash below is a SHA256 placeholder — see README for how the API
--  validates this; change it immediately after first login)
IF NOT EXISTS (SELECT 1 FROM Users)
BEGIN
    INSERT INTO Users (EmployeeCode, FullName, Username, PasswordHash, RoleId, BranchId, IsActive)
    SELECT 'EMP-0001', N'System Administrator', 'admin',
           'E86F78A8A3CAF0B60D8E74E5942AA6D86DC150CD3C03338AEF25B7D2D7E3ACC7',  -- SHA256("Admin@123")
           (SELECT TOP 1 RoleId FROM Roles WHERE RoleName = N'Administrator'),
           (SELECT TOP 1 BranchId FROM Branches),
           1;
END
GO

PRINT 'Reference data seeded. Customers, Loans, and Transactions tables remain empty.';
GO
