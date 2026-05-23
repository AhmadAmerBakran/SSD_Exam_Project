using SecureFileUploadPortal.Domain.Entities;

namespace SecureFileUploadPortal.Application.Abstractions;

public sealed record PasswordHashResult(
    string Hash,
    string Salt,
    string Algorithm,
    int MemorySizeKb,
    int Iterations,
    int DegreeOfParallelism,
    int HashLengthBytes);

public interface IPasswordHasher
{
    PasswordHashResult HashPassword(string password);
    bool VerifyPassword(string password, AppUser user);
    bool NeedsRehash(AppUser user);
}