using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.Models;
using JLMS.Api.DTOs;

namespace JLMS.Api.Controllers;

[ApiController]
[Route("api/branches")]
public class BranchesController : ControllerBase
{
    private readonly JlmsDbContext _db;

    public BranchesController(JlmsDbContext db)
    {
        _db = db;
    }

    private bool CheckSuperAdmin()
    {
        var currentUser = HttpContext.Items["CurrentUser"] as User;
        return currentUser.IsSuperAdmin();
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BranchDto>>> GetAll()
    {
        if (!CheckSuperAdmin()) return Forbid();

        var branches = await _db.Branches.AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        var dtos = branches.Select(b => new BranchDto(
            b.BranchId,
            b.BranchCode,
            b.BranchName,
            b.City,
            b.State,
            b.IsActive,
            b.CreatedAt
        ));

        return Ok(dtos);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BranchDto>> GetById(int id)
    {
        if (!CheckSuperAdmin()) return Forbid();

        var b = await _db.Branches.FindAsync(id);
        if (b == null) return NotFound();

        return Ok(new BranchDto(
            b.BranchId,
            b.BranchCode,
            b.BranchName,
            b.City,
            b.State,
            b.IsActive,
            b.CreatedAt
        ));
    }

    [HttpPost]
    public async Task<ActionResult<BranchDto>> Create([FromBody] BranchCreateDto dto)
    {
        if (!CheckSuperAdmin()) return Forbid();

        if (string.IsNullOrWhiteSpace(dto.BranchCode))
            return BadRequest("Branch Code is required.");
        if (string.IsNullOrWhiteSpace(dto.BranchName))
            return BadRequest("Branch Name is required.");

        var codeUpper = dto.BranchCode.Trim().ToUpperInvariant();
        if (await _db.Branches.AnyAsync(b => b.BranchCode.ToUpper() == codeUpper))
            return BadRequest($"Branch Code '{dto.BranchCode}' is already registered.");

        var branch = new Branch
        {
            BranchCode = dto.BranchCode.Trim(),
            BranchName = dto.BranchName.Trim(),
            City = dto.City?.Trim(),
            State = dto.State?.Trim(),
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();

        return Ok(new BranchDto(
            branch.BranchId,
            branch.BranchCode,
            branch.BranchName,
            branch.City,
            branch.State,
            branch.IsActive,
            branch.CreatedAt
        ));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] BranchCreateDto dto)
    {
        if (!CheckSuperAdmin()) return Forbid();

        var branch = await _db.Branches.FindAsync(id);
        if (branch == null) return NotFound();

        if (string.IsNullOrWhiteSpace(dto.BranchCode))
            return BadRequest("Branch Code is required.");
        if (string.IsNullOrWhiteSpace(dto.BranchName))
            return BadRequest("Branch Name is required.");

        var codeUpper = dto.BranchCode.Trim().ToUpperInvariant();
        if (await _db.Branches.AnyAsync(b => b.BranchCode.ToUpper() == codeUpper && b.BranchId != id))
            return BadRequest($"Branch Code '{dto.BranchCode}' is already in use by another branch.");

        branch.BranchCode = dto.BranchCode.Trim();
        branch.BranchName = dto.BranchName.Trim();
        branch.City = dto.City?.Trim();
        branch.State = dto.State?.Trim();
        branch.IsActive = dto.IsActive;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:int}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        if (!CheckSuperAdmin()) return Forbid();

        var branch = await _db.Branches.FindAsync(id);
        if (branch == null) return NotFound();

        branch.IsActive = !branch.IsActive;
        await _db.SaveChangesAsync();

        return Ok(new { branchId = branch.BranchId, isActive = branch.IsActive });
    }
}
