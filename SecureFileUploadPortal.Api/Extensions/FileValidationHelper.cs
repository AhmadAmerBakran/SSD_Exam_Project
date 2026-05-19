using System.Text;

namespace SecureFileUploadPortal.Api.Extensions;

public static class FileValidationHelper
{
    private static readonly Dictionary<string, string[]> ExtensionMimeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ["application/pdf"],
        [".png"] = ["image/png"],
        [".jpg"] = ["image/jpeg", "image/jpg"],
        [".jpeg"] = ["image/jpeg", "image/jpg"],
        [".txt"] = ["text/plain", "application/octet-stream"]
    };

    public static bool IsAllowedExtension(string extension, IReadOnlyCollection<string> allowedExtensions)
    {
        return allowedExtensions.Any(e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsAllowedMimeType(string extension, string contentType)
    {
        if (!ExtensionMimeMap.TryGetValue(extension, out var allowedMimeTypes)) return false;
        return allowedMimeTypes.Any(m => string.Equals(m, contentType, StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<bool> HasValidSignatureAsync(IFormFile file, string extension, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var buffer = new byte[Math.Min(512, (int)Math.Max(file.Length, 1))];
        var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
        var header = buffer.Take(read).ToArray();

        return extension.ToLowerInvariant() switch
        {
            ".pdf" => StartsWith(header, [0x25, 0x50, 0x44, 0x46]),
            ".png" => StartsWith(header, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            ".jpg" or ".jpeg" => StartsWith(header, [0xFF, 0xD8, 0xFF]),
            ".txt" => IsLikelyText(header),
            _ => false
        };
    }

    private static bool StartsWith(byte[] source, byte[] prefix)
    {
        if (source.Length < prefix.Length) return false;
        for (var i = 0; i < prefix.Length; i++)
        {
            if (source[i] != prefix[i]) return false;
        }
        return true;
    }

    private static bool IsLikelyText(byte[] bytes)
    {
        if (bytes.Length == 0) return true;
        if (bytes.Any(b => b == 0)) return false;
        try
        {
            _ = new UTF8Encoding(false, true).GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
