using System.Security.Cryptography;
using SecureFileUploadPortal.Application.Abstractions;

namespace SecureFileUploadPortal.Infrastructure.Security;

public sealed class TokenService : ITokenService
{
    public string CreateUrlSafeToken(int numberOfBytes)
    {
        var bytes = RandomNumberGenerator.GetBytes(numberOfBytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public string HashToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public bool FixedTimeEquals(string leftHex, string rightHex)
    {
        if (leftHex.Length != rightHex.Length) return false;
        try
        {
            var left = Convert.FromHexString(leftHex);
            var right = Convert.FromHexString(rightHex);
            return CryptographicOperations.FixedTimeEquals(left, right);
        }
        catch
        {
            return false;
        }
    }
}