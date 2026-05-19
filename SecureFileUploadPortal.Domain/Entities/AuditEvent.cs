using System.ComponentModel.DataAnnotations;

namespace SecureFileUploadPortal.Domain.Entities;

public sealed class AuditEvent
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(32)]
    public string? UserId { get; set; }

    [MaxLength(80)]
    public string Action { get; set; } = default!;

    [MaxLength(600)]
    public string Details { get; set; } = default!;

    [MaxLength(64)]
    public string IpAddress { get; set; } = default!;

    [MaxLength(300)]
    public string UserAgent { get; set; } = default!;

    public DateTime CreatedAtUtc { get; set; }
}