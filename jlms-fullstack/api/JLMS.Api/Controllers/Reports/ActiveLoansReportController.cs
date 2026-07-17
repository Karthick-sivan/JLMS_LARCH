using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.DTOs;

namespace JLMS.Api.Controllers.Reports;

[ApiController]
[Route("api/active-loans-report")]
public class ActiveLoansReportController : ControllerBase
{
    private readonly JlmsDbContext _db;

    public ActiveLoansReportController(JlmsDbContext db)
    {
        _db = db;
    }

    private static readonly string[] PaymentTypes = { "PrincipalCollection", "InterestCollection", "LoanOpsPayment" };

    // GET /api/active-loans-report?loanNo=&customerName=&mobile=&aadhaar=&loanSchemeId=&jewelTypeId=&fromDate=&toDate=&branchId=&status=&page=1&pageSize=25
    [HttpGet]
    public async Task<ActionResult<ActiveLoanReportPagedDto>> Get(
        [FromQuery] string? loanNo,
        [FromQuery] string? customerName,
        [FromQuery] string? mobile,
        [FromQuery] string? aadhaar,
        [FromQuery] int? loanSchemeId,
        [FromQuery] int? jewelTypeId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int? branchId,
        [FromQuery] string? status,      // derived sub-filter: Active | Due | Overdue
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var query = _db.Loans
            .AsNoTracking()
            .Include(l => l.Customer)
            .Include(l => l.LoanScheme)
            .Include(l => l.JewelItems).ThenInclude(ji => ji.JewelType)
            .Where(l => l.Status == "Active")
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

        if (fromDate.HasValue)
            query = query.Where(l => l.LoanDate.HasValue && l.LoanDate.Value.Date >= fromDate.Value.Date);

        if (toDate.HasValue)
            query = query.Where(l => l.LoanDate.HasValue && l.LoanDate.Value.Date <= toDate.Value.Date);

        var user = HttpContext.Items["CurrentUser"] as JLMS.Api.Models.User;
        var filterBranchId = user?.GetFilterBranchId() ?? branchId;
        if (filterBranchId.HasValue && filterBranchId.Value > 0)
            query = query.Where(l => l.BranchId == filterBranchId.Value);

        var totalCount = await query.CountAsync();

        // Footer totals — computed on the full filtered set (direct Loan columns, cheap)
        var totals = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalPrincipal = g.Sum(l => l.LoanAmount),
                TotalOverallInterest = g.Sum(l => l.OverallInterest),
                TotalOutPrincipal = g.Sum(l => l.OutstandingPrincipal),
                TotalOutInterest = g.Sum(l => l.OutstandingInterest)
            })
            .FirstOrDefaultAsync();

        // Pull the filtered set into memory — needed because "Status" (Active/Due/Overdue)
        // is a derived value based on MaturityDate, not a stored column.
        var loans = await query
            .OrderByDescending(l => l.LoanDate)
            .ThenByDescending(l => l.CreatedAt)
            .ToListAsync();

        var branchNames = await _db.Branches.AsNoTracking().ToDictionaryAsync(b => b.BranchId, b => b.BranchName);

        var loanIds = loans.Select(l => l.LoanId).ToList();
        var lastPayments = await _db.LoanTransactions
            .AsNoTracking()
            .Where(t => loanIds.Contains(t.LoanId) && PaymentTypes.Contains(t.TransactionType))
            .GroupBy(t => t.LoanId)
            .Select(g => new { LoanId = g.Key, LastDate = g.Max(t => t.TransactionDate) })
            .ToDictionaryAsync(x => x.LoanId, x => x.LastDate);

        var today = DateTime.UtcNow.Date;

        var allRows = loans.Select(l =>
        {
            var daysOverdue = (l.MaturityDate.HasValue && l.MaturityDate.Value.Date < today)
                ? (int)(today - l.MaturityDate.Value.Date).TotalDays
                : 0;

            // NOTE: "Due" window is set to 7 days before maturity — adjust to match your
            // actual due-date business rule if different.
            var derivedStatus = daysOverdue > 0
                ? "Overdue"
                : (l.MaturityDate.HasValue && l.MaturityDate.Value.Date <= today.AddDays(7) ? "Due" : "Active");

            var jewelTypeNames = l.JewelItems.Select(ji => ji.JewelType?.JewelTypeName ?? "").Where(n => n != "").Distinct();
            var purities = l.JewelItems.Select(ji => ji.Purity ?? "").Where(p => p != "").Distinct();

            return new ActiveLoanReportRowDto(
                LoanId: l.LoanId,
                LoanNumber: l.LoanNumber,
                CustomerCode: l.Customer?.CustomerCode ?? "",
                CustomerName: l.Customer?.CustomerName ?? "",
                Mobile: l.Customer?.Mobile ?? "",
                SchemeDisplay: l.LoanScheme != null ? $"{l.LoanScheme.SchemeName} ({l.LoanScheme.InterestRatePct}% - {l.LoanScheme.TenureMonths}m)" : "",
                LoanDate: l.LoanDate,
                MaturityDate: l.MaturityDate,
                JewelTypes: string.Join(", ", jewelTypeNames),
                GrossWeight: l.JewelItems.Sum(ji => ji.GrossWeightGrams),
                NetWeight: l.JewelItems.Sum(ji => ji.NetWeightGrams),
                Purity: string.Join(", ", purities),
                PrincipalAmount: l.LoanAmount,
                OverallInterest: l.OverallInterest,
                OutstandingPrincipal: l.OutstandingPrincipal,
                OutstandingInterest: l.OutstandingInterest,
                TotalOutstanding: l.OutstandingPrincipal + l.OutstandingInterest,
                LastPaymentDate: lastPayments.TryGetValue(l.LoanId, out var lp) ? lp : (DateTime?)null,
                DaysOverdue: daysOverdue,
                Status: derivedStatus,
                BranchName: branchNames.TryGetValue(l.BranchId, out var bn) ? bn : ""
            );
        }).ToList();

        if (!string.IsNullOrWhiteSpace(status))
            allRows = allRows.Where(r => string.Equals(r.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();

        var pagedItems = allRows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var result = new ActiveLoanReportPagedDto(
            Items: pagedItems,
            TotalCount: allRows.Count,
            Page: page,
            PageSize: pageSize,
            TotalActiveLoans: totalCount,
            TotalPrincipal: totals?.TotalPrincipal ?? 0,
            TotalOverallInterest: totals?.TotalOverallInterest ?? 0,
            TotalOutstandingPrincipal: totals?.TotalOutPrincipal ?? 0,
            TotalOutstandingInterest: totals?.TotalOutInterest ?? 0,
            GrandOutstanding: (totals?.TotalOutPrincipal ?? 0) + (totals?.TotalOutInterest ?? 0)
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