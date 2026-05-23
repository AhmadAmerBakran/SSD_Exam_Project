namespace SecureFileUploadPortal.Application.DTOs;

public sealed record RegisterRequest(string FullName, string Email, string Password, string ConfirmPassword);
public sealed record LoginRequest(string Email, string Password);
