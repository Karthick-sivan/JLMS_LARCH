using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.DTOs;
using JLMS.Api.Services;

namespace JLMS.Api.Controllers;

[ApiController]
[Route("api/jewel-appraisal")]
public class JewelAppraisalController : ControllerBase
{
    private readonly JlmsDbContext _db;
    private readonly LoanCalculationService _calc;

    public JewelAppraisalController(JlmsDbContext db, LoanCalculationService calc)
    {
        _db = db;
        _calc = calc;
    }

    // POST /api/jewel-appraisal/calculate
    // Calculates valuation live (does NOT save to DB) so the officer can
    // adjust items before committing to a New Loan.
    [HttpPost("calculate")]
    public async Task<ActionResult<AppraisalResultDto>> Calculate([FromBody] AppraisalRequestDto request)
    {
        if (request.Items == null || request.Items.Count == 0)
            return BadRequest("At least one jewel item is required.");

        var today = DateTime.UtcNow.Date;
        var goldRate = await _db.GoldRates.AsNoTracking()
            .Where(r => r.EffectiveDate <= today)
            .OrderByDescending(r => r.EffectiveDate)
            .FirstOrDefaultAsync();

        if (goldRate == null)
            return BadRequest("No gold rate configured. Please set today's rate in Gold Rate Master before appraising.");

        var jewelTypeIds = request.Items.Select(i => i.JewelTypeId).Distinct().ToList();
        var jewelTypes = await _db.JewelTypes.AsNoTracking()
            .Where(jt => jewelTypeIds.Contains(jt.JewelTypeId))
            .ToDictionaryAsync(jt => jt.JewelTypeId);

        var results = new List<JewelItemResultDto>();
        decimal totalGross = 0, totalStone = 0, totalNet = 0, totalValue = 0;

        foreach (var item in request.Items)
        {
            if (!jewelTypes.TryGetValue(item.JewelTypeId, out var jewelType))
                return BadRequest($"Unknown JewelTypeId {item.JewelTypeId}. Please check Jewel Type Master.");

            var purity = item.Purity ?? jewelType.DefaultPurity;
            var (netWeight, marketValue) = _calc.CalculateJewelValue(
                item.GrossWeightGrams, item.StoneWeightGrams, purity,
                goldRate.Rate24K, goldRate.Rate22K, goldRate.Rate18K);

            var lineNetWeight = netWeight * item.Quantity;
            var lineMarketValue = marketValue * item.Quantity;

            results.Add(new JewelItemResultDto(0, jewelType.JewelTypeName, item.Quantity,
                item.GrossWeightGrams, item.StoneWeightGrams, netWeight, purity, lineMarketValue));

            totalGross += item.GrossWeightGrams * item.Quantity;
            totalStone += item.StoneWeightGrams * item.Quantity;
            totalNet += lineNetWeight;
            totalValue += lineMarketValue;
        }

        return Ok(new AppraisalResultDto(results, totalGross, totalStone, totalNet, goldRate.Rate22K, totalValue));
    }
}
