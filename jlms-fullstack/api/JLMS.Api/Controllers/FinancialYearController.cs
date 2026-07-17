using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.Models;
using JLMS.Api.DTOs;

namespace JLMS.Api.Controllers;

[ApiController]
[Route("api/financial-years")]
public class FinancialYearController : ControllerBase
{
    private readonly JlmsDbContext _db;
    public FinancialYearController(JlmsDbContext db) => _db = db;

    // GET /api/financial-years?activeOnly=true
    [HttpGet]
    public async Task<ActionResult<List<FinancialYearDto>>> GetAll([FromQuery] bool activeOnly = false)
    {
        var query = _db.FinancialYears.AsNoTracking().AsQueryable();
        if (activeOnly) query = query.Where(f => f.Status == "A");

        var rows = await query
            .OrderByDescending(f => f.FromDt).ThenBy(f => f.GoldLoanType)
            .ToListAsync();

        return Ok(rows.Select(ToDto).ToList());
    }

    // GET /api/financial-years/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<FinancialYearDto>> GetById(int id)
    {
        var f = await _db.FinancialYears.AsNoTracking().FirstOrDefaultAsync(x => x.FinancialYearId == id);
        if (f == null) return NotFound();
        return Ok(ToDto(f));
    }

    // POST /api/financial-years
    [HttpPost]
    public async Task<ActionResult<FinancialYearDto>> Create([FromBody] FinancialYearCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.GoldLoanType) || string.IsNullOrWhiteSpace(dto.Prefix))
            return BadRequest("Code, GoldLoanType and Prefix are required.");

        if (dto.ToDt < dto.FromDt)
            return BadRequest("ToDt cannot be before FromDt.");

        // Guard against two overlapping ACTIVE ranges for the same numbering type —
        // that would make GetActiveAsync ambiguous about which prefix to use.
        var overlap = await _db.FinancialYears.AnyAsync(f =>
            f.GoldLoanType == dto.GoldLoanType && f.Status == "A" &&
            f.FromDt <= dto.ToDt && f.ToDt >= dto.FromDt);
        if (overlap)
            return Conflict($"An active '{dto.GoldLoanType}' financial year already covers part of that date range.");

        var entity = new FinancialYear
        {
            Code = dto.Code,
            GoldLoanType = dto.GoldLoanType,
            FromDt = dto.FromDt,
            ToDt = dto.ToDt,
            GoldLoanNoStartsFrom = dto.GoldLoanNoStartsFrom <= 0 ? 1 : dto.GoldLoanNoStartsFrom,
            Prefix = dto.Prefix,
            Suffix = dto.Suffix,
            Status = "A",
            CreatedDt = DateTime.UtcNow,
            CreatedBy = dto.CreatedBy
        };

        _db.FinancialYears.Add(entity);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = entity.FinancialYearId }, ToDto(entity));
    }

    // PUT /api/financial-years/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] FinancialYearUpdateDto dto)
    {
        var entity = await _db.FinancialYears.FindAsync(id);
        if (entity == null) return NotFound();

        if (dto.ToDt < dto.FromDt)
            return BadRequest("ToDt cannot be before FromDt.");

        if (dto.Status == "A")
        {
            var overlap = await _db.FinancialYears.AnyAsync(f =>
                f.FinancialYearId != id &&
                f.GoldLoanType == dto.GoldLoanType && f.Status == "A" &&
                f.FromDt <= dto.ToDt && f.ToDt >= dto.FromDt);
            if (overlap)
                return Conflict($"An active '{dto.GoldLoanType}' financial year already covers part of that date range.");
        }

        entity.Code = dto.Code;
        entity.GoldLoanType = dto.GoldLoanType;
        entity.FromDt = dto.FromDt;
        entity.ToDt = dto.ToDt;
        entity.GoldLoanNoStartsFrom = dto.GoldLoanNoStartsFrom <= 0 ? 1 : dto.GoldLoanNoStartsFrom;
        entity.Prefix = dto.Prefix;
        entity.Suffix = dto.Suffix;
        entity.Status = dto.Status;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/financial-years/5/deactivate  (soft delete — keeps history for audit)
    [HttpPost("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var entity = await _db.FinancialYears.FindAsync(id);
        if (entity == null) return NotFound();

        entity.Status = "I";
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static FinancialYearDto ToDto(FinancialYear f) => new(
        f.FinancialYearId, f.Code, f.GoldLoanType, f.FromDt, f.ToDt,
        f.GoldLoanNoStartsFrom, f.Prefix, f.Suffix, f.Status, f.CreatedDt, f.CreatedBy);
}
