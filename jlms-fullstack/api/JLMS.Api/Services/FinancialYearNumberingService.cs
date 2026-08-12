using JLMS.Api.Data;
using JLMS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace JLMS.Api.Services;

// Drives Loan Number / Customer Code generation off the FinancialYear master table
// instead of the hardcoded prefixes that used to live in LoanCalculationService.
public class FinancialYearNumberingService
{
    public const string TypeLoanNumber = "LoanNumber";
    public const string TypeCustomerCode = "CustomerCode";

    // Branch 2 gets its own LOAN NUMBER scheme only — CustomerCode for branch 2
    // still goes through the normal FinancialYear-driven path below, untouched.
    private const int Branch2Id = 100;
    private const int Branch2BlockSize = 10000;

    private readonly JlmsDbContext _db;
    public FinancialYearNumberingService(JlmsDbContext db) => _db = db;

    // Active row for a given series type, covering "today" (UTC date), optionally filtered by branch.
    public async Task<FinancialYear?> GetActiveAsync(string goldLoanType, DateTime? asOf = null, int? branchId = null)
    {
        var date = (asOf ?? DateTime.UtcNow).Date;
        var query = _db.FinancialYears.AsNoTracking()
            .Where(f => f.GoldLoanType == goldLoanType
                     && f.Status == "A"
                     && f.FromDt.Date <= date
                     && f.ToDt.Date >= date);

        if (branchId.HasValue)
            query = query.Where(f => f.BranchId == branchId.Value);

        return await query
            .OrderByDescending(f => f.FromDt)
            .FirstOrDefaultAsync();
    }

    // ---------------- LOAN NUMBER ----------------
    public async Task<string> GenerateNextLoanNumberAsync(int? branchId = null, DateTime? asOf = null)
    {
        // Branch 2 only: fixed numeric -> A1..Z10000 scheme, independent of FinancialYear master.
        if (branchId.HasValue && branchId.Value == Branch2Id)
            return await GenerateNextBranch2LoanNumberAsync();

        // All other branches: unchanged Financial-Year-driven behavior.
        var fy = await GetActiveAsync(TypeLoanNumber, asOf, branchId);
        if (fy == null)
            throw new InvalidOperationException(
                $"No active Financial Year configured for '{TypeLoanNumber}' covering {(asOf ?? DateTime.UtcNow).Date:d}" +
                (branchId.HasValue ? $" for branch {branchId.Value}" : "") +
                ". Add one on the Financial Year screen.");

        // Find the last loan number for this financial year/branch to start from the correct sequence
        var lastLoan = await _db.Loans.AsNoTracking()
            .Where(l => l.LoanNumber.StartsWith(fy.Prefix))
            .Where(l => branchId.HasValue ? l.BranchId == branchId.Value : true)
            .OrderByDescending(l => l.LoanNumber)
            .FirstOrDefaultAsync();

        int seq;
        if (lastLoan != null && lastLoan.LoanNumber.Length >= fy.Prefix.Length + 5)
        {
            // Extract the 5-digit sequence from the last loan number
            var seqStr = lastLoan.LoanNumber.Substring(fy.Prefix.Length, 5);
            if (int.TryParse(seqStr, out int lastSeq))
                seq = lastSeq + 1;
            else
                seq = fy.GoldLoanNoStartsFrom;
        }
        else
        {
            seq = fy.GoldLoanNoStartsFrom;
        }

        string candidate;
        do
        {
            candidate = BuildNumber(fy.Prefix, fy.Suffix, seq);
            seq++;
        }
        while (await _db.Loans.AnyAsync(l => l.LoanNumber == candidate));

        return candidate;
    }

    // ---------------- CUSTOMER CODE (unchanged — same for every branch, including branch 2) ----------------
    public async Task<string> GenerateNextCustomerCodeAsync(int? branchId = null, DateTime? asOf = null)
    {
        var fy = await GetActiveAsync(TypeCustomerCode, asOf, branchId);
        if (fy == null)
            throw new InvalidOperationException(
                $"No active Financial Year configured for '{TypeCustomerCode}' covering {(asOf ?? DateTime.UtcNow).Date:d}" +
                (branchId.HasValue ? $" for branch {branchId.Value}" : "") +
                ". Add one on the Financial Year screen.");

        var seq = fy.GoldLoanNoStartsFrom;
        string candidate;
        do
        {
            candidate = BuildNumber(fy.Prefix, fy.Suffix, seq);
            seq++;
        }
        while (await _db.Customers.AnyAsync(c => c.CustomerCode == candidate));

        return candidate;
    }

    // Prefix + zero-padded 5-digit sequence + optional suffix, e.g. "BR2627" + "00001" -> "BR262700001"
    private static string BuildNumber(string prefix, string? suffix, int seq)
        => $"{prefix}{seq:D5}{suffix}";

    // --- Branch 2 special LOAN NUMBER scheme ---
    // n = 1..10000        -> "000001".."010000"  (plain 6-digit number)
    // n = 10001..20000    -> "A1".."A10000"
    // n = 20001..30000    -> "B1".."B10000"
    // ...
    // n = 250001..260000  -> "Z1".."Z10000"
    private async Task<string> GenerateNextBranch2LoanNumberAsync()
    {
        var n = await _db.Loans.CountAsync(l => l.BranchId == Branch2Id) + 1;

        string candidate;
        do
        {
            candidate = BuildBranch2Number(n);
            n++;
        }
        while (await _db.Loans.AnyAsync(l => l.LoanNumber == candidate));

        return candidate;
    }

    private static string BuildBranch2Number(int n)
    {
        var blockIndex = (n - 1) / Branch2BlockSize;      // 0 = numeric block, 1 = 'A', 2 = 'B', ...
        var localSeq = ((n - 1) % Branch2BlockSize) + 1;  // 1..10000 within the block

        if (blockIndex == 0)
            return localSeq.ToString("D6");

        var letterIndex = blockIndex - 1; // 0 -> 'A'
        if (letterIndex > 25)
            throw new InvalidOperationException(
                "Branch 2 loan number sequence exhausted (passed Z10000). Configure a new numbering scheme.");

        var letter = (char)('A' + letterIndex);
        return $"{letter}{localSeq}";
    }
}