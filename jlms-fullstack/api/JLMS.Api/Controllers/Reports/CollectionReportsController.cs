using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.DTOs;

namespace JLMS.Api.Controllers.Reports;

[ApiController]
[Route("api/collection-reports")]
public class CollectionReportsController : ControllerBase
{
    private readonly JlmsDbContext _db;

    public CollectionReportsController(JlmsDbContext db)
    {
        _db = db;
    }

    private static readonly string[] CollectionTypes = { "PrincipalCollection", "InterestCollection", "LoanOpsPayment" };

    // GET /api/collection-reports?fromDate=2026-01-01&toDate=2026-12-31&customerId=5&page=1&pageSize=25
    [HttpGet]
    public async Task<ActionResult<CollectionReportPagedDto>> GetCollectionReport(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int? customerId,
          [FromQuery] int? loanId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var query = _db.LoanTransactions
            .AsNoTracking()
            .Include(t => t.Loan)
                .ThenInclude(l => l.Customer)
            .Include(t => t.Loan)
                .ThenInclude(l => l.LoanScheme)
            .Where(t => CollectionTypes.Contains(t.TransactionType))
            .AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(t => t.TransactionDate.Date >= fromDate.Value.Date);

        if (toDate.HasValue)
            query = query.Where(t => t.TransactionDate.Date <= toDate.Value.Date);

        if (customerId.HasValue && customerId.Value > 0)
            query = query.Where(t => t.Loan.CustomerId == customerId.Value);

        if (loanId.HasValue && loanId.Value > 0)        
            query = query.Where(t => t.LoanId == loanId.Value);

        var totalCount = await query.CountAsync();

        var totals = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalPrincipal = g.Sum(t => t.PrincipalAmount),
                TotalInterest = g.Sum(t => t.InterestAmount),
                TotalCollected = g.Sum(t => t.TotalAmount)
            })
            .FirstOrDefaultAsync();

        var transactions = await query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.TransactionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = transactions.Select(t => new CollectionReportRowDto(
            TransactionId: t.TransactionId,
            LoanId: t.LoanId,
            LoanNumber: t.Loan?.LoanNumber ?? "",
            LoanDate: t.Loan?.LoanDate,
            CustomerName: t.Loan?.Customer?.CustomerName ?? "",
            CustomerCode: t.Loan?.Customer?.CustomerCode ?? "",
            CustomerMobile: t.Loan?.Customer?.Mobile ?? "",
            SchemeName: t.Loan?.LoanScheme?.SchemeName ?? "",
            TransactionDate: t.TransactionDate,
            TransactionType: t.TransactionType,
            LoanAmount: t.Loan?.LoanAmount ?? 0,
            PrincipalAmount: t.PrincipalAmount,
            InterestAmount: t.InterestAmount,
            TotalAmount: t.TotalAmount,
          BalanceAmount: t.BalancePrincipalAfter ?? 0

        )).ToList();

        var result = new CollectionReportPagedDto(
            Items: items,
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            TotalPrincipalCollected: totals?.TotalPrincipal ?? 0,
            TotalInterestCollected: totals?.TotalInterest ?? 0,
            GrandTotalCollected: totals?.TotalCollected ?? 0
        );

        return Ok(result);
    }

    // GET /api/collection-reports/customer-search?q=Murugan
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

    [HttpGet("loans-by-customer")]
    public async Task<ActionResult<IEnumerable<object>>> LoansByCustomer([FromQuery] int customerId)
    {
        if (customerId <= 0)
            return Ok(Array.Empty<object>());

        var loans = await _db.Loans
            .AsNoTracking()
            .Where(l => l.CustomerId == customerId)
            .OrderByDescending(l => l.LoanDate)
            .Select(l => new { l.LoanId, l.LoanNumber, l.Status })
            .ToListAsync();

        return Ok(loans);
    }
}