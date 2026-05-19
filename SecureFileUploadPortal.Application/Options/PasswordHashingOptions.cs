namespace SecureFileUploadPortal.Application.Options;

public sealed class PasswordHashingOptions
{
    public int MemorySizeKb { get; set; } = 65536;
    public int Iterations { get; set; } = 3;
    public int DegreeOfParallelism { get; set; } = 2;
    public int SaltLengthBytes { get; set; } = 32;
    public int HashLengthBytes { get; set; } = 32;
}
