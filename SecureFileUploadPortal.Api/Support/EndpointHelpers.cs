using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SecureFileUploadPortal.Api.Extensions;
using SecureFileUploadPortal.Application.Abstractions;
using SecureFileUploadPortal.Application.DTOs;
using SecureFileUploadPortal.Application.Options;
using SecureFileUploadPortal.Domain.Entities;
using SecureFileUploadPortal.Infrastructure.Data;

namespace SecureFileUploadPortal.Api.Support;

public static class EndpointHelpers
{
    public static async Task<IResult> SignInAndReturnAuthResponseAsync(
        AppUser user,
        AppDbContext db,
        ITokenService tokenService,
        AppSessionOptions sessionOptions,
        HttpContext http)
    {
        var now = DateTime.UtcNow;
        var csrfToken = tokenService.CreateUrlSafeToken(32);
        var session = new AuthSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CsrfTokenHash = tokenService.HashToken(csrfToken),
            UserAgent = Truncate(http.Request.Headers.UserAgent.ToString(), 300),
            IpAddress = Truncate(http.Connection.RemoteIpAddress?.ToString() ?? "unknown", 64),
            CreatedAtUtc = now,
            LastSeenAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(sessionOptions.SessionLifetimeMinutes)
        };

        db.AuthSessions.Add(session);
        db.AuditEvents.Add(CreateAudit(http, user.Id, "SessionCreated", $"Server-side session {session.Id} created."));
        await db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimsPrincipalExtensions.SessionIdClaim, session.Id.ToString()),
            new(ClaimsPrincipalExtensions.SecurityStampClaim, user.SecurityStamp)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = session.ExpiresAtUtc,
                IssuedUtc = now
            });

        return Results.Ok(new AuthResponse(new AuthUserResponse(user.Id, user.FullName, user.Email), csrfToken));
    }

    public static AuditEvent CreateAudit(HttpContext http, string? userId, string action, string details)
    {
        return new AuditEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            Details = Truncate(details, 600),
            IpAddress = Truncate(http.Connection.RemoteIpAddress?.ToString() ?? "unknown", 64),
            UserAgent = Truncate(http.Request.Headers.UserAgent.ToString(), 300),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
