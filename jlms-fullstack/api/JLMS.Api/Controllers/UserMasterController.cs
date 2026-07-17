using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.Models;
using JLMS.Api.DTOs;

namespace JLMS.Api.Controllers;

[ApiController]
[Route("api/user-master")]
public class UserMasterController : ControllerBase
{
    private readonly JlmsDbContext _db;

    public UserMasterController(JlmsDbContext db)
    {
        _db = db;
    }

    // GET /api/user-master
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserMasterDto>>> GetAll()
    {
        var currentUser = HttpContext.Items["CurrentUser"] as JLMS.Api.Models.User;
        var filterBranchId = currentUser?.GetFilterBranchId();

        var query = _db.Users.AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.Branch)
            .AsQueryable();

        // Scope to the logged-in user's branch
        if (filterBranchId.HasValue)
            query = query.Where(u => u.BranchId == filterBranchId.Value);

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        var dtos = users.Select(u => new UserMasterDto(
            u.UserId,
            u.EmployeeCode,
            u.FullName,
            u.Username,
            u.RoleId,
            u.Role?.RoleName ?? "No Role",
            u.BranchId,
            u.Branch?.BranchName ?? "No Branch",
            u.Mobile,
            u.Email,
            u.IsActive,
            u.CreatedAt
        ));

        return Ok(dtos);
    }

    // GET /api/user-master/branches
    [HttpGet("branches")]
    public async Task<ActionResult<IEnumerable<BranchOptionDto>>> GetBranches()
    {
        var branches = await _db.Branches.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.BranchName)
            .Select(b => new BranchOptionDto(b.BranchId, b.BranchName, b.BranchCode))
            .ToListAsync();

        return Ok(branches);
    }

    // GET /api/user-master/roles
    [HttpGet("roles")]
    public async Task<ActionResult<IEnumerable<RoleOptionDto>>> GetRoles()
    {
        var roles = await _db.Roles.AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.RoleName)
            .Select(r => new RoleOptionDto(r.RoleId, r.RoleName))
            .ToListAsync();

        return Ok(roles);
    }

    // POST /api/user-master
    [HttpPost]
    public async Task<ActionResult<UserMasterDto>> Create([FromBody] UserCreateDto dto)
    {
        // Force new user into the current user's branch
        var currentUser = HttpContext.Items["CurrentUser"] as JLMS.Api.Models.User;
        var filterBranchId = currentUser?.GetFilterBranchId();
        // Use the logged-in user's branch when set; otherwise fall back to the DTO value
        var effectiveBranchId = filterBranchId ?? dto.BranchId;

        if (string.IsNullOrWhiteSpace(dto.EmployeeCode))
            return BadRequest("Employee Code is required.");
        if (string.IsNullOrWhiteSpace(dto.FullName))
            return BadRequest("Full Name is required.");
        if (string.IsNullOrWhiteSpace(dto.Username))
            return BadRequest("Username is required.");
        if (string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest("Password is required.");
        if (dto.RoleId <= 0)
            return BadRequest("Valid Role is required.");
        if (effectiveBranchId <= 0)
            return BadRequest("Valid Branch is required.");

        // Check uniqueness
        if (await _db.Users.AnyAsync(u => u.EmployeeCode == dto.EmployeeCode))
            return BadRequest($"Employee Code '{dto.EmployeeCode}' is already registered.");
        if (await _db.Users.AnyAsync(u => u.Username == dto.Username))
            return BadRequest($"Username '{dto.Username}' is already taken.");
        if (!string.IsNullOrWhiteSpace(dto.Mobile) && await _db.Users.AnyAsync(u => u.Mobile == dto.Mobile.Trim()))
            return BadRequest($"Mobile number '{dto.Mobile}' is already registered to another user.");
        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var emailStr = dto.Email.Trim();
            if (!IsValidEmail(emailStr))
                return BadRequest("Invalid email address format.");
            if (await _db.Users.AnyAsync(u => u.Email == emailStr))
                return BadRequest($"Email address '{dto.Email}' is already registered to another user.");
        }

        // Check if role and branch exist
        var role = await _db.Roles.FindAsync(dto.RoleId);
        if (role == null) return BadRequest("Selected Role does not exist.");

        var branch = await _db.Branches.FindAsync(effectiveBranchId);
        if (branch == null) return BadRequest("Selected Branch does not exist.");

        var user = new User
        {
            EmployeeCode = dto.EmployeeCode.Trim(),
            FullName = dto.FullName.Trim(),
            Username = dto.Username.Trim().ToLowerInvariant(),
            PasswordHash = ComputeSha256(dto.Password),
            RoleId = dto.RoleId,
            BranchId = effectiveBranchId,
            Mobile = dto.Mobile?.Trim(),
            Email = dto.Email?.Trim(),
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new UserMasterDto(
            user.UserId,
            user.EmployeeCode,
            user.FullName,
            user.Username,
            user.RoleId,
            role.RoleName,
            user.BranchId,
            branch.BranchName,
            user.Mobile,
            user.Email,
            user.IsActive,
            user.CreatedAt
        ));
    }

    // PUT /api/user-master/{id:int}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UserUpdateDto dto)
    {
        var currentUser = HttpContext.Items["CurrentUser"] as JLMS.Api.Models.User;
        var filterBranchId = currentUser?.GetFilterBranchId();
        // Use the logged-in user's branch when set; otherwise fall back to the DTO value
        var effectiveBranchId = filterBranchId ?? dto.BranchId;

        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        // Prevent editing users from other branches
        if (filterBranchId.HasValue && user.BranchId != filterBranchId.Value)
            return Forbid();

        if (string.IsNullOrWhiteSpace(dto.EmployeeCode))
            return BadRequest("Employee Code is required.");
        if (string.IsNullOrWhiteSpace(dto.FullName))
            return BadRequest("Full Name is required.");
        if (string.IsNullOrWhiteSpace(dto.Username))
            return BadRequest("Username is required.");
        if (dto.RoleId <= 0)
            return BadRequest("Valid Role is required.");
        if (effectiveBranchId <= 0)
            return BadRequest("Valid Branch is required.");

        // Check uniqueness
        if (await _db.Users.AnyAsync(u => u.EmployeeCode == dto.EmployeeCode && u.UserId != id))
            return BadRequest($"Employee Code '{dto.EmployeeCode}' is already in use by another user.");
        if (await _db.Users.AnyAsync(u => u.Username == dto.Username && u.UserId != id))
            return BadRequest($"Username '{dto.Username}' is already taken by another user.");
        if (!string.IsNullOrWhiteSpace(dto.Mobile) && await _db.Users.AnyAsync(u => u.Mobile == dto.Mobile.Trim() && u.UserId != id))
            return BadRequest($"Mobile number '{dto.Mobile}' is already registered to another user.");
        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var emailStr = dto.Email.Trim();
            if (!IsValidEmail(emailStr))
                return BadRequest("Invalid email address format.");
            if (await _db.Users.AnyAsync(u => u.Email == emailStr && u.UserId != id))
                return BadRequest($"Email address '{dto.Email}' is already registered to another user.");
        }

        // Check if role and branch exist
        var role = await _db.Roles.FindAsync(dto.RoleId);
        if (role == null) return BadRequest("Selected Role does not exist.");

        var branch = await _db.Branches.FindAsync(dto.BranchId);
        if (branch == null) return BadRequest("Selected Branch does not exist.");

        user.EmployeeCode = dto.EmployeeCode.Trim();
        user.FullName = dto.FullName.Trim();
        user.Username = dto.Username.Trim().ToLowerInvariant();
        user.RoleId = dto.RoleId;
        user.BranchId = effectiveBranchId;
        user.Mobile = dto.Mobile?.Trim();
        user.Email = dto.Email?.Trim();
        user.IsActive = dto.IsActive;

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            user.PasswordHash = ComputeSha256(dto.Password);
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // PATCH /api/user-master/{id:int}/toggle-status
    [HttpPatch("{id:int}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var currentUser = HttpContext.Items["CurrentUser"] as JLMS.Api.Models.User;
        var filterBranchId = currentUser?.GetFilterBranchId();

        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        // Prevent toggling users from other branches
        if (filterBranchId.HasValue && user.BranchId != filterBranchId.Value)
            return Forbid();

        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync();

        return Ok(new { userId = user.UserId, isActive = user.IsActive });
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder();
        foreach (var b in bytes) sb.Append(b.ToString("X2"));
        return sb.ToString();
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
