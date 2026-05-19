using System.ComponentModel.DataAnnotations;

namespace SecureFileUploadPortal.Domain.Entities;

public sealed class AuthSession
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(32)]
    public string UserId { get; set; } = default!;
    public AppUser User { get; set; } = default!;

    [MaxLength(64)]
    public string CsrfTokenHash { get; set; } = default!;

    [MaxLength(300)]
    public string UserAgent { get; set; } = default!;

    [MaxLength(64)]
    public string IpAddress { get; set; } = default!;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    public bool IsActive(DateTime utcNow) => RevokedAtUtc is null && ExpiresAtUtc > utcNow;
}