using System.Security.Cryptography;

namespace FleetOps.Api.Security;

public static class WebSessionSecurity
{
    public const string AuthenticationCookie = "fleetops-session";
    public const string CsrfCookie = "fleetops-csrf";
    public const string CsrfHeader = "X-CSRF-Token";

    public static string CreateCsrfToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static string SetSessionCookies(HttpContext context, IssuedToken token)
    {
        var secure = context.Request.IsHttps;
        context.Response.Cookies.Append(
            AuthenticationCookie,
            token.AccessToken,
            CreateCookieOptions(token.ExpiresAtUtc, secure, httpOnly: true));
        var csrfToken = CreateCsrfToken();
        context.Response.Cookies.Append(
            CsrfCookie,
            csrfToken,
            CreateCookieOptions(token.ExpiresAtUtc, secure, httpOnly: true));
        return csrfToken;
    }

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
