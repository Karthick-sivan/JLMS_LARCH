using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.Models;
using System;
using System.Text;
using System.Threading.Tasks;

namespace JLMS.Api.Middleware;

public class UserContextMiddleware
{
    private readonly RequestDelegate _next;

    public UserContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, JlmsDbContext db)
    {
        var authHeader = context.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                var parts = decoded.Split(':');
                if (parts.Length >= 2 && int.TryParse(parts[0], out var userId))
                {
                    var user = await db.Users.AsNoTracking()
                        .Include(u => u.Role)
                        .Include(u => u.Branch)
                        .FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive);

                    if (user != null)
                    {
                        context.Items["CurrentUser"] = user;
                    }
                }
            }
            catch
            {
                // Ignore decoding issues and continue
            }
        }

        await _next(context);
    }
}
