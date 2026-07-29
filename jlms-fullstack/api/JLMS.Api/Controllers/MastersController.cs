using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.Models;
using JLMS.Api.DTOs;

namespace JLMS.Api.Controllers;

[ApiController]
[Route("api/jewel-types")]
public class JewelTypesController : ControllerBase
{
    private readonly JlmsDbContext _db;
    public JewelTypesController(JlmsDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<JewelTypeDto>>> GetAll([FromQuery] bool activeOnly = false)
    {
        var query = _db.JewelTypes.AsNoTracking().AsQueryable();
        if (activeOnly) query = query.Where(j => j.IsActive);
        var items = await query.OrderBy(j => j.JewelTypeName).ToListAsync();
        return Ok(items.Select(j => new JewelTypeDto(j.JewelTypeId, j.JewelTypeName, j.Category, j.DefaultPurity, j.WastagePercent, j.IsActive, j.BranchId)));
    }

    [HttpPost]
    public async Task<ActionResult<JewelTypeDto>> Create([FromBody] JewelTypeCreateDto dto)
    {
        var user = await this.GetCurrentUserAsync(_db);
        var branchId = dto.BranchId ?? user?.BranchId;

        var entity = new JewelType
        {
            JewelTypeName = dto.JewelTypeName,
            Category = dto.Category,
            DefaultPurity = dto.DefaultPurity,
            WastagePercent = dto.WastagePercent,
            IsActive = dto.IsActive,
            BranchId = branchId
        };
        _db.JewelTypes.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(new JewelTypeDto(entity.JewelTypeId, entity.JewelTypeName, entity.Category, entity.DefaultPurity, entity.WastagePercent, entity.IsActive, entity.BranchId));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] JewelTypeCreateDto dto)
    {
        var entity = await _db.JewelTypes.FindAsync(id);
        if (entity == null) return NotFound();

        var user = await this.GetCurrentUserAsync(_db);
        var branchId = dto.BranchId ?? user?.BranchId ?? entity.BranchId;

        entity.JewelTypeName = dto.JewelTypeName;
        entity.Category = dto.Category;
        entity.DefaultPurity = dto.DefaultPurity;
        entity.WastagePercent = dto.WastagePercent;
        entity.IsActive = dto.IsActive;
        entity.BranchId = branchId;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

[ApiController]
[Route("api/gold-rates")]
public class GoldRatesController : ControllerBase
{
    private readonly JlmsDbContext _db;
    public GoldRatesController(JlmsDbContext db) => _db = db;

    [HttpGet("today")]
    public async Task<ActionResult<GoldRateDto>> GetToday()
    {
        var today = DateTime.UtcNow.Date;
        var rate = await _db.GoldRates.AsNoTracking()
            .Where(r => r.EffectiveDate <= today)
            .OrderByDescending(r => r.EffectiveDate)
            .FirstOrDefaultAsync();

        if (rate == null) return NotFound("No gold/silver rate has been set yet. Please add one via Gold/Silver Rate Master.");
        return Ok(new GoldRateDto(rate.GoldRateId, rate.EffectiveDate, rate.Rate24K, rate.Rate22K, rate.Rate18K, rate.SilverRate, rate.BranchId));
    }

    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<GoldRateDto>>> GetHistory([FromQuery] int days = 30)
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(-days);
        var rates = await _db.GoldRates.AsNoTracking()
            .Where(r => r.EffectiveDate >= cutoff)
            .OrderByDescending(r => r.EffectiveDate)
            .ToListAsync();
        return Ok(rates.Select(r => new GoldRateDto(r.GoldRateId, r.EffectiveDate, r.Rate24K, r.Rate22K, r.Rate18K, r.SilverRate, r.BranchId)));
    }

    [HttpPost]
    public async Task<ActionResult<GoldRateDto>> SetToday([FromBody] GoldRateCreateDto dto)
    {
        var today = DateTime.UtcNow.Date;
        var user = await this.GetCurrentUserAsync(_db);
        var branchId = dto.BranchId ?? user?.BranchId;

        var existing = await _db.GoldRates.FirstOrDefaultAsync(r => r.EffectiveDate == today && r.BranchId == branchId);

        if (existing != null)
        {
            existing.Rate24K = dto.Rate24K;
            existing.Rate22K = dto.Rate22K;
            existing.Rate18K = dto.Rate18K;
            existing.SilverRate = dto.SilverRate;
            existing.UpdatedBy = user?.UserId;
            await _db.SaveChangesAsync();
            return Ok(new GoldRateDto(existing.GoldRateId, existing.EffectiveDate, existing.Rate24K, existing.Rate22K, existing.Rate18K, existing.SilverRate, existing.BranchId));
        }

        var entity = new GoldRate
        {
            EffectiveDate = today,
            Rate24K = dto.Rate24K,
            Rate22K = dto.Rate22K,
            Rate18K = dto.Rate18K,
            SilverRate = dto.SilverRate,
            UpdatedBy = user?.UserId,
            BranchId = branchId,
            CreatedAt = DateTime.UtcNow
        };
        _db.GoldRates.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(new GoldRateDto(entity.GoldRateId, entity.EffectiveDate, entity.Rate24K, entity.Rate22K, entity.Rate18K, entity.SilverRate, entity.BranchId));
    }
}

[ApiController]
[Route("api/loan-schemes")]
public class LoanSchemesController : ControllerBase
{
    private readonly JlmsDbContext _db;
    public LoanSchemesController(JlmsDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LoanSchemeDto>>> GetAll([FromQuery] bool activeOnly = false)
    {
        var query = _db.LoanSchemes.AsNoTracking().AsQueryable();
        if (activeOnly) query = query.Where(s => s.IsActive);
        var items = await query.OrderBy(s => s.SchemeName).ToListAsync();
        return Ok(items.Select(s => new LoanSchemeDto(s.LoanSchemeId, s.SchemeName, s.InterestRatePct, s.TenureMonths, s.MaxLtvPercent, s.ProcessingFee, s.PenaltyRatePerDay, s.IsActive, s.BranchId)));
    }

    [HttpPost]
    public async Task<ActionResult<LoanSchemeDto>> Create([FromBody] LoanSchemeCreateDto dto)
    {
        var user = await this.GetCurrentUserAsync(_db);
        var branchId = dto.BranchId ?? user?.BranchId;

        var entity = new LoanScheme
        {
            SchemeName = dto.SchemeName,
            InterestRatePct = dto.InterestRatePct,
            TenureMonths = dto.TenureMonths,
            MaxLtvPercent = dto.MaxLtvPercent,
            ProcessingFee = dto.ProcessingFee,
            PenaltyRatePerDay = dto.PenaltyRatePerDay,
            IsActive = dto.IsActive,
            BranchId = branchId,
            CreatedAt = DateTime.UtcNow
        };
        _db.LoanSchemes.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(new LoanSchemeDto(entity.LoanSchemeId, entity.SchemeName, entity.InterestRatePct, entity.TenureMonths, entity.MaxLtvPercent, entity.ProcessingFee, entity.PenaltyRatePerDay, entity.IsActive, entity.BranchId));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] LoanSchemeCreateDto dto)
    {
        var entity = await _db.LoanSchemes.FindAsync(id);
        if (entity == null) return NotFound();

        var user = await this.GetCurrentUserAsync(_db);
        var branchId = dto.BranchId ?? user?.BranchId ?? entity.BranchId;

        entity.SchemeName = dto.SchemeName;
        entity.InterestRatePct = dto.InterestRatePct;
        entity.TenureMonths = dto.TenureMonths;
        entity.MaxLtvPercent = dto.MaxLtvPercent;
        entity.ProcessingFee = dto.ProcessingFee;
        entity.PenaltyRatePerDay = dto.PenaltyRatePerDay;
        entity.IsActive = dto.IsActive;
        entity.BranchId = branchId;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
