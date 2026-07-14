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
    private readonly LoanReceiptPdfService _receiptService;

    public LoansController(JlmsDbContext db, LoanCalculationService calc, LoanReceiptPdfService receiptService)
    {
        _db = db;
        _calc = calc;
        _receiptService = receiptService;
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
            loan.LoanId,
            loan.LoanNumber,
            loan.Status,
            CustomerName = loan.Customer?.CustomerName,
            CustomerCode = loan.Customer?.CustomerCode,
            CustomerMobile = loan.Customer?.Mobile,
            KycVerified = loan.Customer?.KycVerified,
            SchemeName = loan.LoanScheme?.SchemeName,
            loan.LoanDate,
            LastPaymentDate = lastPayment,
            loan.MaturityDate,
            loan.InterestRatePct,
            loan.TenureMonths,
            loan.MarketValue,
            loan.EligibleAmount,
            loan.LoanAmount,
            loan.ProcessingFee,
            loan.OutstandingPrincipal,
            loan.OutstandingInterest,
            loan.PenaltyAccrued,
            JewelItems = loan.JewelItems.Select(ji => new
            {
                ji.JewelItemId,
                JewelTypeName = ji.JewelType?.JewelTypeName,
                ji.Quantity,
                ji.GrossWeightGrams,
                ji.StoneWeightGrams,
                ji.NetWeightGrams,
                ji.Purity,
                ji.MarketValue,
                HasPhoto = !string.IsNullOrEmpty(ji.PhotoPath)
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

    // POST /api/loans/5/submit-for-approval
    // Combines: validate -> approve -> disburse -> ledger entry, in one transaction.
    // Final status is "Active" (loan is now live and accruing interest).
    // Payment mode is hardcoded to "Cash" since this flow no longer collects it.
    [HttpPost("{id:int}/submit-for-approval")]
    public async Task<ActionResult<SubmitForApprovalResponseDto>> SubmitForApproval(
        int id, [FromBody] SubmitForApprovalRequestDto request)
    {
        const string HardcodedPaymentMode = "Cash";

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // ---- Step 1: Atomic "claim" — prevents duplicate approval/disbursement
            // if the button is clicked twice or the request is retried. This UPDATE
            // takes a row lock immediately; a concurrent duplicate call blocks until
            // this transaction finishes, then its WHERE no longer matches (status
            // already moved on) so it affects 0 rows.
            var claimed = await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE Loans
                SET Status = 'Processing', UpdatedAt = GETUTCDATE()
                WHERE LoanId = {id} AND Status IN ('Draft', 'PendingApproval')");

            if (claimed == 0)
            {
                var existing = await _db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.LoanId == id);
                await tx.RollbackAsync();

                if (existing == null)
                    return NotFound($"Loan {id} not found.");

                if (existing.Status == "Active")
                    // Duplicate click after success — return existing result instead of erroring.
                    return Ok(await BuildSubmitResponse(id));

                return Conflict($"Loan {id} cannot be submitted for approval. Current status: {existing.Status}.");
            }

            // ---- Step 2: Load full loan graph ----
            var loan = await _db.Loans
                .Include(l => l.Customer)
                .Include(l => l.LoanScheme)
                .Include(l => l.JewelItems)
                .FirstOrDefaultAsync(l => l.LoanId == id);

            if (loan == null)
            {
                await tx.RollbackAsync();
                return NotFound($"Loan {id} not found.");
            }

            // ---- Step 3: Validate ----
            var errors = ValidateLoanForSubmission(loan);
            if (errors.Count > 0)
            {
                await tx.RollbackAsync();
                return BadRequest(new { Errors = errors });
            }

            var now = DateTime.UtcNow;

            // ---- Step 4: Approve ----
            loan.SubmittedBy = request.SubmittedByUserId;
            loan.SubmittedAt = now;
            loan.ApprovedBy = request.SubmittedByUserId;
            loan.ApprovedAt = now;

            // ---- Step 5: Disburse ----
            var today = now.Date;
            var maturity = today.AddMonths(loan.TenureMonths);
            var netDisbursement = loan.LoanAmount - loan.ProcessingFee;

            loan.LoanDate = today;
            loan.MaturityDate = maturity;
            loan.DisbursedBy = request.SubmittedByUserId;
            loan.DisbursedAt = now;
            loan.UpdatedAt = now;
            loan.Status = "Active"; // loan is now live and accruing interest

            var seq = await _db.LoanTransactions.CountAsync() + 1;
            var receiptNo = _calc.GenerateReceiptNumber(seq);
            while (await _db.LoanTransactions.AnyAsync(t => t.ReceiptNumber == receiptNo))
            {
                seq++;
                receiptNo = _calc.GenerateReceiptNumber(seq);
            }

            var txn = new LoanTransaction
            {
                LoanId = loan.LoanId,
                TransactionType = "Disbursement",
                ReceiptNumber = receiptNo,
                TransactionDate = now,
                PrincipalAmount = loan.LoanAmount,
                InterestAmount = 0,
                PenaltyAmount = 0,
                ChargesAmount = loan.ProcessingFee,
                TotalAmount = netDisbursement,
                PaymentMode = HardcodedPaymentMode,
                ReferenceNo = null,
                BalancePrincipalAfter = loan.OutstandingPrincipal,
                ProcessedBy = request.SubmittedByUserId,
                BranchId = loan.BranchId,
                Remarks = "Auto-approved and disbursed via Submit for Approval",
                CreatedAt = now
            };
            _db.LoanTransactions.Add(txn);

            _db.AuditLogs.Add(new AuditLog
            {
                EventTime = now,
                UserId = request.SubmittedByUserId,
                ModuleName = "LoanApproval",
                ActionDescription = $"Submit-for-approval: approved and disbursed loan {loan.LoanNumber} in a single step.",
                RecordReference = loan.LoanNumber
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(await BuildSubmitResponse(loan.LoanId));
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private List<string> ValidateLoanForSubmission(Loan loan)
    {
        var errors = new List<string>();

        if (loan.JewelItems == null || loan.JewelItems.Count == 0)
            errors.Add("Loan has no jewel items on record.");

        if (loan.LoanAmount <= 0)
            errors.Add("Loan amount must be greater than zero.");

        if (loan.Customer == null)
            errors.Add("Loan is not linked to a valid customer.");

        if (loan.LoanScheme == null || !loan.LoanScheme.IsActive)
            errors.Add("Loan scheme is missing or inactive.");

        if (loan.TenureMonths <= 0)
            errors.Add("Loan tenure is invalid.");

        return errors;
    }

    private async Task<SubmitForApprovalResponseDto> BuildSubmitResponse(int loanId)
    {
        var loan = await _db.Loans.AsNoTracking()
            .Include(l => l.Customer)
            .FirstAsync(l => l.LoanId == loanId);

        var txn = await _db.LoanTransactions.AsNoTracking()
            .Where(t => t.LoanId == loanId && t.TransactionType == "Disbursement")
            .OrderByDescending(t => t.TransactionDate)
            .FirstOrDefaultAsync();

        return new SubmitForApprovalResponseDto(
            loan.LoanId, loan.LoanNumber, loan.Status, loan.Customer?.CustomerName ?? "",
            loan.ApprovedBy ?? 0, loan.ApprovedAt ?? DateTime.MinValue,
            txn?.ReceiptNumber ?? "", loan.DisbursedAt ?? DateTime.MinValue,
            loan.LoanDate, loan.MaturityDate, loan.LoanAmount, loan.ProcessingFee,
            txn?.TotalAmount ?? 0, loan.OutstandingPrincipal, loan.OutstandingInterest,
            txn?.PaymentMode ?? ""
        );
    }

    // POST /api/loans
    // Creates a Draft/PendingApproval loan with jewel items, using the appraisal values supplied.
    // Returns the created jewel item ids (same order as request.JewelItems) so the front end
    // can immediately upload a photo against each jewel item.
    [HttpPost]
    public async Task<ActionResult<NewLoanResponseDto>> Create([FromBody] NewLoanRequestDto request)
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

        // Skip the LTV cap only when the caller explicitly checked "Don't validate" on the New Loan screen.
        if (!request.AllowExceedEligible && request.RequestedLoanAmount > eligibleAmount)
            return BadRequest($"Requested amount ₹{request.RequestedLoanAmount:N2} exceeds eligible amount ₹{eligibleAmount:N2} for this scheme's LTV. Check 'Don't validate — allow amount to exceed eligible' to override.");

        //var sequence = await _db.Loans.CountAsync() + 1;
        //var loanNumber = _calc.GenerateLoanNumber(sequence);
        //while (await _db.Loans.AnyAsync(l => l.LoanNumber == loanNumber))
        //{
        //    sequence++;
        //    loanNumber = _calc.GenerateLoanNumber(sequence);
        //}
        var branchId = customer.BranchId ?? 1;
        var branch = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.BranchId == branchId);
        if (branch == null) return BadRequest("Branch not found.");

        var sequence = await _db.Loans.CountAsync() + 1;
        var loanNumber = _calc.GenerateLoanNumber(sequence, branch.BranchCode);
        while (await _db.Loans.AnyAsync(l => l.LoanNumber == loanNumber))
        {
            sequence++;
            loanNumber = _calc.GenerateLoanNumber(sequence, branch.BranchCode);
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
            BranchId = branchId,
            //BranchId = customer.BranchId ?? 1,
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

        return CreatedAtAction(nameof(GetById), new { id = loan.LoanId }, new NewLoanResponseDto(
            loan.LoanId,
            loan.LoanNumber,
            loan.Status,
            loan.MarketValue,
            loan.EligibleAmount,
            loan.LoanAmount,
            loan.OverallInterest,
            jewelItemEntities.Select(ji => new NewLoanJewelItemRefDto(ji.JewelItemId, ji.JewelTypeId)).ToList()
        ));
    }

    // POST /api/loans/jewel-items/5/photo
    // Uploads/replaces the photo for a single jewel item. Retained for other screens
    // that still work per-jewel-item; the New Loan screen no longer uses this endpoint
    // (it now captures one combined photo per loan — see POST /api/loans/{id}/photo).
    [HttpPost("jewel-items/{jewelItemId:int}/photo")]
    public async Task<ActionResult> UploadJewelItemPhoto(int jewelItemId, IFormFile photo)
    {
        if (photo == null || photo.Length == 0)
            return BadRequest(new { message = "No file uploaded." });

        var jewelItem = await _db.JewelItems.FindAsync(jewelItemId);
        if (jewelItem == null)
            return NotFound(new { message = "Jewel item not found." });

        var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        var jewelPhotosDir = Path.Combine(uploadsRoot, "JewelPhotos");
        Directory.CreateDirectory(jewelPhotosDir);

        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(photo.FileName);
        var filePath = Path.Combine(jewelPhotosDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await photo.CopyToAsync(stream);
        }

        jewelItem.PhotoPath = Path.Combine("JewelPhotos", fileName).Replace("\\", "/");
        await _db.SaveChangesAsync();

        return Ok(new { jewelItem.JewelItemId, jewelItem.PhotoPath });
    }

    // GET /api/loans/jewel-items/5/photo            -> inline (for the lightbox / thumbnail)
    // GET /api/loans/jewel-items/5/photo?download=true -> forces attachment download
    [HttpGet("jewel-items/{jewelItemId:int}/photo")]
    public async Task<IActionResult> GetJewelItemPhoto(int jewelItemId, [FromQuery] bool download = false)
    {
        var jewelItem = await _db.JewelItems.AsNoTracking()
            .FirstOrDefaultAsync(j => j.JewelItemId == jewelItemId);

        if (jewelItem == null)
            return NotFound(new { message = "Jewel item not found." });
        if (string.IsNullOrEmpty(jewelItem.PhotoPath))
            return NotFound(new { message = "No photo uploaded for this jewel item." });

        var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        var fullPath = Path.GetFullPath(Path.Combine(uploadsRoot, jewelItem.PhotoPath));

        // Guard against path traversal - resolved path must stay inside Uploads.
        if (!fullPath.StartsWith(Path.GetFullPath(uploadsRoot), StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Invalid photo path." });

        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { message = "File missing on server." });

        var contentType = GetContentType(fullPath);
        var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);

        if (download)
            return File(bytes, contentType, $"JewelItem-{jewelItemId}{Path.GetExtension(fullPath)}");

        return File(bytes, contentType);
    }

    // POST /api/loans/5/photo
    // Uploads/replaces the SINGLE combined photo covering all jewel items on this loan.
    // Used by the New Loan screen (Step 3), which now captures one photo via the
    // customer's webcam instead of one photo per jewel item. View-only on the New Loan
    // screen — no download action is exposed there.
    [HttpPost("{id:int}/photo")]
    public async Task<ActionResult> UploadLoanPhoto(int id, IFormFile photo)
    {
        if (photo == null || photo.Length == 0)
            return BadRequest(new { message = "No file uploaded." });

        var loan = await _db.Loans.FindAsync(id);
        if (loan == null)
            return NotFound(new { message = "Loan not found." });

        var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        var loanPhotosDir = Path.Combine(uploadsRoot, "LoanPhotos");
        Directory.CreateDirectory(loanPhotosDir);

        var extension = Path.GetExtension(photo.FileName);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".jpg";
        var fileName = Guid.NewGuid().ToString() + extension;
        var filePath = Path.Combine(loanPhotosDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await photo.CopyToAsync(stream);
        }

        loan.GroupPhotoPath = Path.Combine("LoanPhotos", fileName).Replace("\\", "/");
        loan.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { loan.LoanId, loan.GroupPhotoPath });
    }

    // GET /api/loans/5/photo
    // Streams the single combined jewel-items photo for the loan (inline only).
    [HttpGet("{id:int}/photo")]
    public async Task<IActionResult> GetLoanPhoto(int id)
    {
        var loan = await _db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.LoanId == id);
        if (loan == null)
            return NotFound(new { message = "Loan not found." });
        if (string.IsNullOrEmpty(loan.GroupPhotoPath))
            return NotFound(new { message = "No photo uploaded for this loan." });

        var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        var fullPath = Path.GetFullPath(Path.Combine(uploadsRoot, loan.GroupPhotoPath));

        if (!fullPath.StartsWith(Path.GetFullPath(uploadsRoot), StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Invalid photo path." });

        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { message = "File missing on server." });

        var contentType = GetContentType(fullPath);
        var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
        return File(bytes, contentType);
    }

    private static string GetContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }

    // GET /api/loans/5/receipt-pdf
    [HttpGet("{id:int}/receipt-pdf")]
    public async Task<IActionResult> GetReceiptPdf(int id)
    {
        var loan = await _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.LoanScheme)
            .Include(l => l.JewelItems).ThenInclude(ji => ji.JewelType)
            .FirstOrDefaultAsync(l => l.LoanId == id);

        if (loan == null) return NotFound(new { message = "Loan not found." });
        if (loan.Customer == null) return BadRequest(new { message = "Loan has no linked customer." });

        var bytes = _receiptService.GenerateReceipt(loan);
        return File(bytes, "application/pdf", $"Receipt-{loan.LoanNumber}.pdf");
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