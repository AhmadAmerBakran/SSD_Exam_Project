namespace SecureFileUploadPortal.Application.DTOs;

public sealed record ApiError(string Message, IReadOnlyCollection<string>? Errors = null);
public sealed record AuthUserResponse(string Id, string FullName, string Email);
public sealed record AuthResponse(AuthUserResponse User, string CsrfToken);
public sealed record FileUploadResponse(Guid Id, string OriginalFileName, string ContentType, long SizeBytes, string Sha256Hash, DateTime UploadedAtUtc);
public sealed record FileListItemResponse(Guid Id, string OriginalFileName, string ContentType, long SizeBytes, string Sha256Hash, DateTime UploadedAtUtc);
public sealed record ShareLinkResponse(Guid Id, string Url, DateTime ExpiresAtUtc, int MaxDownloads, int DownloadCount, bool Revoked);
public sealed record ShareLinkListResponse(Guid Id, DateTime CreatedAtUtc, DateTime ExpiresAtUtc, int MaxDownloads, int DownloadCount, bool Revoked);
