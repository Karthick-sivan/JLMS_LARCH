using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.DTOs;
using JLMS.Api.Models;

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
        var currentUser = HttpContext.Items["CurrentUser"] as User;
        var filterBranchId = currentUser?.GetFilterBranchId();

        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        // Active Loans
        var activeLoansQuery = _db.Loans.Where(l => l.Status == "Active");
        if (filterBranchId.HasValue)
            activeLoansQuery = activeLoansQuery.Where(l => l.BranchId == filterBranchId.Value);
        var activeLoans = await activeLoansQuery.CountAsync();

        // Outstanding Amount
        var outstandingQuery = _db.Loans.Where(l => l.Status == "Active");
        if (filterBranchId.HasValue)
            outstandingQuery = outstandingQuery.Where(l => l.BranchId == filterBranchId.Value);
        var outstandingAmount = await outstandingQuery.SumAsync(l => (decimal?)l.OutstandingPrincipal) ?? 0;

        // Today's Collections
        var todaysCollectionsQuery = _db.LoanTransactions
            .Where(t => (t.TransactionType == "InterestCollection" || t.TransactionType == "LoanOpsPayment" || t.TransactionType == "Closure")
                        && t.TransactionDate.Date == today);
        if (filterBranchId.HasValue)
            todaysCollectionsQuery = todaysCollectionsQuery.Where(t => _db.Loans.Any(l => l.LoanId == t.LoanId && l.BranchId == filterBranchId.Value));
        var todaysCollections = await todaysCollectionsQuery.SumAsync(t => (decimal?)t.TotalAmount) ?? 0;

        // Today's Disbursement
        var todaysDisbursementQuery = _db.LoanTransactions
            .Where(t => t.TransactionType == "Disbursement" && t.TransactionDate.Date == today);
        if (filterBranchId.HasValue)
            todaysDisbursementQuery = todaysDisbursementQuery.Where(t => _db.Loans.Any(l => l.LoanId == t.LoanId && l.BranchId == filterBranchId.Value));
        var todaysDisbursement = await todaysDisbursementQuery.SumAsync(t => (decimal?)t.TotalAmount) ?? 0;

        // Overdue Loans
        var overdueQuery = _db.Loans.Where(l => l.Status == "Active" && l.MaturityDate != null && l.MaturityDate < today);
        if (filterBranchId.HasValue)
            overdueQuery = overdueQuery.Where(l => l.BranchId == filterBranchId.Value);
        var overdueLoans = await overdueQuery.CountAsync();

        // Auction Eligible
        var auctionEligibleQuery = _db.Auctions.Where(a => (a.Status == "Eligible" || a.Status == "NoticeSent") && _db.Loans.Any(l => l.LoanId == a.LoanId));
        if (filterBranchId.HasValue)
            auctionEligibleQuery = auctionEligibleQuery.Where(a => _db.Loans.Any(l => l.LoanId == a.LoanId && l.BranchId == filterBranchId.Value));
        var auctionEligible = await auctionEligibleQuery.CountAsync();

        // Renewals This Month
        var renewalsQuery = _db.LoanTransactions.Where(t => t.TransactionType == "Renewal" && t.TransactionDate >= monthStart);
        if (filterBranchId.HasValue)
            renewalsQuery = renewalsQuery.Where(t => _db.Loans.Any(l => l.LoanId == t.LoanId && l.BranchId == filterBranchId.Value));
        var renewalsThisMonth = await renewalsQuery.CountAsync();

        // Closures This Month
        var closuresQuery = _db.Loans.Where(t => t.Status == "Closed" && t.ClosedAt >= monthStart);
        if (filterBranchId.HasValue)
            closuresQuery = closuresQuery.Where(t => t.BranchId == filterBranchId.Value);
        var closuresThisMonth = await closuresQuery.CountAsync();

        return Ok(new DashboardSummaryDto(
            activeLoans, outstandingAmount, todaysCollections, todaysDisbursement,
            overdueLoans, auctionEligible, renewalsThisMonth, closuresThisMonth));
    }

    [HttpGet("collections-today")]
    public async Task<ActionResult> GetCollectionsToday()
    {
        var currentUser = HttpContext.Items["CurrentUser"] as User;
        var filterBranchId = currentUser?.GetFilterBranchId();
        var today = DateTime.Today;

        var itemsQuery = _db.LoanTransactions
            .AsNoTracking()
            .Where(x =>
                (x.TransactionType == "InterestCollection" ||
                 x.TransactionType == "LoanOpsPayment" ||
                 x.TransactionType == "Closure") &&
                x.TransactionDate >= today &&
                x.TransactionDate < today.AddDays(1));

        if (filterBranchId.HasValue)
        {
            itemsQuery = itemsQuery.Where(x => _db.Loans.Any(l => l.LoanId == x.LoanId && l.BranchId == filterBranchId.Value));
        }

        var items = await itemsQuery
            .Join(_db.Loans,
                x => x.LoanId,
                t => t.LoanId,
                (x, t) => new { x, t })
            .Join(_db.Customers,
                xt => xt.t.CustomerId,
                c => c.CustomerId,
                (xt, c) => new
                {
                    xt.t.LoanNumber,
                    c.CustomerName,
                    xt.x.TotalAmount,
                    xt.x.PaymentMode,
                    xt.x.TransactionDate
                })
            .OrderByDescending(x => x.TransactionDate)
            .Take(20)
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("collection-trend")]
    public async Task<ActionResult> GetCollectionTrend([FromQuery] int days = 14)
    {
        var currentUser = HttpContext.Items["CurrentUser"] as User;
        var filterBranchId = currentUser?.GetFilterBranchId();

        var today = DateTime.UtcNow.Date;
        var startDate = today.AddDays(-(days - 1));

        var query = _db.LoanTransactions.AsNoTracking()
            .Where(t => (t.TransactionType == "InterestCollection" || t.TransactionType == "LoanOpsPayment" || t.TransactionType == "Closure")
                        && t.TransactionDate.Date >= startDate);

        if (filterBranchId.HasValue)
        {
            query = query.Where(t => _db.Loans.Any(l => l.LoanId == t.LoanId && l.BranchId == filterBranchId.Value));
        }

        var raw = await query
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
        var currentUser = HttpContext.Items["CurrentUser"] as User;
        var filterBranchId = currentUser?.GetFilterBranchId();

        var today = DateTime.UtcNow.Date;
        var query = _db.Loans.AsNoTracking()
            .Include(l => l.Customer)
            .Where(l => l.Status == "Active" && l.MaturityDate != null && l.MaturityDate <= today.AddDays(3));

        if (filterBranchId.HasValue)
        {
            query = query.Where(l => l.BranchId == filterBranchId.Value);
        }

        var loans = await query
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

    [HttpGet("superadmin-summary")]
    public async Task<ActionResult> GetSuperAdminSummary()
    {
        var currentUser = HttpContext.Items["CurrentUser"] as User;
        if (currentUser == null || !currentUser.IsSuperAdmin())
        {
            return Forbid();
        }

        var totalBranches = await _db.Branches.CountAsync();
        var activeBranches = await _db.Branches.CountAsync(b => b.IsActive);
        var inactiveBranches = totalBranches - activeBranches;
        var totalBranchAdmins = await _db.Users.CountAsync(u => u.IsActive && u.RoleId == 4);
        var totalSystemUsers = await _db.Users.CountAsync(u => u.IsActive);
        var totalCustomers = await _db.Customers.CountAsync();
        var totalLoans = await _db.Loans.CountAsync();

        var today = DateTime.UtcNow.Date;

        // Branch list for management with performance metrics
        var branches = await _db.Branches.AsNoTracking().ToListAsync();

        var branchStats = branches.Select(b => {
            // Active loans for this branch
            var activeLoans = _db.Loans.Count(l => l.BranchId == b.BranchId && l.Status == "Active");
            
            // Outstanding amount for this branch
            var outstandingAmount = _db.Loans
                .Where(l => l.BranchId == b.BranchId && l.Status == "Active")
                .Sum(l => (decimal?)l.OutstandingPrincipal) ?? 0;
            
            // Today's collections for this branch
            var todaysCollections = _db.LoanTransactions
                .Where(t => (t.TransactionType == "InterestCollection" || t.TransactionType == "LoanOpsPayment" || t.TransactionType == "Closure")
                            && t.TransactionDate.Date == today
                            && _db.Loans.Any(l => l.LoanId == t.LoanId && l.BranchId == b.BranchId))
                .Sum(t => (decimal?)t.TotalAmount) ?? 0;

            return new {
                b.BranchId,
                b.BranchCode,
                b.BranchName,
                b.City,
                b.State,
                b.IsActive,
                ActiveLoans = activeLoans,
                OutstandingAmount = outstandingAmount,
                TodaysCollections = todaysCollections
            };
        }).ToList();

        return Ok(new {
            totalBranches,
            activeBranches,
            inactiveBranches,
            totalBranchAdmins,
            totalSystemUsers,
            totalCustomers,
            totalLoans,
            branchStats
        });
    }
}