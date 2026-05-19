namespace SecureFileUploadPortal.Application.Options;

public sealed class UploadOptions
{
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;
    public int ShareLinkLifetimeMinutes { get; set; } = 10;
    public int ShareLinkMaxDownloads { get; set; } = 3;
    public string StorageFolder { get; set; } = "App_Data/uploads";
    public string[] AllowedExtensions { get; set; } = [".pdf", ".png", ".jpg", ".jpeg", ".txt"];
}