using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.Models;

namespace JLMS.Api.Controllers;

public static class ControllerExtensions
{
    public static async Task<User?> GetCurrentUserAsync(this ControllerBase controller, JlmsDbContext db)
    {
        var authHeader = controller.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = decoded.Split(':');
            if (parts.Length < 2) return null;

            var userIdStr = parts[0];
            if (!int.TryParse(userIdStr, out var userId)) return null;

            return await db.Users.AsNoTracking()
                .Include(u => u.Role)
                .Include(u => u.Branch)
                .FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive);
        }
        catch
        {
            return null;
        }
    }

    public static int? GetFilterBranchId(this User user)
    {
        if (user == null) return null;
        // Filter always applies — branch scope is set by user's BranchId, not role.
        return user.BranchId;
    }

    public static bool CanAccessBranch(this User user, int branchId)
    {
        if (user == null) return false;
        return user.BranchId == branchId;
    }
}
