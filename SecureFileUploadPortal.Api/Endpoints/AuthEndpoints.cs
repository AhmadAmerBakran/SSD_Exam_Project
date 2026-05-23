using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecureFileUploadPortal.Api.Extensions;
using SecureFileUploadPortal.Api.Support;
using SecureFileUploadPortal.Application.Abstractions;
using SecureFileUploadPortal.Application.DTOs;
using SecureFileUploadPortal.Application.Options;
using SecureFileUploadPortal.Application.Validation;
using SecureFileUploadPortal.Domain.Entities;
using SecureFileUploadPortal.Infrastructure.Data;

namespace SecureFileUploadPortal.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Authentication");

        auth.MapPost("/register", RegisterAsync).RequireRateLimiting("auth");
        auth.MapPost("/login", LoginAsync).RequireRateLimiting("auth");
        auth.MapGet("/me", GetCurrentUserAsync).RequireAuthorization();
        auth.MapPost("/logout", LogoutAsync).RequireAuthorization().RequireCsrf();

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        [FromBody] RegisterRequest request,
        AppDbContext db,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IOptions<AppSessionOptions> sessionOptions,
        HttpContext http)
    {
        var errors = InputValidator.ValidateRegistration(request.FullName, request.Email, request.Password, request.ConfirmPassword);
        if (errors.Count > 0) return Results.BadRequest(new ApiError("Registration validation failed.", errors));

        var normalizedEmail = InputValidator.NormalizeEmail(request.Email);
        var emailExists = await db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail);
        if (emailExists) return Results.Conflict(new ApiError("A user with this email address already exists."));

        var passwordHash = passwordHasher.HashPassword(request.Password);
        var user = new AppUser
        {
            Id = Guid.NewGuid().ToString("N"),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = passwordHash.Hash,
            PasswordSalt = passwordHash.Salt,
            PasswordAlgorithm = passwordHash.Algorithm,
            PasswordMemorySizeKb = passwordHash.MemorySizeKb,
            PasswordIterations = passwordHash.Iterations,
            PasswordDegreeOfParallelism = passwordHash.DegreeOfParallelism,
            PasswordHashLengthBytes = passwordHash.HashLengthBytes,
            SecurityStamp = tokenService.CreateUrlSafeToken(32),
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Users.Add(user);
        db.AuditEvents.Add(EndpointHelpers.CreateAudit(http, user.Id, "UserRegistered", "A new user account was created."));
        await db.SaveChangesAsync();

        return await EndpointHelpers.SignInAndReturnAuthResponseAsync(user, db, tokenService, sessionOptions.Value, http);
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] LoginRequest request,
        AppDbContext db,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IOptions<AppSessionOptions> sessionOptions,
        HttpContext http)
    {
        var errors = InputValidator.ValidateLogin(request.Email, request.Password);
        if (errors.Count > 0) return Results.BadRequest(new ApiError("Login validation failed.", errors));

        var normalizedEmail = InputValidator.NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
        if (user is null || !passwordHasher.VerifyPassword(request.Password, user))
        {
            db.AuditEvents.Add(EndpointHelpers.CreateAudit(http, null, "LoginFailed", $"Invalid login attempt for {normalizedEmail}."));
            await db.SaveChangesAsync();
            return Results.Unauthorized();
        }

        if (passwordHasher.NeedsRehash(user))
        {
            var upgraded = passwordHasher.HashPassword(request.Password);
            user.PasswordHash = upgraded.Hash;
            user.PasswordSalt = upgraded.Salt;
            user.PasswordAlgorithm = upgraded.Algorithm;
            user.PasswordMemorySizeKb = upgraded.MemorySizeKb;
            user.PasswordIterations = upgraded.Iterations;
            user.PasswordDegreeOfParallelism = upgraded.DegreeOfParallelism;
            user.PasswordHashLengthBytes = upgraded.HashLengthBytes;
        }

        db.AuditEvents.Add(EndpointHelpers.CreateAudit(http, user.Id, "LoginSucceeded", "User logged in."));
        await db.SaveChangesAsync();

        return await EndpointHelpers.SignInAndReturnAuthResponseAsync(user, db, tokenService, sessionOptions.Value, http);
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        AppDbContext db,
        ITokenService tokenService)
    {
        var userId = principal.GetUserId();
        var sessionId = principal.GetSessionId();
        var user = await db.Users.FindAsync(userId);
        var session = await db.AuthSessions.FindAsync(sessionId);

        if (user is null || session is null || !session.IsActive(DateTime.UtcNow)) return Results.Unauthorized();

        var freshCsrfToken = tokenService.CreateUrlSafeToken(32);
        session.CsrfTokenHash = tokenService.HashToken(freshCsrfToken);
        session.LastSeenAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Results.Ok(new AuthResponse(new AuthUserResponse(user.Id, user.FullName, user.Email), freshCsrfToken));
    }

    private static async Task<IResult> LogoutAsync(
        ClaimsPrincipal principal,
        AppDbContext db,
        HttpContext http)
    {
        var sessionId = principal.GetSessionId();
        var session = await db.AuthSessions.FindAsync(sessionId);
        if (session is not null)
        {
            session.RevokedAtUtc = DateTime.UtcNow;
            db.AuditEvents.Add(EndpointHelpers.CreateAudit(http, session.UserId, "Logout", "User logged out and server session was revoked."));
            await db.SaveChangesAsync();
        }

        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Ok(new { message = "Logged out." });
    }
}
