using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.DTOs;
using JLMS.Api.Services;

namespace JLMS.Api.Controllers;

/// <summary>
/// New, independent controller for the merged Loan Operations page.
/// Does not call or modify LoansController, LoanCalculationService, or any
/// existing Interest Collection / Principal Collection / Closure logic.
/// Thin — all business logic and financial calculation lives in
/// LoanOperationsService / LoanOperationsCalculationHelper.
/// </summary>
[ApiController]
[Route("api/loan-operations")]
public class LoanOperationsController : ControllerBase
{
    private readonly LoanOperationsService _service;
    private readonly JlmsDbContext _db;
    private readonly LoanReceiptPdfService _pdfService;

    //public LoanOperationsController(LoanOperationsService service)
    //{
    //    _service = service;
    //}

    public LoanOperationsController(
        LoanOperationsService service,
        JlmsDbContext db,
        LoanReceiptPdfService pdfService)
    {
        _service = service;
        _db = db;
        _pdfService = pdfService;
    }

    // GET /api/loan-operations/grid
    [HttpGet("grid")]
    public async Task<ActionResult<LoanOperationsGridResultDto>> GetGrid(
        [FromQuery] string? loanNo, [FromQuery] string? customerName, [FromQuery] string? mobile,
        [FromQuery] string? aadhaar, [FromQuery] string? pan, [FromQuery] string? scheme,
        [FromQuery] string? status, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null, [FromQuery] string? sortDir = "asc")
    {
        var query = new LoanOperationsGridQueryDto(
            loanNo, customerName, mobile, aadhaar, pan, scheme, status, fromDate, toDate,
            page, pageSize, sortBy, sortDir);

        var result = await _service.GetGridAsync(query);
        return Ok(result);
    }

    // GET /api/loan-operations/5/payment-details
    [HttpGet("{loanId:int}/payment-details")]
    public async Task<ActionResult<LoanOperationsPaymentDetailsDto>> GetPaymentDetails(int loanId, [FromQuery] DateTime? asOfDate)
    {
        try
        {
            return Ok(await _service.GetPaymentDetailsAsync(loanId, asOfDate));
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    // GET /api/loan-operations/5/interest-preview?asOfDate=2026-07-10
    // Lightweight recompute used when the user changes the payment date.
    [HttpGet("{loanId:int}/interest-preview")]
    public async Task<ActionResult<LoanOperationsInterestCalculationDto>> GetInterestPreview(int loanId, [FromQuery] DateTime? asOfDate)
    {
        try
        {
            return Ok(await _service.GetInterestPreviewAsync(loanId, asOfDate));
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    // POST /api/loan-operations/5/payment
    [HttpPost("{loanId:int}/payment")]
    public async Task<ActionResult<LoanOperationsPaymentResponseDto>> SavePayment(int loanId, [FromBody] LoanOperationsPaymentRequestDto request)
    {
        try
        {
            return Ok(await _service.SavePaymentAsync(loanId, request));
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    // GET /api/loan-operations/5/closure-details
    [HttpGet("{loanId:int}/closure-details")]
    public async Task<ActionResult<LoanOperationsClosureDetailsDto>> GetClosureDetails(int loanId)
    {
        try
        {
            return Ok(await _service.GetClosureDetailsAsync(loanId));
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    //// POST /api/loan-operations/5/close
    //[HttpPost("{loanId:int}/close")]
    //public async Task<ActionResult<LoanOperationsClosureResponseDto>> CloseLoan(int loanId, [FromForm] LoanOperationsClosureRequestDto request)
    //{
    //    try
    //    {
    //        return Ok(await _service.CloseLoanAsync(loanId, request));
    //    }
    //    catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    //    catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    //}
    // POST /api/loan-operations/5/close
    [HttpPost("{loanId:int}/close")]
    public async Task<ActionResult<LoanOperationsClosureResponseDto>> CloseLoan(int loanId, [FromForm] ClosureRequestWithPhotoDto request)
    {
        try
        {
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

            var closeRequest = new LoanOperationsClosureRequestDto(
                request.PaymentMode,
                request.ReferenceNo,
                request.ProcessedByUserId
            );

            return Ok(await _service.CloseLoanAsync(loanId, closeRequest, closePhotoPath));
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    // GET /api/loan-operations/5/ledger?page=1&pageSize=10
    [HttpGet("{loanId:int}/ledger")]
    public async Task<ActionResult<LoanOperationsLedgerResponseDto>> GetLedger(int loanId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            return Ok(await _service.GetLedgerAsync(loanId, page, pageSize));
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    // Returns ALL ledger rows (no pagination) for client-side PDF / Excel export.
    // Identical shape to GetLedger but pageSize = int.MaxValue so no row is omitted.
    [HttpGet("{loanId:int}/ledger-all")]
    public async Task<ActionResult<LoanOperationsLedgerResponseDto>> GetLedgerAll(int loanId)
    {
        try
        {
            return Ok(await _service.GetLedgerAsync(loanId, page: 1, pageSize: int.MaxValue));
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    // Inject LoanReceiptPdfService and DbContext in the constructor


    // POST /api/loan-operations/payment-receipt-pdf
    // Body: PaymentReceiptPdfDto (matches fields of LoanOperationsPaymentResponseDto)
    [HttpPost("payment-receipt-pdf")]
    public IActionResult DownloadPaymentReceiptPdf([FromBody] PaymentReceiptPdfDto dto)
    {
        var bytes = _pdfService.GeneratePaymentReceipt(dto);
        return File(bytes, "application/pdf", $"Receipt-{dto.ReceiptNumber}.pdf");
    }

    // POST /api/loan-operations/closure-receipt-pdf
    // Body: ClosureReceiptPdfDto (matches fields of LoanOperationsClosureResponseDto)
    //[HttpPost("closure-receipt-pdf")]
    //public IActionResult DownloadClosureReceiptPdf([FromBody] ClosureReceiptPdfDto dto)
    //{
    //    var bytes = _pdfService.GenerateClosureReceipt(dto);
    //    return File(bytes, "application/pdf", $"Closure-Receipt-{dto.ReceiptNumber}.pdf");
    //}

    // GET /api/loan-operations/5/closure-receipt-pdf
    // Pulls the full loan (jewel items, customer, closure photo) from the DB —
    // unlike the old POST version, nothing needs to be supplied by the client.
    [HttpGet("{loanId:int}/closure-receipt-pdf")]
    public async Task<IActionResult> DownloadClosureReceiptPdf(int loanId)
    {
        var loan = await _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.LoanScheme)
            .Include(l => l.JewelItems).ThenInclude(ji => ji.JewelType)
            .FirstOrDefaultAsync(l => l.LoanId == loanId);

        if (loan == null) return NotFound();
        if (loan.Status != "Closed") return BadRequest("Loan is not closed.");

        var closureTxn = await _db.LoanTransactions
            .Where(t => t.LoanId == loanId && t.TransactionType == "Closure")
            .OrderByDescending(t => t.TransactionDate)
            .FirstOrDefaultAsync();

        var dto = new ClosureReceiptPdfDto(
            ReceiptNumber: closureTxn?.ReceiptNumber ?? loan.LoanNumber,
            LoanNo: loan.LoanNumber,
            CustomerName: loan.Customer!.CustomerName,
            Mobile: loan.Customer.Mobile,
            LoanScheme: loan.LoanScheme?.SchemeName,
            TransactionDate: loan.ClosedAt ?? DateTime.UtcNow,
            OutstandingPrincipal: 0,
            OutstandingInterest: 0,
            OtherCharges: closureTxn?.ChargesAmount ?? 0,
            GrandTotal: closureTxn?.TotalAmount ?? 0,
            GuardianName: loan.Customer.GuardianName
        );

        var bytes = _pdfService.GenerateClosureReceiptWithDetails(loan, dto);
        return File(bytes, "application/pdf", $"Closure-Receipt-{dto.ReceiptNumber}-{loan.LoanNumber}.pdf");
    }

    // GET /api/loan-operations/transaction-receipt-pdf/{transactionId}
    // Ledger row download — server looks up the transaction to build the receipt
    //[HttpGet("transaction-receipt-pdf/{transactionId:int}")]
    //public async Task<IActionResult> DownloadTransactionReceiptPdf(int transactionId)
    //{
    //    var txn = await _db.LoanTransactions
    //        .Include(t => t.Loan)
    //            .ThenInclude(l => l.Customer)
    //        .Include(t => t.Loan)
    //            .ThenInclude(l => l.LoanScheme)
    //        .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

    //    if (txn == null) return NotFound();

    //    // Map LoanTransaction → PaymentReceiptPdfDto
    //    // InterestAmount and PrincipalAmount are stored on the transaction entity
    //    var dto = new PaymentReceiptPdfDto(
    //        ReceiptNumber: txn.ReceiptNumber ?? txn.TransactionId.ToString(),
    //        LoanNo: txn.Loan.LoanNumber,
    //        CustomerName: txn.Loan.Customer!.CustomerName,
    //        Mobile: txn.Loan.Customer.Mobile,
    //        TransactionDate: txn.TransactionDate,
    //        PaymentMode: txn.PaymentMode ?? "-",
    //        LoanScheme: txn.Loan.LoanScheme?.SchemeName,
    //        MaturityDate: txn.Loan.MaturityDate,
    //        InterestPaid: txn.InterestAmount,
    //        PrincipalPaid: txn.PrincipalAmount,
    //        AmountReceived: txn.TotalAmount,
    //        RemainingInterest: txn.Loan.OutstandingInterest,   // balance at time of ledger view
    //        RemainingPrincipal: txn.Loan.OutstandingPrincipal,
    //          GuardianName: txn.Loan.Customer.GuardianName
    //    );

    //    var bytes = _pdfService.GeneratePaymentReceipt(dto);
    //    return File(bytes, "application/pdf",
    //        $"Receipt-{dto.ReceiptNumber}-{dto.LoanNo}.pdf");
    //}

[HttpGet("transaction-receipt-pdf/{transactionId:int}")]
public async Task<IActionResult> DownloadTransactionReceiptPdf(int transactionId)
{
    var txn = await _db.LoanTransactions
        .Include(t => t.Loan).ThenInclude(l => l.Customer)
        .Include(t => t.Loan).ThenInclude(l => l.LoanScheme)
        .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

    if (txn == null) return NotFound();

    PaymentReceiptPdfDto dto;

    if (txn.TransactionType == "Disbursement")
    {
        // This single row represents TWO ledger lines: the principal disbursed
        // and the first-month interest collected upfront. A receipt download
        // from the ledger's "First Month Interest Collected" row should only
        // reflect the interest portion — not the disbursed principal.
        dto = new PaymentReceiptPdfDto(
            ReceiptNumber: txn.ReceiptNumber ?? txn.TransactionId.ToString(),
            LoanNo: txn.Loan.LoanNumber,
            CustomerName: txn.Loan.Customer!.CustomerName,
            Mobile: txn.Loan.Customer.Mobile,
            TransactionDate: txn.TransactionDate,
            PaymentMode: txn.PaymentMode ?? "-",
            LoanScheme: txn.Loan.LoanScheme?.SchemeName,
            MaturityDate: txn.Loan.MaturityDate,
            InterestPaid: txn.FirstMonthInt ?? 0,
            PrincipalPaid: 0,
            AmountReceived: txn.FirstMonthInt ?? 0,
            // Snapshot as-of this transaction: full principal still outstanding
            // (nothing repaid yet), interest fully cleared by this same collection.
            RemainingInterest: 0,
            //RemainingPrincipal: txn.BalancePrincipalAfter,
            RemainingPrincipal: txn.BalancePrincipalAfter ?? 0,
            GuardianName: txn.Loan.Customer.GuardianName
        );
    }
    else
    {
        dto = new PaymentReceiptPdfDto(
            ReceiptNumber: txn.ReceiptNumber ?? txn.TransactionId.ToString(),
            LoanNo: txn.Loan.LoanNumber,
            CustomerName: txn.Loan.Customer!.CustomerName,
            Mobile: txn.Loan.Customer.Mobile,
            TransactionDate: txn.TransactionDate,
            PaymentMode: txn.PaymentMode ?? "-",
            LoanScheme: txn.Loan.LoanScheme?.SchemeName,
            MaturityDate: txn.Loan.MaturityDate,
            InterestPaid: txn.InterestAmount,
            PrincipalPaid: txn.PrincipalAmount,
            AmountReceived: txn.TotalAmount,
            // Use the historical snapshot instead of the loan's CURRENT live
            // balance, so old receipts don't show today's (possibly ₹0) balance.
            RemainingInterest: 0, // see note below
            //RemainingPrincipal: txn.BalancePrincipalAfter,
            RemainingPrincipal: txn.BalancePrincipalAfter ?? 0,
            GuardianName: txn.Loan.Customer.GuardianName
        );
    }

    var bytes = _pdfService.GeneratePaymentReceipt(dto);
    return File(bytes, "application/pdf", $"Receipt-{dto.ReceiptNumber}-{txn.Loan.LoanNumber}.pdf");
}


}
