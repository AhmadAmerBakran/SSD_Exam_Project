using System.Net.Mail;
using System.Text.RegularExpressions;

namespace SecureFileUploadPortal.Application.Validation;

public static partial class InputValidator
{
    public static IReadOnlyList<string> ValidateRegistration(string fullName, string email, string password, string confirmPassword)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length < 2 || fullName.Trim().Length > 120)
            errors.Add("Full name must be between 2 and 120 characters.");

        if (!IsValidEmail(email))
            errors.Add("Email address is not valid.");

        errors.AddRange(ValidatePassword(password, email));

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
            errors.Add("Password and confirmation password do not match.");

        return errors;
    }

    public static IReadOnlyList<string> ValidateLogin(string email, string password)
    {
        var errors = new List<string>();
        if (!IsValidEmail(email)) errors.Add("Email address is not valid.");
        if (string.IsNullOrWhiteSpace(password)) errors.Add("Password is required.");
        return errors;
    }

    public static IReadOnlyList<string> ValidatePassword(string password, string? email = null)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("Password is required.");
            return errors;
        }

        if (password.Length < 12) errors.Add("Password must be at least 12 characters.");
        if (password.Length > 128) errors.Add("Password must not exceed 128 characters.");
        if (!password.Any(char.IsUpper)) errors.Add("Password must contain at least one uppercase letter.");
        if (!password.Any(char.IsLower)) errors.Add("Password must contain at least one lowercase letter.");
        if (!password.Any(char.IsDigit)) errors.Add("Password must contain at least one number.");
        if (!password.Any(ch => !char.IsLetterOrDigit(ch))) errors.Add("Password must contain at least one special character.");

        if (!string.IsNullOrWhiteSpace(email))
        {
            var localPart = email.Split('@')[0];
            if (localPart.Length >= 4 && password.Contains(localPart, StringComparison.OrdinalIgnoreCase))
                errors.Add("Password must not contain the email username.");
        }

        return errors;
    }

    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254) return false;
        try
        {
            var address = new MailAddress(email.Trim());
            return string.Equals(address.Address, email.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    public static IReadOnlyList<string> ValidateDisplayFileName(string originalFileName)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            errors.Add("File name is required.");
            return errors;
        }

        if (originalFileName.Length > 150) errors.Add("File name must not exceed 150 characters.");
        if (originalFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) errors.Add("File name contains invalid characters.");
        if (DangerousFileNameCharactersRegex().IsMatch(originalFileName)) errors.Add("File name contains unsafe HTML characters.");
        return errors;
    }

    [GeneratedRegex("[<>\\\"']")]
    private static partial Regex DangerousFileNameCharactersRegex();
}
