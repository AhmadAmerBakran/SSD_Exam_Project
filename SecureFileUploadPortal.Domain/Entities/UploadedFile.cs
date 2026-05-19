using System.ComponentModel.DataAnnotations;

namespace SecureFileUploadPortal.Domain.Entities;

public sealed class UploadedFile
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(32)]
    public string OwnerUserId { get; set; } = default!;
    public AppUser Owner { get; set; } = default!;

    [MaxLength(150)]
    public string OriginalFileName { get; set; } = default!;

    [MaxLength(100)]
    public string StoredFileName { get; set; } = default!;

    [MaxLength(100)]
    public string ContentType { get; set; } = default!;

    public long SizeBytes { get; set; }

    [MaxLength(64)]
    public string Sha256Hash { get; set; } = default!;

    public DateTime UploadedAtUtc { get; set; }

    public ICollection<ShareLink> ShareLinks { get; set; } = new List<ShareLink>();
}