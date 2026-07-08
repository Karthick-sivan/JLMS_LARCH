using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.Models;
using JLMS.Api.DTOs;
using JLMS.Api.Services;

namespace JLMS.Api.Controllers;

[ApiController]
[Route("api/loans/{loanId:int}")]
public class CollectionsController : ControllerBase
{
    private readonly JlmsDbContext _db;
    private readonly LoanCalculationService _calc;

    public CollectionsController(JlmsDbContext db, LoanCalculationService calc)
    {
        _db = db;
        _calc = calc;
    }

    // GET /api/loans/5/outstanding   — live-calculated outstanding interest & penalty
    [HttpGet("outstanding")]
    public async Task<ActionResult> GetOutstanding(int loanId)
    {
        var loan = await _db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.LoanId == loanId);
        if (loan == null) return NotFound();
        if (loan.Status != "Active")
            return BadRequest($"Loan is '{loan.Status}', outstanding figures only apply to Active loans.");

        var lastTxn = await _db.LoanTransactions.AsNoTracking()
          .Where(t => t.LoanId == loanId &&
    (t.TransactionType == "InterestCollection" ||
     t.TransactionType == "PrincipalCollection" ||
     t.TransactionType == "Disbursement" ||
     t.TransactionType == "Renewal"))
            .OrderByDescending(t => t.TransactionDate)
            .FirstOrDefaultAsync();

        var fromDate = lastTxn?.TransactionDate ?? loan.LoanDate ?? loan.CreatedAt;
        var today = DateTime.UtcNow;

        var newlyAccruedInterest = _calc.CalculateAccruedInterest(
     loan.OutstandingPrincipal,
     loan.InterestRatePct,
     fromDate,
     today);

        var accruedInterest = loan.OutstandingInterest + newlyAccruedInterest;

        decimal penalty = 0;
        int overdueDays = 0;
        if (loan.MaturityDate.HasValue)
        {
            var scheme = await _db.LoanSchemes.AsNoTracking().FirstOrDefaultAsync(s => s.LoanSchemeId == loan.LoanSchemeId);
            var penaltyRate = scheme?.PenaltyRatePerDay ?? 0;
            penalty = _calc.CalculatePenalty(loan.OutstandingPrincipal, penaltyRate, loan.MaturityDate.Value, today);
            overdueDays = _calc.CalculateOverdueDays(loan.MaturityDate.Value, today);
        }

        return Ok(new
        {
            loan.LoanId, loan.LoanNumber, loan.OutstandingPrincipal,
            OutstandingInterest = accruedInterest,
            Penalty = penalty,
            OverdueDays = overdueDays,
            loan.MaturityDate
        });
    }

    // POST /api/loans/5/collect-interest
    [HttpPost("collect-interest")]
    public async Task<ActionResult<ReceiptDto>> CollectInterest(int loanId, [FromBody] InterestCollectionRequestDto request)
    {
        var loan = await _db.Loans.Include(l => l.Customer).FirstOrDefaultAsync(l => l.LoanId == loanId);
        if (loan == null) return NotFound();
        if (loan.Status != "Active") return BadRequest($"Loan is '{loan.Status}', cannot collect interest.");
        if (request.AmountReceived <= 0) return BadRequest("Amount received must be greater than zero.");

        var seq = await _db.LoanTransactions.CountAsync() + 1;
        var receiptNo = _calc.GenerateReceiptNumber(seq);

        // Apply received amount: penalty first, then interest (typical collection priority)
        var lastTxn = await _db.LoanTransactions.AsNoTracking()
          .Where(t => t.LoanId == loanId &&
    (t.TransactionType == "InterestCollection" ||
     t.TransactionType == "PrincipalCollection" ||
     t.TransactionType == "Disbursement" ||
     t.TransactionType == "Renewal"))
            .OrderByDescending(t => t.TransactionDate).FirstOrDefaultAsync();
        var fromDate = lastTxn?.TransactionDate ?? loan.LoanDate ?? loan.CreatedAt;

        var newlyAccruedInterest = _calc.CalculateAccruedInterest(
            loan.OutstandingPrincipal,
            loan.InterestRatePct,
            fromDate,
            DateTime.UtcNow);

        var accruedInterest = loan.OutstandingInterest + newlyAccruedInterest;

        decimal penalty = 0;
        if (loan.MaturityDate.HasValue)
        {
            var scheme = await _db.LoanSchemes.AsNoTracking().FirstOrDefaultAsync(s => s.LoanSchemeId == loan.LoanSchemeId);
            penalty = _calc.CalculatePenalty(loan.OutstandingPrincipal, scheme?.PenaltyRatePerDay ?? 0, loan.MaturityDate.Value, DateTime.UtcNow);
        }
        var totalDue = accruedInterest + penalty;

        if (totalDue <= 0)
        {
            return BadRequest("There is no outstanding interest or penalty to collect.");
        }

        if (request.AmountReceived > totalDue)
        {
            return BadRequest(
                $"Amount exceeds outstanding dues. Total payable is ₹{totalDue:N2}.");
        }

        if (!request.IsPartial &&
            Math.Abs(request.AmountReceived - totalDue) > 0.01m)
        {
            return BadRequest(
                $"Full payment required. Please pay ₹{totalDue:N2} or enable Partial Payment.");
        }

        var remaining = request.AmountReceived;
        var penaltyCollected = Math.Min(remaining, penalty);
        remaining -= penaltyCollected;
        var interestCollected = Math.Min(remaining, accruedInterest);
        remaining -= interestCollected;

        loan.OutstandingInterest = Math.Max(0, accruedInterest - interestCollected);
        loan.PenaltyAccrued = Math.Max(0, penalty - penaltyCollected);

        // If full payment, clear all outstanding interest.
        if (!request.IsPartial)
        {
            loan.OutstandingInterest = 0;
        }
        loan.UpdatedAt = DateTime.UtcNow;

        var nextDue = (loan.MaturityDate ?? DateTime.UtcNow).Date;
        if (!request.IsPartial) nextDue = DateTime.UtcNow.Date.AddMonths(1);

        var txn = new LoanTransaction
        {
            LoanId = loan.LoanId,
            TransactionType = "InterestCollection",
            ReceiptNumber = receiptNo,
            TransactionDate = DateTime.UtcNow,
            InterestAmount = interestCollected,
            PenaltyAmount = penaltyCollected,
            TotalAmount = penaltyCollected + interestCollected,
            PaymentMode = request.PaymentMode,
            ReferenceNo = request.ReferenceNo,
            BalancePrincipalAfter = loan.OutstandingPrincipal,
            NextDueDate = nextDue,
            ProcessedBy = request.ProcessedByUserId,
            BranchId = loan.BranchId,
            CreatedAt = DateTime.UtcNow
        };
        _db.LoanTransactions.Add(txn);
        await _db.SaveChangesAsync();

        return Ok(new ReceiptDto(receiptNo, txn.TransactionDate, loan.LoanNumber, loan.Customer?.CustomerName ?? "",
            interestCollected, penaltyCollected, txn.TotalAmount, request.PaymentMode, loan.OutstandingPrincipal, nextDue));
    }

    // POST /api/loans/5/collect-principal
  // POST /api/loans/5/collect-principal
[HttpPost("collect-principal")]
public async Task<ActionResult> CollectPrincipal(
    int loanId,
    [FromBody] PrincipalCollectionRequestDto request)
{
    var loan = await _db.Loans
        .Include(l => l.Customer)
        .FirstOrDefaultAsync(l => l.LoanId == loanId);

    if (loan == null)
        return NotFound();

    if (loan.Status != "Active")
        return BadRequest($"Loan is '{loan.Status}', cannot collect principal.");

    if (request.PrincipalAmount <= 0)
        return BadRequest("Principal amount must be greater than zero.");

    if (request.PrincipalAmount > loan.OutstandingPrincipal)
        return BadRequest($"Principal amount ₹{request.PrincipalAmount:N2} exceeds outstanding principal ₹{loan.OutstandingPrincipal:N2}.");

    decimal previousOutstanding = loan.OutstandingPrincipal;

    loan.OutstandingPrincipal -= request.PrincipalAmount;
    loan.UpdatedAt = DateTime.UtcNow;

    var seq = await _db.LoanTransactions.CountAsync() + 1;
    var receiptNo = _calc.GenerateReceiptNumber(seq);

    var txn = new LoanTransaction
    {
        LoanId = loan.LoanId,
        TransactionType = "PrincipalCollection",
        ReceiptNumber = receiptNo,
        TransactionDate = DateTime.UtcNow,
        PrincipalAmount = request.PrincipalAmount,
        TotalAmount = request.PrincipalAmount,
        PaymentMode = request.PaymentMode,
        ReferenceNo = request.ReferenceNo,
        BalancePrincipalAfter = loan.OutstandingPrincipal,
        ProcessedBy = request.ProcessedByUserId,
        BranchId = loan.BranchId,
        CreatedAt = DateTime.UtcNow
    };

    _db.LoanTransactions.Add(txn);

    await _db.SaveChangesAsync();

    return Ok(new
    {
        ReceiptNumber = receiptNo,
        ReceiptDate = txn.TransactionDate,

        LoanId = loan.LoanId,
        LoanNumber = loan.LoanNumber,

        CustomerName = loan.Customer?.CustomerName,

        OriginalPrincipal = loan.LoanAmount,

        PreviousOutstanding = previousOutstanding,

        PrincipalCollected = request.PrincipalAmount,

        NewOutstanding = loan.OutstandingPrincipal,

        PaymentMode = request.PaymentMode,

        ReferenceNo = request.ReferenceNo
    });
}
    }