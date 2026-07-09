using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.DTOs;

namespace JLMS.Api.Controllers.Reports;

[ApiController]
[Route("api/outstanding-reports")]
public class OutstandingReportsController : ControllerBase
{
    private readonly JlmsDbContext _db;

    public OutstandingReportsController(JlmsDbContext db)
    {
        _db = db;
    }

    // GET /api/outstanding-reports?fromDate=2026-01-01&toDate=2026-12-31&customerId=5&page=1&pageSize=25
    [HttpGet]
    public async Task<ActionResult<OutstandingReportPagedDto>> GetOutstandingReport(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int? customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        // Only loans that are Active, Due, or Overdue (i.e., have outstanding balances)
        var query = _db.Loans
            .AsNoTracking()
            .Include(l => l.Customer)
            .Include(l => l.LoanScheme)
            .Where(l => l.Status == "Active" || l.Status == "Due" || l.Status == "Overdue")
            .AsQueryable();

        // Filter by loan date range
        if (fromDate.HasValue)
            query = query.Where(l => l.LoanDate.HasValue && l.LoanDate.Value.Date >= fromDate.Value.Date);

        if (toDate.HasValue)
            query = query.Where(l => l.LoanDate.HasValue && l.LoanDate.Value.Date <= toDate.Value.Date);

        // Filter by customer
        if (customerId.HasValue && customerId.Value > 0)
            query = query.Where(l => l.CustomerId == customerId.Value);

        var totalCount = await query.CountAsync();

        // Totals across all matching records (before pagination)
        var totals = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalPrincipal     = g.Sum(l => l.LoanAmount),
                TotalOutPrincipal  = g.Sum(l => l.OutstandingPrincipal),
                TotalOutInterest   = g.Sum(l => l.OutstandingInterest),
                //TotalPenalty       = g.Sum(l => l.PenaltyAccrued),
                TotalOutstanding   = g.Sum(l => l.OutstandingPrincipal + l.OutstandingInterest)
            })
            .FirstOrDefaultAsync();

        var loans = await query
            .OrderByDescending(l => l.LoanDate)
            .ThenByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Determine overdue status on the fly based on maturity date
        var today = DateTime.UtcNow.Date;
        var items = loans.Select(l =>
        {
            var daysOverdue = (l.MaturityDate.HasValue && l.MaturityDate.Value.Date < today)
                ? (int)(today - l.MaturityDate.Value.Date).TotalDays
                : 0;

            var derivedStatus = l.Status;
            if (l.MaturityDate.HasValue && l.MaturityDate.Value.Date < today && l.Status == "Active")
                derivedStatus = "Overdue";

            return new OutstandingReportRowDto(
                LoanId:              l.LoanId,
                LoanNumber:          l.LoanNumber,
                CustomerName:        l.Customer?.CustomerName ?? "",
                CustomerCode:        l.Customer?.CustomerCode ?? "",
                CustomerMobile:      l.Customer?.Mobile ?? "",
                SchemeName:          l.LoanScheme?.SchemeName ?? "",
                LoanDate:            l.LoanDate,
                MaturityDate:        l.MaturityDate,
                LoanAmount:          l.LoanAmount,
                OutstandingPrincipal:l.OutstandingPrincipal,
                OutstandingInterest: l.OutstandingInterest,
                //PenaltyAccrued:      l.PenaltyAccrued,
                TotalOutstanding:    l.OutstandingPrincipal + l.OutstandingInterest,
                DaysOverdue:         daysOverdue,
                Status:              derivedStatus
            );
        }).ToList();

        var result = new OutstandingReportPagedDto(
            Items:      items,
            TotalCount: totalCount,
            Page:       page,
            PageSize:   pageSize,
            TotalLoanAmount:          totals?.TotalPrincipal     ?? 0,
            TotalOutstandingPrincipal:totals?.TotalOutPrincipal  ?? 0,
            TotalOutstandingInterest: totals?.TotalOutInterest   ?? 0,
            //TotalPenalty:             totals?.TotalPenalty       ?? 0,
            GrandTotalOutstanding:    totals?.TotalOutstanding   ?? 0
        );

        return Ok(result);
    }

    // GET /api/outstanding-reports/customer-search?q=Murugan
    // Lightweight endpoint for the customer autocomplete dropdown in filters
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
