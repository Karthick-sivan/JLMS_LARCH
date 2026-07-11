using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.Models;
using JLMS.Api.DTOs;
using JLMS.Api.Services;

namespace JLMS.Api.Controllers;

[ApiController]
[Route("api/loans/{loanId:int}")]
public class ClosureController : ControllerBase
{
    private readonly JlmsDbContext _db;
    private readonly LoanCalculationService _calc;

    public ClosureController(JlmsDbContext db, LoanCalculationService calc)
    {
        _db = db;
        _calc = calc;
    }

    // GET /api/loans/5/closure-calculation
    [HttpGet("closure-calculation")]
    public async Task<ActionResult<ClosureCalculationDto>> CalculateClosure(int loanId)
    {
        var loan = await _db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.LoanId == loanId);
        if (loan == null) return NotFound();
        if (loan.Status != "Active") return BadRequest($"Loan is '{loan.Status}', closure only applies to Active loans.");

        var lastTxn = await _db.LoanTransactions.AsNoTracking()
            .Where(t => t.LoanId == loanId && (t.TransactionType == "InterestCollection" || t.TransactionType == "Disbursement" || t.TransactionType == "Renewal"))
            .OrderByDescending(t => t.TransactionDate).FirstOrDefaultAsync();
        var fromDate = lastTxn?.TransactionDate ?? loan.LoanDate ?? loan.CreatedAt;

        var interest = _calc.CalculateAccruedInterest(loan.OutstandingPrincipal, loan.InterestRatePct, fromDate, DateTime.UtcNow);

        decimal penalty = 0;
        if (loan.MaturityDate.HasValue)
        {
            var scheme = await _db.LoanSchemes.AsNoTracking().FirstOrDefaultAsync(s => s.LoanSchemeId == loan.LoanSchemeId);
            penalty = _calc.CalculatePenalty(loan.OutstandingPrincipal, scheme?.PenaltyRatePerDay ?? 0, loan.MaturityDate.Value, DateTime.UtcNow);
        }

        var total = loan.OutstandingPrincipal + interest + penalty;
        return Ok(new ClosureCalculationDto(loan.OutstandingPrincipal, interest, penalty, total));
    }

    // POST /api/loans/5/close
    [HttpPost("close")]
    public async Task<ActionResult<ReceiptDto>> CloseLoan(int loanId, [FromForm] ClosureRequestWithPhotoDto request)
    {
        var loan = await _db.Loans.Include(l => l.Customer).FirstOrDefaultAsync(l => l.LoanId == loanId);
        if (loan == null) return NotFound();
        if (loan.Status != "Active") return BadRequest($"Loan is '{loan.Status}', cannot close.");

        var calcResult = await CalculateClosure(loanId);
        if (calcResult.Result is not OkObjectResult ok || ok.Value is not ClosureCalculationDto calc)
            return BadRequest("Could not calculate closure amount.");

        // Save closure photo to Uploads/Closure/
        string? closePhotoPath = null;
        if (request.ClosePhoto != null && request.ClosePhoto.Length > 0)
        {
            var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            var folder = Path.Combine(uploadsRoot, "Closure");
            Directory.CreateDirectory(folder);
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(request.ClosePhoto.FileName);
            var filePath = Path.Combine(folder, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
                await request.ClosePhoto.CopyToAsync(stream);
            closePhotoPath = Path.Combine("Closure", fileName).Replace("\\", "/");
        }

        loan.Status = "Closed";
        loan.OutstandingPrincipal = 0;
        loan.OutstandingInterest = 0;
        loan.PenaltyAccrued = 0;
        loan.ClosedBy = request.ProcessedByUserId;
        loan.ClosedAt = DateTime.UtcNow;
        loan.UpdatedAt = DateTime.UtcNow;
        loan.ClosePhotoPath = closePhotoPath;

        var seq = await _db.LoanTransactions.CountAsync() + 1;
        var receiptNo = _calc.GenerateReceiptNumber(seq);

        var txn = new LoanTransaction
        {
            LoanId = loan.LoanId,
            TransactionType = "Closure",
            ReceiptNumber = receiptNo,
            TransactionDate = DateTime.UtcNow,
            PrincipalAmount = calc.OutstandingPrincipal,
            InterestAmount = calc.OutstandingInterest,
            PenaltyAmount = calc.Penalty,
            TotalAmount = calc.TotalClosureAmount,
            PaymentMode = request.PaymentMode,
            ReferenceNo = request.ReferenceNo,
            BalancePrincipalAfter = 0,
            ProcessedBy = request.ProcessedByUserId,
            BranchId = loan.BranchId,
            CreatedAt = DateTime.UtcNow
        };
        _db.LoanTransactions.Add(txn);
        await _db.SaveChangesAsync();

        return Ok(new ReceiptDto(receiptNo, txn.TransactionDate, loan.LoanNumber, loan.Customer?.CustomerName ?? "",
            calc.OutstandingInterest, calc.Penalty, calc.TotalClosureAmount, request.PaymentMode, 0, null));
    }

    // POST /api/loans/5/renew
    [HttpPost("renew")]
    public async Task<ActionResult> RenewLoan(int loanId, [FromBody] RenewalRequestDto request)
    {
        var loan = await _db.Loans.FirstOrDefaultAsync(l => l.LoanId == loanId);
        if (loan == null) return NotFound();
        if (loan.Status != "Active") return BadRequest($"Loan is '{loan.Status}', cannot renew.");

        var calcResult = await CalculateClosure(loanId); // reuse interest+penalty calc
        if (calcResult.Result is not OkObjectResult ok || ok.Value is not ClosureCalculationDto calc)
            return BadRequest("Could not calculate renewal amounts.");

        var totalPayableNow = calc.OutstandingInterest + calc.Penalty + request.RenewalCharges;
        var newMaturity = DateTime.UtcNow.Date.AddMonths(request.NewTenureMonths);

        loan.OutstandingInterest = 0;
        loan.PenaltyAccrued = 0;
        loan.MaturityDate = newMaturity;
        loan.TenureMonths = request.NewTenureMonths;
        loan.UpdatedAt = DateTime.UtcNow;

        var seq = await _db.LoanTransactions.CountAsync() + 1;
        var receiptNo = _calc.GenerateReceiptNumber(seq);

        var txn = new LoanTransaction
        {
            LoanId = loan.LoanId,
            TransactionType = "Renewal",
            ReceiptNumber = receiptNo,
            TransactionDate = DateTime.UtcNow,
            InterestAmount = calc.OutstandingInterest,
            PenaltyAmount = calc.Penalty,
            ChargesAmount = request.RenewalCharges,
            TotalAmount = totalPayableNow,
            BalancePrincipalAfter = loan.OutstandingPrincipal,
            NextDueDate = newMaturity,
            ProcessedBy = request.ProcessedByUserId,
            BranchId = loan.BranchId,
            CreatedAt = DateTime.UtcNow
        };
        _db.LoanTransactions.Add(txn);
        await _db.SaveChangesAsync();

        return Ok(new { receiptNo, loan.LoanId, loan.LoanNumber, NewMaturityDate = newMaturity, TotalPaid = totalPayableNow });
    }

    // POST /api/loans/5/release-jewel
    [HttpPost("release-jewel")]
    public async Task<ActionResult> ReleaseJewel(int loanId, [FromBody] int releasedByUserId)
    {
        var loan = await _db.Loans.FirstOrDefaultAsync(l => l.LoanId == loanId);
        if (loan == null) return NotFound();
        if (loan.Status != "Closed") return BadRequest("Jewel can only be released after the loan is Closed.");

        var existing = await _db.JewelReleases.FirstOrDefaultAsync(r => r.LoanId == loanId);
        if (existing != null) return BadRequest("Jewel has already been released for this loan.");

        var release = new JewelRelease
        {
            LoanId = loanId,
            ReleaseDate = DateTime.UtcNow,
            ReleasedBy = releasedByUserId,
            CustomerAcknowledged = true
        };
        _db.JewelReleases.Add(release);
        await _db.SaveChangesAsync();

        return Ok(new { release.JewelReleaseId, loan.LoanNumber, Message = "Jewel released successfully." });
    }
}
