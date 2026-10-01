using System.Security.Claims;
using GeoScenery.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace GeoScenery.Api.Middleware;

/// <summary>Immediately blocks existing authenticated sessions after an account is suspended.</summary>
public sealed class SuspendedAccountMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, MyProjectDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && !context.Request.Path.StartsWithSegments("/api/auth"))
        {
            var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (long.TryParse(userIdValue, out var userId)
                && await db.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.IsSuspended, context.RequestAborted))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Type = "https://httpstatuses.com/403",
                    Title = "Account suspended",
                    Status = StatusCodes.Status403Forbidden,
                    Detail = "This account is currently suspended. Contact support if you believe this is an error."
                }, context.RequestAborted);
                return;
            }
        }

        await next(context);
    }
}
