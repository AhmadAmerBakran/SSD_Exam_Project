using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using SecureFileUploadPortal.Api.Extensions;
using SecureFileUploadPortal.Application.Abstractions;
using SecureFileUploadPortal.Application.Options;
using SecureFileUploadPortal.Infrastructure.Data;
using SecureFileUploadPortal.Infrastructure.Security;
using SecureFileUploadPortal.Infrastructure.Storage;

namespace SecureFileUploadPortal.Api.Configuration;

public static class DependencyInjection
{
    public static IServiceCollection AddPortalServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PasswordHashingOptions>(configuration.GetSection("PasswordHashing"));
        services.Configure<UploadOptions>(configuration.GetSection("UploadSettings"));
        services.Configure<AppSessionOptions>(configuration.GetSection("SessionSettings"));

        var connectionString = configuration.GetConnectionString("Default") ?? "Data Source=secure_upload_portal.db";
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();

        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 6 * 1024 * 1024;
        });

        services.AddPortalAuthentication();
        services.AddAuthorization();
        services.AddPortalRateLimiting();

        return services;
    }

    private static IServiceCollection AddPortalAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "SecureFileUploadPortal.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.SlidingExpiration = false;
                options.LoginPath = "/";
                options.AccessDeniedPath = "/";
                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = ValidateServerSideSessionAsync
                };
            });

        return services;
    }

    private static IServiceCollection AddPortalRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("auth", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetRateLimitPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));
            options.AddPolicy("share", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));

            options.AddPolicy("upload", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetRateLimitPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(5),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));
        });

        return services;
    }

    private static string GetRateLimitPartitionKey(HttpContext httpContext)
    {
        return httpContext.User.Identity?.IsAuthenticated == true
            ? httpContext.User.GetUserId()
            : httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
    }

    private static async Task ValidateServerSideSessionAsync(CookieValidatePrincipalContext context)
    {
        try
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var sessionId = context.Principal?.GetSessionId();
            var userId = context.Principal?.GetUserId();
            var securityStamp = context.Principal?.GetSecurityStamp();

            if (sessionId is null || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(securityStamp))
            {
                await RejectAndSignOutAsync(context);
                return;
            }

            var now = DateTime.UtcNow;
            var session = await db.AuthSessions
                .Include(s => s.User)
                .SingleOrDefaultAsync(s => s.Id == sessionId.Value &&
                                           s.UserId == userId &&
                                           s.RevokedAtUtc == null &&
                                           s.ExpiresAtUtc > now);

            if (session is null || !string.Equals(session.User.SecurityStamp, securityStamp, StringComparison.Ordinal))
            {
                await RejectAndSignOutAsync(context);
                return;
            }

            session.LastSeenAtUtc = now;
            await db.SaveChangesAsync();
        }
        catch
        {
            await RejectAndSignOutAsync(context);
        }
    }

    private static async Task RejectAndSignOutAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
