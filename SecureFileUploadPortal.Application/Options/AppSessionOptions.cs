namespace SecureFileUploadPortal.Application.Options;

public sealed class AppSessionOptions
{
    public int SessionLifetimeMinutes { get; set; } = 60;
}
