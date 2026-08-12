using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.DTOs;

namespace JLMS.Api.Controllers.Reports;

[ApiController]
[Route("api/loan-payment-report")]
public class LoanPaymentReportController : ControllerBase
{
    private readonly JlmsDbContext _db;

    public LoanPaymentReportController(JlmsDbContext db)
    {
        _db = db;
    }

    private static readonly string[] PaymentTypes = { "PrincipalCollection", "InterestCollection", "LoanOpsPayment", "Disbursement" };

    // GET /api/loan-payment-report?year=2026&month=8&customerId=5&page=1&pageSize=25
    [HttpGet]
    public async Task<ActionResult<LoanPaymentReportPagedDto>> GetLoanPaymentReport(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] int? customerId,
        [FromQuery] int? loanId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        // Validate year and month
        if (year < 2020 || year > 2100 || month < 1 || month > 12)
        {
            return BadRequest("Invalid year or month parameters");
        }

        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        // Get all loans that had activity in the selected month
        var loanIdsWithActivity = await _db.LoanTransactions
            .AsNoTracking()
            .Where(t => PaymentTypes.Contains(t.TransactionType))
            .Where(t => t.TransactionDate.Date >= startDate.Date && t.TransactionDate.Date <= endDate.Date)
            .Select(t => t.LoanId)
            .Distinct()
            .ToListAsync();

        var query = _db.Loans
            .AsNoTracking()
            .Include(l => l.Customer)
            .Where(l => loanIdsWithActivity.Contains(l.LoanId))
            .Where(l => l.BranchId != 1)
            .AsQueryable();

        if (customerId.HasValue && customerId.Value > 0)
            query = query.Where(l => l.CustomerId == customerId.Value);

        if (loanId.HasValue && loanId.Value > 0)
            query = query.Where(l => l.LoanId == loanId.Value);

        var totalCount = await query.CountAsync();

        var loans = await query
            .OrderByDescending(l => l.LoanDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Get all transactions for these loans in the selected month
        var loanIdsInPage = loans.Select(l => l.LoanId).ToList();
        var transactions = await _db.LoanTransactions
            .AsNoTracking()
            .Include(t => t.Loan)
                .ThenInclude(l => l.Customer)
            .Where(t => loanIdsInPage.Contains(t.LoanId))
            .Where(t => PaymentTypes.Contains(t.TransactionType))
            .Where(t => t.TransactionType != "Disbursement" || (t.FirstMonthInt.HasValue && t.FirstMonthInt.Value > 0))
            .Where(t => t.TransactionDate.Date >= startDate.Date && t.TransactionDate.Date <= endDate.Date)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.TransactionId)
            .ToListAsync();

        // Build the report rows - one row per transaction
        var items = new List<LoanPaymentReportRowDto>();
        decimal totalLoanAmount = 0;
        decimal totalPrincipalAmount = 0;
        decimal totalInterestAmount = 0;
        decimal totalPaidAmount = 0;
        decimal totalBalanceAmount = 0;

        foreach (var txn in transactions)
        {
            bool isDisbursement = txn.TransactionType == "Disbursement";
            decimal principalAmt = isDisbursement ? 0 : txn.PrincipalAmount;
            decimal interestAmt = isDisbursement ? (txn.FirstMonthInt ?? 0) : txn.InterestAmount;
            decimal paidAmt = isDisbursement ? (txn.FirstMonthInt ?? 0) : txn.TotalAmount;

            totalLoanAmount += txn.Loan.LoanAmount;
            totalPrincipalAmount += principalAmt;
            totalInterestAmount += interestAmt;
            totalPaidAmount += paidAmt;
            totalBalanceAmount += txn.BalancePrincipalAfter ?? 0;

            items.Add(new LoanPaymentReportRowDto(
                LoanId: txn.LoanId,
                LoanNumber: txn.Loan.LoanNumber,
                LoanDate: txn.Loan.LoanDate,
                LoanAmount: txn.Loan.LoanAmount,
                CustomerName: txn.Loan.Customer?.CustomerName ?? "",
                GuardianName: txn.Loan.Customer?.GuardianName,
                Mobile: txn.Loan.Customer?.Mobile ?? "",
                PaymentDate: txn.TransactionDate,
                PrincipalAmount: principalAmt,
                InterestAmount: interestAmt,
                PaidAmount: paidAmt,
                BalanceAmount: txn.BalancePrincipalAfter ?? 0,
                BookNo: txn.Loan.BookNo
            ));
        }

        var result = new LoanPaymentReportPagedDto(
            Items: items,
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            Year: year,
            Month: month,
            TotalLoanAmount: totalLoanAmount,
            TotalPrincipalAmount: totalPrincipalAmount,
            TotalInterestAmount: totalInterestAmount,
            TotalPaidAmount: totalPaidAmount,
            TotalBalanceAmount: totalBalanceAmount
        );

        return Ok(result);
    }

    // GET /api/loan-payment-report/customer-search?q=Murugan
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
