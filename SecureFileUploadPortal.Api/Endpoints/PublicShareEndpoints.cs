using Microsoft.EntityFrameworkCore;
using SecureFileUploadPortal.Api.Constants;
using SecureFileUploadPortal.Api.Support;
using SecureFileUploadPortal.Application.Abstractions;
using SecureFileUploadPortal.Application.DTOs;
using SecureFileUploadPortal.Infrastructure.Data;

namespace SecureFileUploadPortal.Api.Endpoints;

public static class PublicShareEndpoints
{
    public static IEndpointRouteBuilder MapPublicShareEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/share/{token}", DownloadSharedFileAsync)
            .RequireRateLimiting("share")
            .WithTags("Public share links");
        return app;
    }

    private static async Task<IResult> DownloadSharedFileAsync(
        string token,
        AppDbContext db,
        ITokenService tokenService,
        IFileStorageService storage,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < 32)
            return Results.NotFound(new ApiError(ErrorMessages.ShareLinkNotFound));

        var tokenHash = tokenService.HashToken(token);
        var share = await db.ShareLinks
            .Include(s => s.UploadedFile)
            .SingleOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken);

        if (share is null || !share.CanBeUsed(DateTime.UtcNow))
        {
            db.AuditEvents.Add(EndpointHelpers.CreateAudit(http, null, "ShareDownloadDenied", "Invalid, expired, revoked, or exhausted share token."));
            await db.SaveChangesAsync(cancellationToken);
            return Results.NotFound(new ApiError(ErrorMessages.ShareLinkNotFoundOrExpired));
        }

        var file = share.UploadedFile;
        try
        {
            var stream = await storage.OpenReadAsync(file.StoredFileName, cancellationToken);
            share.DownloadCount++;
            db.AuditEvents.Add(EndpointHelpers.CreateAudit(http, share.CreatedByUserId, "SharedFileDownloaded", $"Shared download for file {file.Id}."));
            await db.SaveChangesAsync(cancellationToken);
            return Results.File(stream, file.ContentType, file.OriginalFileName);
        }
        catch (FileNotFoundException)
        {
            return Results.NotFound(new ApiError(ErrorMessages.StoredFileNotFound));
        }
    }
}
