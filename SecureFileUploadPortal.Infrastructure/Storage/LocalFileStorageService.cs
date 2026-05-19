using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SecureFileUploadPortal.Application.Abstractions;
using SecureFileUploadPortal.Application.Options;

namespace SecureFileUploadPortal.Infrastructure.Storage;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _storageRoot;

    public LocalFileStorageService(IHostEnvironment environment, IOptions<UploadOptions> options)
    {
        var configured = options.Value.StorageFolder;
        _storageRoot = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(environment.ContentRootPath, configured);
        Directory.CreateDirectory(_storageRoot);
    }

    public async Task SaveAsync(Stream input, string storedFileName, CancellationToken cancellationToken)
    {
        var path = SafeCombine(storedFileName);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await input.CopyToAsync(output, cancellationToken);
    }

    public Task<Stream> OpenReadAsync(string storedFileName, CancellationToken cancellationToken)
    {
        var path = SafeCombine(storedFileName);
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteIfExistsAsync(string storedFileName, CancellationToken cancellationToken)
    {
        var path = SafeCombine(storedFileName);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public string CreateStoredFileName(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        return $"{Guid.NewGuid():N}{extension}";
    }

    public string GetSafePathForDiagnosticsOnly(string storedFileName) => SafeCombine(storedFileName);

    private string SafeCombine(string storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName))
            throw new InvalidOperationException("Stored file name is missing.");

        var fileNameOnly = Path.GetFileName(storedFileName);
        if (!string.Equals(fileNameOnly, storedFileName, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid stored file name.");

        var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, fileNameOnly));
        var root = Path.GetFullPath(_storageRoot);
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Path traversal attempt blocked.");

        return fullPath;
    }
}