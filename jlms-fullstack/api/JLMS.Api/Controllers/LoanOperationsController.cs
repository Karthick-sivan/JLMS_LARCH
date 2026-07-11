using Microsoft.AspNetCore.Mvc;
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

    public LoanOperationsController(LoanOperationsService service)
    {
        _service = service;
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

    // POST /api/loan-operations/5/close
    [HttpPost("{loanId:int}/close")]
    public async Task<ActionResult<LoanOperationsClosureResponseDto>> CloseLoan(int loanId, [FromBody] LoanOperationsClosureRequestDto request)
    {
        try
        {
            return Ok(await _service.CloseLoanAsync(loanId, request));
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
}
