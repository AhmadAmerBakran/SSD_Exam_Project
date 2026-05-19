using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;
using SecureFileUploadPortal.Application.Abstractions;
using SecureFileUploadPortal.Application.Options;
using SecureFileUploadPortal.Domain.Entities;

namespace SecureFileUploadPortal.Infrastructure.Security;

public sealed class Argon2idPasswordHasher(IOptions<PasswordHashingOptions> options) : IPasswordHasher
{
    public const string AlgorithmName = "argon2id";
    private readonly PasswordHashingOptions _options = options.Value;

    public PasswordHashResult HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(_options.SaltLengthBytes);
        var hashBytes = Hash(password, saltBytes, _options.MemorySizeKb, _options.Iterations, _options.DegreeOfParallelism, _options.HashLengthBytes);

        return new PasswordHashResult(
            Convert.ToBase64String(hashBytes),
            Convert.ToBase64String(saltBytes),
            AlgorithmName,
            _options.MemorySizeKb,
            _options.Iterations,
            _options.DegreeOfParallelism,
            _options.HashLengthBytes);
    }

    public bool VerifyPassword(string password, AppUser user)
    {
        if (!string.Equals(user.PasswordAlgorithm, AlgorithmName, StringComparison.OrdinalIgnoreCase))
            return false;

        var saltBytes = Convert.FromBase64String(user.PasswordSalt);
        var expectedHash = Convert.FromBase64String(user.PasswordHash);
        var actualHash = Hash(password, saltBytes, user.PasswordMemorySizeKb, user.PasswordIterations, user.PasswordDegreeOfParallelism, user.PasswordHashLengthBytes);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    public bool NeedsRehash(AppUser user)
    {
        return !string.Equals(user.PasswordAlgorithm, AlgorithmName, StringComparison.OrdinalIgnoreCase)
               || user.PasswordMemorySizeKb != _options.MemorySizeKb
               || user.PasswordIterations != _options.Iterations
               || user.PasswordDegreeOfParallelism != _options.DegreeOfParallelism
               || user.PasswordHashLengthBytes != _options.HashLengthBytes;
    }

    private static byte[] Hash(string password, byte[] salt, int memorySizeKb, int iterations, int degreeOfParallelism, int hashLengthBytes)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memorySizeKb,
            Iterations = iterations,
            DegreeOfParallelism = degreeOfParallelism
        };

        return argon2.GetBytes(hashLengthBytes);
    }
}