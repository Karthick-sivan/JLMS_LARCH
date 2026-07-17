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

    private readonly JlmsDbContext _db;

    public FinancialYearNumberingService(JlmsDbContext db) => _db = db;

    // Active row for a given series type, covering "today" (UTC date).
    public async Task<FinancialYear?> GetActiveAsync(string goldLoanType, DateTime? asOf = null)
    {
        var date = (asOf ?? DateTime.UtcNow).Date;
        return await _db.FinancialYears.AsNoTracking()
            .Where(f => f.GoldLoanType == goldLoanType
                     && f.Status == "A"
                     && f.FromDt.Date <= date
                     && f.ToDt.Date >= date)
            .OrderByDescending(f => f.FromDt)
            .FirstOrDefaultAsync();
    }

    public async Task<string> GenerateNextLoanNumberAsync(DateTime? asOf = null)
    {
        var fy = await GetActiveAsync(TypeLoanNumber, asOf);
        if (fy == null)
            throw new InvalidOperationException(
                $"No active Financial Year configured for '{TypeLoanNumber}' covering {(asOf ?? DateTime.UtcNow).Date:d}. " +
                "Add one on the Financial Year screen.");

        var seq = fy.GoldLoanNoStartsFrom;
        string candidate;
        do
        {
            candidate = BuildNumber(fy.Prefix, fy.Suffix, seq);
            seq++;
        }
        while (await _db.Loans.AnyAsync(l => l.LoanNumber == candidate));

        return candidate;
    }

    public async Task<string> GenerateNextCustomerCodeAsync(DateTime? asOf = null)
    {
        var fy = await GetActiveAsync(TypeCustomerCode, asOf);
        if (fy == null)
            throw new InvalidOperationException(
                $"No active Financial Year configured for '{TypeCustomerCode}' covering {(asOf ?? DateTime.UtcNow).Date:d}. " +
                "Add one on the Financial Year screen.");

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
}
