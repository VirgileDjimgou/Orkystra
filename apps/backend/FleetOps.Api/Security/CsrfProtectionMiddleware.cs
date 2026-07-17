using System.Security.Cryptography;
using System.Text;

namespace FleetOps.Api.Security;

public sealed class CsrfProtectionMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS", "TRACE" };

    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresValidation(context.Request) && !HasValidToken(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Results.Problem(
                    title: "CSRF validation failed",
                    detail: "The anti-forgery token is missing or invalid.",
                    statusCode: StatusCodes.Status400BadRequest)
                .ExecuteAsync(context);
            return;
        }

        await next(context);
    }

    private static bool RequiresValidation(HttpRequest request) =>
        !SafeMethods.Contains(request.Method)
        && request.Cookies.ContainsKey(WebSessionSecurity.AuthenticationCookie)
        && !request.Path.StartsWithSegments("/api/v1/auth/web/login")
        && !request.Path.StartsWithSegments("/hubs/tracking");

    private static bool HasValidToken(HttpRequest request)
    {
        var cookieToken = request.Cookies[WebSessionSecurity.CsrfCookie];
        var headerToken = request.Headers[WebSessionSecurity.CsrfHeader].ToString();
        if (string.IsNullOrWhiteSpace(cookieToken) || string.IsNullOrWhiteSpace(headerToken))
        {
            return false;
        }

        var cookieBytes = Encoding.UTF8.GetBytes(cookieToken);
        var headerBytes = Encoding.UTF8.GetBytes(headerToken);
        return cookieBytes.Length == headerBytes.Length
            && CryptographicOperations.FixedTimeEquals(cookieBytes, headerBytes);
    }
}
