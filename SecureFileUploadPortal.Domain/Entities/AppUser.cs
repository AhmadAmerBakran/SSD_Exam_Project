using System.ComponentModel.DataAnnotations;

namespace SecureFileUploadPortal.Domain.Entities;

public sealed class AppUser
{
    [Key]
    [MaxLength(32)]
    public string Id { get; set; } = default!;

    [MaxLength(120)]
    public string FullName { get; set; } = default!;

    [MaxLength(254)]
    public string Email { get; set; } = default!;

    [MaxLength(254)]
    public string NormalizedEmail { get; set; } = default!;

    public string PasswordHash { get; set; } = default!;
    public string PasswordSalt { get; set; } = default!;

    [MaxLength(40)]
    public string PasswordAlgorithm { get; set; } = default!;

    public int PasswordMemorySizeKb { get; set; }
    public int PasswordIterations { get; set; }
    public int PasswordDegreeOfParallelism { get; set; }
    public int PasswordHashLengthBytes { get; set; }

    [MaxLength(64)]
    public string SecurityStamp { get; set; } = default!;

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<UploadedFile> UploadedFiles { get; set; } = new List<UploadedFile>();
    public ICollection<AuthSession> Sessions { get; set; } = new List<AuthSession>();
}