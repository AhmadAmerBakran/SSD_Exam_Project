namespace SecureFileUploadPortal.Application.Abstractions;

public interface IFileStorageService
{
    Task SaveAsync(Stream input, string storedFileName, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string storedFileName, CancellationToken cancellationToken = default);
    Task DeleteIfExistsAsync(string storedFileName, CancellationToken cancellationToken = default);
    string CreateStoredFileName(string originalFileName);
    string GetSafePathForDiagnosticsOnly(string storedFileName);
}