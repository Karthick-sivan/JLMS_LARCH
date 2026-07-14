using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.DTOs;

namespace JLMS.Api.Controllers.Reports;

[ApiController]
[Route("api/closed-loans-report")]
public class ClosedLoansReportController : ControllerBase
{
    private readonly JlmsDbContext _db;

    public ClosedLoansReportController(JlmsDbContext db)
    {
        _db = db;
    }

    // NOTE: assumes the closure transaction is written with TransactionType == "Closure".
    // Adjust this constant if your LoanOperationsService uses a different literal.
    private const string ClosureTransactionType = "Closure";
    private static readonly string[] CollectionTypes = { "PrincipalCollection", "InterestCollection", "LoanOpsPayment", ClosureTransactionType };

    // GET /api/closed-loans-report?loanNo=&customerName=&mobile=&aadhaar=&loanSchemeId=&jewelTypeId=&closureFromDate=&closureToDate=&closedByUserId=&branchId=&page=1&pageSize=25
    [HttpGet]
    public async Task<ActionResult<ClosedLoanReportPagedDto>> Get(
        [FromQuery] string? loanNo,
        [FromQuery] string? customerName,
        [FromQuery] string? mobile,
        [FromQuery] string? aadhaar,
        [FromQuery] int? loanSchemeId,
        [FromQuery] int? jewelTypeId,
        [FromQuery] DateTime? closureFromDate,
        [FromQuery] DateTime? closureToDate,
        [FromQuery] int? closedByUserId,
        [FromQuery] int? branchId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var query = _db.Loans
            .AsNoTracking()
            .Include(l => l.Customer)
            .Include(l => l.LoanScheme)
            .Include(l => l.JewelItems).ThenInclude(ji => ji.JewelType)
            .Where(l => l.Status == "Closed")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(loanNo))
            query = query.Where(l => l.LoanNumber.Contains(loanNo));

        if (!string.IsNullOrWhiteSpace(customerName))
            query = query.Where(l => l.Customer != null && l.Customer.CustomerName.Contains(customerName));

        if (!string.IsNullOrWhiteSpace(mobile))
            query = query.Where(l => l.Customer != null && l.Customer.Mobile.Contains(mobile));

        if (!string.IsNullOrWhiteSpace(aadhaar))
            query = query.Where(l => l.Customer != null && l.Customer.AadhaarNumber != null && l.Customer.AadhaarNumber.Contains(aadhaar));

        if (loanSchemeId.HasValue && loanSchemeId.Value > 0)
            query = query.Where(l => l.LoanSchemeId == loanSchemeId.Value);

        if (jewelTypeId.HasValue && jewelTypeId.Value > 0)
            query = query.Where(l => l.JewelItems.Any(ji => ji.JewelTypeId == jewelTypeId.Value));

        if (closureFromDate.HasValue)
            query = query.Where(l => l.ClosedAt.HasValue && l.ClosedAt.Value.Date >= closureFromDate.Value.Date);

        if (closureToDate.HasValue)
            query = query.Where(l => l.ClosedAt.HasValue && l.ClosedAt.Value.Date <= closureToDate.Value.Date);

        if (closedByUserId.HasValue && closedByUserId.Value > 0)
            query = query.Where(l => l.ClosedBy == closedByUserId.Value);

        if (branchId.HasValue && branchId.Value > 0)
            query = query.Where(l => l.BranchId == branchId.Value);

        var totalCount = await query.CountAsync();

        var totalPrincipalDisbursed = await query.SumAsync(l => l.LoanAmount);

        // Full matching id set — used both for footer totals and to avoid a second
        // per-page transactions query.
        var allMatchingLoanIds = await query.Select(l => l.LoanId).ToListAsync();

        var allTx = await _db.LoanTransactions
            .AsNoTracking()
            .Where(t => allMatchingLoanIds.Contains(t.LoanId) && CollectionTypes.Contains(t.TransactionType))
            .ToListAsync();

        var totalInterestCollected = allTx.Sum(t => t.InterestAmount);
        var totalPrincipalCollected = allTx.Sum(t => t.PrincipalAmount);
        var grandTotalCollected = allTx.Sum(t => t.TotalAmount);

        var loans = await query
            .OrderByDescending(l => l.ClosedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var branchNames = await _db.Branches.AsNoTracking().ToDictionaryAsync(b => b.BranchId, b => b.BranchName);
        var userNames = await _db.Users.AsNoTracking().ToDictionaryAsync(u => u.UserId, u => u.FullName);

        var items = loans.Select(l =>
        {
            var loanTx = allTx.Where(t => t.LoanId == l.LoanId).ToList();
            var closureTx = loanTx.Where(t => t.TransactionType == ClosureTransactionType)
                                   .OrderByDescending(t => t.TransactionDate)
                                   .FirstOrDefault();

            var interestCollected = loanTx.Sum(t => t.InterestAmount);
            var principalCollected = loanTx.Sum(t => t.PrincipalAmount);
            var totalCollected = loanTx.Sum(t => t.TotalAmount);

            var jewelTypeNames = l.JewelItems.Select(ji => ji.JewelType?.JewelTypeName ?? "").Where(n => n != "").Distinct();
            var purities = l.JewelItems.Select(ji => ji.Purity ?? "").Where(p => p != "").Distinct();

            var durationDays = (l.ClosedAt.HasValue && l.LoanDate.HasValue)
                ? (int)(l.ClosedAt.Value.Date - l.LoanDate.Value.Date).TotalDays
                : 0;

            return new ClosedLoanReportRowDto(
                LoanId: l.LoanId,
                LoanNumber: l.LoanNumber,
                CustomerCode: l.Customer?.CustomerCode ?? "",
                CustomerName: l.Customer?.CustomerName ?? "",
                Mobile: l.Customer?.Mobile ?? "",
                SchemeDisplay: l.LoanScheme != null ? $"{l.LoanScheme.SchemeName} ({l.LoanScheme.InterestRatePct}% - {l.LoanScheme.TenureMonths}m)" : "",
                LoanDate: l.LoanDate,
                MaturityDate: l.MaturityDate,
                ClosureDate: l.ClosedAt,
                LoanDurationDays: durationDays,
                JewelTypes: string.Join(", ", jewelTypeNames),
                GrossWeight: l.JewelItems.Sum(ji => ji.GrossWeightGrams),
                NetWeight: l.JewelItems.Sum(ji => ji.NetWeightGrams),
                Purity: string.Join(", ", purities),
                PrincipalAmount: l.LoanAmount,
                OverallInterest: l.OverallInterest,
                TotalInterestCollected: interestCollected,
                TotalPrincipalCollected: principalCollected,
                TotalAmountCollected: totalCollected,
                ClosureCharges: closureTx?.ChargesAmount ?? 0,
                FinalSettlementAmount: closureTx?.TotalAmount ?? 0,
                PaymentMode: closureTx?.PaymentMode,
                ReferenceNo: closureTx?.ReferenceNo,
                ClosedByName: l.ClosedBy.HasValue && userNames.TryGetValue(l.ClosedBy.Value, out var un) ? un : null,
                BranchName: branchNames.TryGetValue(l.BranchId, out var bn) ? bn : "",
                ClosureReceiptNo: closureTx?.ReceiptNumber,
                Status: l.Status,
                Remarks: l.Remarks
            );
        }).ToList();

        var result = new ClosedLoanReportPagedDto(
            Items: items,
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            TotalClosedLoans: totalCount,
            TotalPrincipalDisbursed: totalPrincipalDisbursed,
            TotalPrincipalCollected: totalPrincipalCollected,
            TotalInterestCollected: totalInterestCollected,
            GrandTotalCollected: grandTotalCollected
        );

        return Ok(result);
    }

    [HttpGet("customer-search")]
    public async Task<ActionResult<IEnumerable<object>>> CustomerSearch([FromQuery] string? q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(Array.Empty<object>());

        var customers = await _db.Customers
            .AsNoTracking()
            .Where(c => c.CustomerName.Contains(q) || c.CustomerCode.Contains(q) || c.Mobile.Contains(q))
            .OrderBy(c => c.CustomerName)
            .Take(20)
            .Select(c => new { c.CustomerId, c.CustomerCode, c.CustomerName, c.Mobile })
            .ToListAsync();

        return Ok(customers);
    }
}