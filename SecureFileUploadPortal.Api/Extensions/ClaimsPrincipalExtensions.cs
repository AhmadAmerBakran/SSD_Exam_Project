using System.Security.Claims;

namespace SecureFileUploadPortal.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public const string SessionIdClaim = "sid";
    public const string SecurityStampClaim = "sstamp";

    public static string GetUserId(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? throw new UnauthorizedAccessException("Missing user id claim.");
    }

    public static Guid GetSessionId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(SessionIdClaim);
        return Guid.TryParse(value, out var sessionId)
            ? sessionId
            : throw new UnauthorizedAccessException("Missing session id claim.");
    }

    public static string GetSecurityStamp(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(SecurityStampClaim)
               ?? throw new UnauthorizedAccessException("Missing security stamp claim.");
    }
}
