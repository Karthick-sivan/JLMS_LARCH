using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.Models;
using JLMS.Api.DTOs;
using JLMS.Api.Services;

namespace JLMS.Api.Controllers;

[ApiController]
[Route("api/loans")]
public class LoansController : ControllerBase
{
    private readonly JlmsDbContext _db;
    private readonly LoanCalculationService _calc;

    public LoansController(JlmsDbContext db, LoanCalculationService calc)
    {
        _db = db;
        _calc = calc;
    }

    // GET /api/loans?status=Active
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LoanSummaryDto>>> GetAll(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        var query = _db.Loans.AsNoTracking().Include(l => l.Customer).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(l => l.Status == status);

        var loans = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(loans.Select(l => new LoanSummaryDto(
            l.LoanId, l.LoanNumber, l.Customer?.CustomerName ?? "", l.Status,
            l.MarketValue, l.EligibleAmount, l.LoanAmount,
            l.OutstandingPrincipal, l.OutstandingInterest, l.LoanDate, l.MaturityDate)));
    }

    // GET /api/loans/JL-20261247  (lookup by loan number — used by Collections/Closure/Renewal screens)
    [HttpGet("by-number/{loanNumber}")]
    public async Task<ActionResult<object>> GetByNumber(string loanNumber)
    {
        var loan = await _db.Loans.AsNoTracking()
            .Include(l => l.Customer)
            .Include(l => l.LoanScheme)
            .Include(l => l.JewelItems).ThenInclude(ji => ji.JewelType)
            .FirstOrDefaultAsync(l => l.LoanNumber == loanNumber);

        if (loan == null) return NotFound($"No loan found with number {loanNumber}.");
        DateTime? lastPayment = await _db.LoanTransactions
            .AsNoTracking()
            .Where(t => t.LoanId == loan.LoanId &&
                   (t.TransactionType == "PrincipalCollection" ||
                    t.TransactionType == "InterestCollection"))
            .OrderByDescending(t => t.TransactionDate)
            .Select(t => (DateTime?)t.TransactionDate)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            loan.LoanId, loan.LoanNumber, loan.Status,
            CustomerName = loan.Customer?.CustomerName,
            CustomerCode = loan.Customer?.CustomerCode,
            CustomerMobile = loan.Customer?.Mobile,
            KycVerified = loan.Customer?.KycVerified,
            SchemeName = loan.LoanScheme?.SchemeName,
            loan.LoanDate,
            LastPaymentDate = lastPayment,
            loan.MaturityDate, loan.InterestRatePct, loan.TenureMonths,
            loan.MarketValue, loan.EligibleAmount, loan.LoanAmount, loan.ProcessingFee,
            loan.OutstandingPrincipal, loan.OutstandingInterest, loan.PenaltyAccrued,
            JewelItems = loan.JewelItems.Select(ji => new
            {
                ji.JewelItemId, JewelTypeName = ji.JewelType?.JewelTypeName, ji.Quantity,
                ji.GrossWeightGrams, ji.StoneWeightGrams, ji.NetWeightGrams, ji.Purity, ji.MarketValue
            })
        });
    }

    // GET /api/loans/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<object>> GetById(int id)
    {
        var loan = await _db.Loans.AsNoTracking()
            .Include(l => l.Customer)
            .Include(l => l.LoanScheme)
            .Include(l => l.JewelItems).ThenInclude(ji => ji.JewelType)
            .FirstOrDefaultAsync(l => l.LoanId == id);

        if (loan == null) return NotFound();
        return Ok(loan);
    }

    // GET /api/loans/5/release-details
    [HttpGet("{id:int}/release-details")]
    public async Task<ActionResult<object>> GetReleaseDetails(int id)
    {
        var loan = await _db.Loans
            .AsNoTracking()
            .Include(l => l.Customer)
            .Include(l => l.JewelItems)
                .ThenInclude(j => j.JewelType)
            .FirstOrDefaultAsync(l => l.LoanId == id);

        if (loan == null)
            return NotFound();

        return Ok(new
        {
            loan.LoanId,
            loan.LoanNumber,
            loan.Status,
            loan.ClosedAt,

            Customer = new
            {
                loan.Customer.CustomerCode,
                loan.Customer.CustomerName,
                loan.Customer.Mobile,
                loan.Customer.KycVerified
            },

            JewelItems = loan.JewelItems.Select(j => new
            {
                j.JewelItemId,
                JewelTypeName = j.JewelType.JewelTypeName,
                j.Quantity,
                j.NetWeightGrams,
                j.Purity
            })
        });
    }
    // POST /api/loans
    // Creates a Draft loan with jewel items, using the appraisal values supplied.
    // POST /api/loans
    // Creates a Draft loan with jewel items, using the appraisal values supplied.
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] NewLoanRequestDto request)
    {
        var customer = await _db.Customers.FindAsync(request.CustomerId);
        if (customer == null) return BadRequest("Customer not found.");

        var scheme = await _db.LoanSchemes.FindAsync(request.LoanSchemeId);
        if (scheme == null) return BadRequest("Loan scheme not found.");

        if (request.JewelItems == null || request.JewelItems.Count == 0)
            return BadRequest("At least one jewel item is required.");

        var today = DateTime.UtcNow.Date;
        var goldRate = await _db.GoldRates.AsNoTracking()
            .Where(r => r.EffectiveDate <= today).OrderByDescending(r => r.EffectiveDate)
            .FirstOrDefaultAsync();
        if (goldRate == null) return BadRequest("No gold rate configured. Set today's rate first.");

        var jewelTypeIds = request.JewelItems.Select(i => i.JewelTypeId).Distinct().ToList();
        var jewelTypes = await _db.JewelTypes.AsNoTracking()
            .Where(jt => jewelTypeIds.Contains(jt.JewelTypeId)).ToDictionaryAsync(jt => jt.JewelTypeId);

        var jewelItemEntities = new List<JewelItem>();
        decimal totalMarketValue = 0;

        foreach (var item in request.JewelItems)
        {
            if (!jewelTypes.TryGetValue(item.JewelTypeId, out var jewelType))
                return BadRequest($"Unknown JewelTypeId {item.JewelTypeId}.");

            var purity = item.Purity ?? jewelType.DefaultPurity;
            var (netWeight, marketValuePerUnit) = _calc.CalculateJewelValue(
                item.GrossWeightGrams, item.StoneWeightGrams, purity,
                goldRate.Rate24K, goldRate.Rate22K, goldRate.Rate18K);

            var lineMarketValue = marketValuePerUnit * item.Quantity;
            totalMarketValue += lineMarketValue;

            jewelItemEntities.Add(new JewelItem
            {
                JewelTypeId = item.JewelTypeId,
                Quantity = item.Quantity,
                GrossWeightGrams = item.GrossWeightGrams,
                StoneWeightGrams = item.StoneWeightGrams,
                NetWeightGrams = netWeight * item.Quantity,
                Purity = purity,
                MarketValue = lineMarketValue,
                CreatedAt = DateTime.UtcNow
            });
        }

        var eligibleAmount = _calc.CalculateEligibleAmount(totalMarketValue, scheme.MaxLtvPercent);

        if (request.RequestedLoanAmount > eligibleAmount)
            return BadRequest($"Requested amount ₹{request.RequestedLoanAmount:N2} exceeds eligible amount ₹{eligibleAmount:N2} for this scheme's LTV.");

        var sequence = await _db.Loans.CountAsync() + 1;
        var loanNumber = _calc.GenerateLoanNumber(sequence);
        while (await _db.Loans.AnyAsync(l => l.LoanNumber == loanNumber))
        {
            sequence++;
            loanNumber = _calc.GenerateLoanNumber(sequence);
        }

        // Rule 4: Overall Interest = Principal x Interest%, fixed once at creation.
        // Outstanding Interest starts EQUAL to Overall Interest — not zero — so the
        // grid and payment screens show the correct figure from day one.
        var overallInterest = Math.Round(request.RequestedLoanAmount * scheme.InterestRatePct / 100m, 2, MidpointRounding.AwayFromZero);

        var loan = new Loan
        {
            LoanNumber = loanNumber,
            CustomerId = request.CustomerId,
            LoanSchemeId = request.LoanSchemeId,
            BranchId = customer.BranchId ?? 1,
            InterestRatePct = scheme.InterestRatePct,
            TenureMonths = scheme.TenureMonths,
            MarketValue = totalMarketValue,
            EligibleAmount = eligibleAmount,
            LoanAmount = request.RequestedLoanAmount,
            ProcessingFee = scheme.ProcessingFee,
            OverallInterest = overallInterest,
            OutstandingPrincipal = request.RequestedLoanAmount,
            OutstandingInterest = overallInterest,
            PenaltyAccrued = 0,
            Status = "PendingApproval",
            Remarks = request.Remarks,
            CreatedAt = DateTime.UtcNow,
            JewelItems = jewelItemEntities
        };

        _db.Loans.Add(loan);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = loan.LoanId }, new
        {
            loan.LoanId,
            loan.LoanNumber,
            loan.Status,
            loan.MarketValue,
            loan.EligibleAmount,
            loan.LoanAmount,
            loan.OverallInterest
        });
    }

    // POST /api/loans/5/approve
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> ApproveOrReject(int id, [FromBody] ApprovalDecisionDto decision)
    {
        var loan = await _db.Loans.FindAsync(id);
        if (loan == null) return NotFound();
        if (loan.Status != "PendingApproval")
            return BadRequest($"Loan is currently '{loan.Status}' and cannot be approved/rejected.");

        loan.Status = decision.Approved ? "Active" : "Rejected";
        loan.ApprovedBy = decision.ApprovedByUserId;
        loan.ApprovedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(decision.Remarks))
            loan.Remarks = decision.Remarks;

        await _db.SaveChangesAsync();
        return Ok(new { loan.LoanId, loan.LoanNumber, loan.Status });
    }

    // POST /api/loans/5/disburse
    [HttpPost("{id:int}/disburse")]
    public async Task<ActionResult<ReceiptDto>> Disburse(int id, [FromBody] DisbursementRequestDto request)
    {
        var loan = await _db.Loans.Include(l => l.Customer).FirstOrDefaultAsync(l => l.LoanId == id);
        if (loan == null) return NotFound();
        if (loan.Status != "Approved")
            return BadRequest($"Loan must be Approved before disbursement. Current status: {loan.Status}");

        var netDisbursement = loan.LoanAmount - loan.ProcessingFee;
        var today = DateTime.UtcNow.Date;
        var maturity = today.AddMonths(loan.TenureMonths);

        loan.Status = "Active";
        loan.LoanDate = today;
        loan.MaturityDate = maturity;
        loan.DisbursedBy = request.ProcessedByUserId;
        loan.DisbursedAt = DateTime.UtcNow;
        loan.UpdatedAt = DateTime.UtcNow;

        var seq = await _db.LoanTransactions.CountAsync() + 1;
        var receiptNo = _calc.GenerateReceiptNumber(seq);

        var txn = new LoanTransaction
        {
            LoanId = loan.LoanId,
            TransactionType = "Disbursement",
            ReceiptNumber = receiptNo,
            TransactionDate = DateTime.UtcNow,
            PrincipalAmount = loan.LoanAmount,
            ChargesAmount = loan.ProcessingFee,
            TotalAmount = netDisbursement,
            PaymentMode = request.PaymentMode,
            ReferenceNo = request.ReferenceNo,
            BalancePrincipalAfter = loan.OutstandingPrincipal,
            ProcessedBy = request.ProcessedByUserId,
            BranchId = loan.BranchId,
            CreatedAt = DateTime.UtcNow
        };
        _db.LoanTransactions.Add(txn);
        await _db.SaveChangesAsync();

        return Ok(new ReceiptDto(receiptNo, txn.TransactionDate, loan.LoanNumber, loan.Customer?.CustomerName ?? "",
            0, 0, netDisbursement, request.PaymentMode, loan.OutstandingPrincipal, maturity));
    }
}
