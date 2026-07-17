using System.Security.Cryptography;

namespace FleetOps.Api.Security;

public static class WebSessionSecurity
{
    public const string AuthenticationCookie = "fleetops-session";
    public const string CsrfCookie = "fleetops-csrf";
    public const string CsrfHeader = "X-CSRF-Token";

    public static string CreateCsrfToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static CookieOptions CreateCookieOptions(
        DateTimeOffset expiresAtUtc,
        bool secure,
        bool httpOnly) => new()
        {
            HttpOnly = httpOnly,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = expiresAtUtc,
            IsEssential = true,
        };
}
