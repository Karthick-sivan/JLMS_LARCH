using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.DTOs;

namespace JLMS.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly JlmsDbContext _db;
    public DashboardController(JlmsDbContext db) => _db = db;

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary()
    {
        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var activeLoans = await _db.Loans.CountAsync(l => l.Status == "Active");
        var outstandingAmount = await _db.Loans
            .Where(l => l.Status == "Active")
            .SumAsync(l => (decimal?)l.OutstandingPrincipal) ?? 0;

        var todaysCollections = await _db.LoanTransactions
            .Where(t => (t.TransactionType == "InterestCollection" || t.TransactionType == "PrincipalCollection" || t.TransactionType == "Closure")
                        && t.TransactionDate.Date == today)
            .SumAsync(t => (decimal?)t.TotalAmount) ?? 0;

        var todaysDisbursement = await _db.LoanTransactions
            .Where(t => t.TransactionType == "Disbursement" && t.TransactionDate.Date == today)
            .SumAsync(t => (decimal?)t.TotalAmount) ?? 0;

        var overdueLoans = await _db.Loans
            .CountAsync(l => l.Status == "Active" && l.MaturityDate != null && l.MaturityDate < today);

        var auctionEligible = await _db.Auctions.CountAsync(a => a.Status == "Eligible" || a.Status == "NoticeSent");

        var renewalsThisMonth = await _db.LoanTransactions
            .CountAsync(t => t.TransactionType == "Renewal" && t.TransactionDate >= monthStart);

        var closuresThisMonth = await _db.LoanTransactions
            .CountAsync(t => t.TransactionType == "Closure" && t.TransactionDate >= monthStart);

        return Ok(new DashboardSummaryDto(
            activeLoans, outstandingAmount, todaysCollections, todaysDisbursement,
            overdueLoans, auctionEligible, renewalsThisMonth, closuresThisMonth));
    }

    [HttpGet("collections-today")]
    public async Task<ActionResult> GetCollectionsToday()
    {
        var today = DateTime.UtcNow.Date;
        var items = await _db.LoanTransactions.AsNoTracking()
            .Where(t => (t.TransactionType == "InterestCollection" || t.TransactionType == "PrincipalCollection")
                        && t.TransactionDate.Date == today)
            .OrderByDescending(t => t.TransactionDate)
            .Join(_db.Loans, t => t.LoanId, l => l.LoanId, (t, l) => new { t, l })
            .Join(_db.Customers, x => x.l.CustomerId, c => c.CustomerId, (x, c) => new
            {
                x.l.LoanNumber,
                c.CustomerName,
                x.t.TotalAmount,
                x.t.PaymentMode,
                x.t.TransactionDate
            })
            .Take(20)
            .ToListAsync();
        return Ok(items);
    }

    // GET /api/dashboard/collection-trend?days=14
    // Day-wise total collections (interest + principal + closures) for the last N days,
    // zero-filled for days with no activity.
    [HttpGet("collection-trend")]
    public async Task<ActionResult> GetCollectionTrend([FromQuery] int days = 14)
    {
        var today = DateTime.UtcNow.Date;
        var startDate = today.AddDays(-(days - 1));

        var raw = await _db.LoanTransactions.AsNoTracking()
            .Where(t => (t.TransactionType == "InterestCollection" || t.TransactionType == "PrincipalCollection" || t.TransactionType == "Closure")
                        && t.TransactionDate.Date >= startDate)
            .GroupBy(t => t.TransactionDate.Date)
            .Select(g => new { Date = g.Key, Total = g.Sum(t => t.TotalAmount) })
            .ToListAsync();

        var lookup = raw.ToDictionary(r => r.Date, r => r.Total);

        var result = new List<object>();
        for (var d = startDate; d <= today; d = d.AddDays(1))
        {
            result.Add(new { CollectionDate = d, TotalCollected = lookup.TryGetValue(d, out var total) ? total : 0m });
        }

        return Ok(result);
    }

    [HttpGet("loans-due-today")]
    public async Task<ActionResult> GetLoansDueToday()
    {
        var today = DateTime.UtcNow.Date;
        var loans = await _db.Loans.AsNoTracking()
            .Include(l => l.Customer)
            .Where(l => l.Status == "Active" && l.MaturityDate != null && l.MaturityDate <= today.AddDays(3))
            .OrderBy(l => l.MaturityDate)
            .Take(20)
            .Select(l => new
            {
                l.LoanNumber,
                CustomerName = l.Customer!.CustomerName,
                l.OutstandingPrincipal,
                l.MaturityDate,
                IsOverdue = l.MaturityDate < today
            })
            .ToListAsync();
        return Ok(loans);
    }
}