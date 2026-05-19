using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecureFileUploadPortal.Api.Constants;
using SecureFileUploadPortal.Api.Extensions;
using SecureFileUploadPortal.Api.Support;
using SecureFileUploadPortal.Application.Abstractions;
using SecureFileUploadPortal.Application.DTOs;
using SecureFileUploadPortal.Application.Options;
using SecureFileUploadPortal.Application.Validation;
using SecureFileUploadPortal.Domain.Entities;
using SecureFileUploadPortal.Infrastructure.Data;

namespace SecureFileUploadPortal.Api.Endpoints;

public static class FileEndpoints
{
    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        var files = app.MapGroup("/api/files")
            .RequireAuthorization()
            .WithTags("Files");

        files.MapGet("/", ListOwnFilesAsync);
        files.MapPost("/", UploadFileAsync).RequireCsrf().RequireRateLimiting("upload");
        files.MapGet("/{fileId:guid}/download", DownloadOwnFileAsync);
        files.MapDelete("/{fileId:guid}", DeleteOwnFileAsync).RequireCsrf();
        files.MapPost("/{fileId:guid}/shares", CreateShareLinkAsync).RequireCsrf().RequireRateLimiting("upload");
        files.MapGet("/{fileId:guid}/shares", ListShareLinksAsync);
        files.MapDelete("/{fileId:guid}/shares/{shareId:guid}", RevokeShareLinkAsync).RequireCsrf();

        return app;
    }

    private static async Task<IResult> ListOwnFilesAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.GetUserId();
        var result = await db.UploadedFiles
            .Where(f => f.OwnerUserId == userId)
            .OrderByDescending(f => f.UploadedAtUtc)
            .Select(f => new FileListItemResponse(f.Id, f.OriginalFileName, f.ContentType, f.SizeBytes, f.Sha256Hash, f.UploadedAtUtc))
            .ToListAsync();

        return Results.Ok(result);
    }

    private static async Task<IResult> UploadFileAsync(
        HttpContext http,
        ClaimsPrincipal principal,
        AppDbContext db,
        IFileStorageService storage,
        IOptions<UploadOptions> uploadOptions,
        CancellationToken cancellationToken)
    {
        var options = uploadOptions.Value;
        var userId = principal.GetUserId();

        if (!http.Request.HasFormContentType)
            return Results.BadRequest(new ApiError("Request must be multipart/form-data."));

        var form = await http.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
            return Results.BadRequest(new ApiError("No file was uploaded."));

        if (file.Length > options.MaxFileSizeBytes)
            return Results.BadRequest(new ApiError($"File exceeds the maximum size of {options.MaxFileSizeBytes} bytes."));

        var originalFileName = Path.GetFileName(file.FileName).Trim();
        var displayNameErrors = InputValidator.ValidateDisplayFileName(originalFileName);
        if (displayNameErrors.Count > 0)
            return Results.BadRequest(new ApiError("File name validation failed.", displayNameErrors));

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (!FileValidationHelper.IsAllowedExtension(extension, options.AllowedExtensions))
            return Results.BadRequest(new ApiError("File extension is not allowed."));

        if (!FileValidationHelper.IsAllowedMimeType(extension, file.ContentType))
            return Results.BadRequest(new ApiError("File content type does not match the allowed list."));

        if (!await FileValidationHelper.HasValidSignatureAsync(file, extension, cancellationToken))
            return Results.BadRequest(new ApiError("File signature does not match the file type."));

        var storedFileName = storage.CreateStoredFileName(originalFileName);
        var sha256Hash = await CalculateSha256HashAsync(file, cancellationToken);

        await using (var saveStream = file.OpenReadStream())
        {
            await storage.SaveAsync(saveStream, storedFileName, cancellationToken);
        }

        var uploadedFile = new UploadedFile
        {
            Id = Guid.NewGuid(),
            OwnerUserId = userId,
            OriginalFileName = originalFileName,
            StoredFileName = storedFileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            Sha256Hash = sha256Hash,
            UploadedAtUtc = DateTime.UtcNow
        };

        db.UploadedFiles.Add(uploadedFile);
        db.AuditEvents.Add(EndpointHelpers.CreateAudit(http, userId, "FileUploaded", $"Uploaded file {uploadedFile.Id}."));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/files/{uploadedFile.Id}",
            new FileUploadResponse(uploadedFile.Id, uploadedFile.OriginalFileName, uploadedFile.ContentType, uploadedFile.SizeBytes, uploadedFile.Sha256Hash, uploadedFile.UploadedAtUtc));
    }

    private static async Task<IResult> DownloadOwnFileAsync(
        Guid fileId,
        ClaimsPrincipal principal,
        AppDbContext db,
        IFileStorageService storage,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        var file = await db.UploadedFiles.SingleOrDefaultAsync(f => f.Id == fileId && f.OwnerUserId == userId, cancellationToken);
        if (file is null)
        {
            db.AuditEvents.Add(EndpointHelpers.CreateAudit(http, userId, "DownloadDenied", $"Unauthorized or missing file: {fileId}."));
            await db.SaveChangesAsync(cancellationToken);
            return Results.NotFound(new ApiError(ErrorMessages.FileNotFound));
        }

        try
        {
            var stream = await storage.OpenReadAsync(file.StoredFileName, cancellationToken);
            db.AuditEvents.Add(EndpointHelpers.CreateAudit(http, userId, "FileDownloaded", $"Downloaded file {file.Id}."));
            await db.SaveChangesAsync(cancellationToken);
            return Results.File(stream, file.ContentType, file.OriginalFileName);
        }
        catch (FileNotFoundException)
        {
            return Results.NotFound(new ApiError(ErrorMessages.StoredFileNotFound));
        }
    }

    private static async Task<IResult> DeleteOwnFileAsync(
        Guid fileId,
        ClaimsPrincipal principal,
        AppDbContext db,
        IFileStorageService storage,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        var file = await db.UploadedFiles
            .Include(f => f.ShareLinks)
            .SingleOrDefaultAsync(f => f.Id == fileId && f.OwnerUserId == userId, cancellationToken);

        if (file is null)
        {
            db.AuditEvents.Add(EndpointHelpers.CreateAudit(http, userId, "DeleteDenied", $"Unauthorized or missing file: {fileId}."));
            await db.SaveChangesAsync(cancellationToken);
            return Results.NotFound(new ApiError(ErrorMessages.FileNotFound));
        }

        await storage.DeleteIfExistsAsync(file.StoredFileName, cancellationToken);
        db.ShareLinks.RemoveRange(file.ShareLinks);
        db.UploadedFiles.Remove(file);
        db.AuditEvents.Add(EndpointHelpers.CreateAudit(http, userId, "FileDeleted", $"Deleted file {file.Id}."));
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> CreateShareLinkAsync(
        Guid fileId,
        ClaimsPrincipal principal,
        AppDbContext db,
        ITokenService tokenService,
        IOptions<UploadOptions> uploadOptions,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        var file = await db.UploadedFiles.SingleOrDefaultAsync(f => f.Id == fileId && f.OwnerUserId == userId, cancellationToken);
        if (file is null)
        {
            db.AuditEvents.Add(EndpointHelpers.CreateAudit(http, userId, "ShareDenied", $"Unauthorized or missing file: {fileId}."));
            await db.SaveChangesAsync(cancellationToken);
            return Results.NotFound(new ApiError(ErrorMessages.FileNotFound));
        }

        var rawToken = tokenService.CreateUrlSafeToken(32);
        var shareLink = new ShareLink
        {
            Id = Guid.NewGuid(),
            FileId = file.Id,
            CreatedByUserId = file.OwnerUserId,
            TokenHash = tokenService.HashToken(rawToken),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(uploadOptions.Value.ShareLinkLifetimeMinutes),
            MaxDownloads = uploadOptions.Value.ShareLinkMaxDownloads,
            DownloadCount = 0
        };

        db.ShareLinks.Add(shareLink);
        db.AuditEvents.Add(EndpointHelpers.CreateAudit(http, userId, "ShareCreated", $"Created share link {shareLink.Id} for file {file.Id}."));
        await db.SaveChangesAsync(cancellationToken);

        var shareUrl = $"{http.Request.Scheme}://{http.Request.Host}/share/{rawToken}";
        return Results.Created(
            $"/api/files/{fileId}/shares/{shareLink.Id}",
            new ShareLinkResponse(shareLink.Id, shareUrl, shareLink.ExpiresAtUtc, shareLink.MaxDownloads, shareLink.DownloadCount, false));
    }

    private static async Task<IResult> ListShareLinksAsync(
        Guid fileId,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        var ownsFile = await db.UploadedFiles.AnyAsync(f => f.Id == fileId && f.OwnerUserId == userId, cancellationToken);
        if (!ownsFile) return Results.NotFound(new ApiError(ErrorMessages.FileNotFound));

        var shares = await db.ShareLinks
            .Where(s => s.FileId == fileId && s.CreatedByUserId == userId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new ShareLinkListResponse(s.Id, s.CreatedAtUtc, s.ExpiresAtUtc, s.MaxDownloads, s.DownloadCount, s.RevokedAtUtc != null))
            .ToListAsync(cancellationToken);

        return Results.Ok(shares);
    }

    private static async Task<IResult> RevokeShareLinkAsync(
        Guid fileId,
        Guid shareId,
        ClaimsPrincipal principal,
        AppDbContext db,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        var share = await db.ShareLinks.SingleOrDefaultAsync(s => s.Id == shareId && s.FileId == fileId && s.CreatedByUserId == userId, cancellationToken);
        if (share is null) return Results.NotFound(new ApiError(ErrorMessages.ShareLinkNotFound));

        share.RevokedAtUtc = DateTime.UtcNow;
        db.AuditEvents.Add(EndpointHelpers.CreateAudit(http, userId, "ShareRevoked", $"Revoked share link {share.Id}."));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { message = "Share link revoked." });
    }

    private static async Task<string> CalculateSha256HashAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var hashStream = file.OpenReadStream();
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(hashStream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
