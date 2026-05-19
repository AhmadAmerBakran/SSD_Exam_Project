using System.ComponentModel.DataAnnotations;

namespace SecureFileUploadPortal.Domain.Entities;

public sealed class ShareLink
{
    [Key]
    public Guid Id { get; set; }

    public Guid FileId { get; set; }
    public UploadedFile UploadedFile { get; set; } = default!;

    [MaxLength(32)]
    public string CreatedByUserId { get; set; } = default!;
    public AppUser CreatedByUser { get; set; } = default!;

    [MaxLength(64)]
    public string TokenHash { get; set; } = default!;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public int DownloadCount { get; set; }
    public int MaxDownloads { get; set; }

    public bool CanBeUsed(DateTime utcNow) => RevokedAtUtc is null && ExpiresAtUtc > utcNow && DownloadCount < MaxDownloads;
}