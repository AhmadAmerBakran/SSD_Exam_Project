namespace SecureFileUploadPortal.Application.Abstractions;

public interface ITokenService
{
    string CreateUrlSafeToken(int numberOfBytes = 32);
    string HashToken(string token);
    bool FixedTimeEquals(string leftHex, string rightHex);
}
