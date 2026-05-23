using SecureFileUploadPortal.Api.Endpoints;
using SecureFileUploadPortal.Api.Extensions;
using SecureFileUploadPortal.Infrastructure.Data;

namespace SecureFileUploadPortal.Api.Configuration;

public static class ApplicationBuilderExtensions
{
    public static async Task EnsureDatabaseCreatedAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public static WebApplication ConfigureRequestPipeline(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/error");
        }

        app.UseSecurityHeaders();
        app.UseRateLimiter();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHealthEndpoints();
        app.MapAuthEndpoints();
        app.MapFileEndpoints();
        app.MapPublicShareEndpoints();

        return app;
    }

    private static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow })).WithTags("Health");
        app.MapGet("/error", () => Results.Problem("An unexpected error occurred.")).WithTags("Errors");
    }
}
