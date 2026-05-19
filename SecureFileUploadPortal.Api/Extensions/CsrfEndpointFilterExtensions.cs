using Microsoft.EntityFrameworkCore;
using SecureFileUploadPortal.Application.Abstractions;
using SecureFileUploadPortal.Infrastructure.Data;

namespace SecureFileUploadPortal.Api.Extensions;

public static class CsrfEndpointFilterExtensions
{
    public static RouteHandlerBuilder RequireCsrf(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            if (http.User.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();

            var providedToken = http.Request.Headers["X-CSRF-Token"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(providedToken))
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "CSRF token is missing.");

            var db = http.RequestServices.GetRequiredService<AppDbContext>();
            var tokenService = http.RequestServices.GetRequiredService<ITokenService>();
            var sessionId = http.User.GetSessionId();
            var now = DateTime.UtcNow;

            var session = await db.AuthSessions.SingleOrDefaultAsync(s => s.Id == sessionId && s.RevokedAtUtc == null && s.ExpiresAtUtc > now);
            if (session is null)
                return Results.Unauthorized();

            var providedHash = tokenService.HashToken(providedToken);
            if (!tokenService.FixedTimeEquals(providedHash, session.CsrfTokenHash))
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "CSRF token is invalid.");

            return await next(context);
        });
    }
}
