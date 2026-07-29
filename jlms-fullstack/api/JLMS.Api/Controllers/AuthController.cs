using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.DTOs;

namespace JLMS.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly JlmsDbContext _db;
    public AuthController(JlmsDbContext db) => _db = db;

    // POST /api/auth/login
    // NOTE: This is a simple SHA256-hash check suitable for local testing.
    // It is NOT production-grade auth (no salting, no JWT expiry, no refresh
    // tokens). See README "Security notes" before deploying this anywhere
    // beyond your own test machine.
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Username and password are required.");

        var user = await _db.Users.AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.Branch)
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive);

        if (user == null) return Unauthorized("Invalid username or password.");

        var hash = ComputeSha256(request.Password);
        if (!string.Equals(hash, user.PasswordHash, StringComparison.OrdinalIgnoreCase))
            return Unauthorized("Invalid username or password.");

        // Block login if the user's branch has been deactivated.
        if (user.Branch != null && !user.Branch.IsActive)
            return Unauthorized("Your branch is currently inactive. Please contact the administrator.");

        if (user.RoleId != 4 && user.RoleId != 1002 && user.BranchId.HasValue)
        {
            var branchAdmin = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.BranchId == user.BranchId.Value && u.RoleId == 4);

            if (branchAdmin != null && !branchAdmin.IsActive)
                return Unauthorized("Your branch inactive. Please contact Admin.");
        }
        // Validate that the user belongs to the selected branch.
        // If a branch was supplied on the login form, the user's BranchId must match.
        if (request.BranchId.HasValue && request.BranchId.Value > 0 && user.BranchId != request.BranchId.Value)
            return Unauthorized("Invalid username or password for the selected branch.");

        // Simple opaque token for this test build (not a real JWT).
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user.UserId}:{user.Username}:{DateTime.UtcNow.Ticks}"));

        return Ok(new LoginResponse(
            user.UserId, user.FullName, user.Username,
            user.Role?.RoleName ?? "", user.Branch?.BranchName ?? "", token, user.BranchId, user.RoleId));
    }

    // POST /api/auth/change-password
    [HttpPost("change-password")]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        // Identify caller from the simple base64 token: userId:username:ticks
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
            return Unauthorized("Authentication required.");

        var tokenPart = authHeader.Substring(7);
        int callerId;
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(tokenPart));
            callerId = int.Parse(decoded.Split(':')[0]);
        }
        catch { return Unauthorized("Invalid token."); }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == callerId && u.IsActive);
        if (user == null) return Unauthorized("User not found.");

        // Verify current password
        var currentHash = ComputeSha256(request.CurrentPassword);
        if (!string.Equals(currentHash, user.PasswordHash, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Current password is incorrect.");

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 4)
            return BadRequest("New password must be at least 4 characters.");

        user.PasswordHash = ComputeSha256(request.NewPassword);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Password changed successfully." });
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder();
        foreach (var b in bytes) sb.Append(b.ToString("X2"));
        return sb.ToString();
    }
}
