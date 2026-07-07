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

        // Simple opaque token for this test build (not a real JWT).
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user.UserId}:{user.Username}:{DateTime.UtcNow.Ticks}"));

        return Ok(new LoginResponse(
            user.UserId, user.FullName, user.Username,
            user.Role?.RoleName ?? "", user.Branch?.BranchName ?? "", token));
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder();
        foreach (var b in bytes) sb.Append(b.ToString("X2"));
        return sb.ToString();
    }
}
