using FleetOps.Api.Demo;

namespace FleetOps.Api.Security;

public sealed class DemoSessionGuardMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS" };

    public async Task InvokeAsync(HttpContext context, IHostEnvironment environment, IConfiguration configuration)
    {
        if ((environment.IsEnvironment("Demo") || environment.IsEnvironment("DemoTesting"))
            && context.Request.Path.StartsWithSegments("/api/internal"))
        {
            if (InternalApiKey.Matches(context, configuration))
            {
                await next(context);
                return;
            }

            if (context.User.Identity?.IsAuthenticated != true)
            {
                await Results.Unauthorized().ExecuteAsync(context);
                return;
            }
        }

        if (!string.Equals(context.User.FindFirst(TenantClaimTypes.DemoSession)?.Value, "true", StringComparison.Ordinal))
        {
            await next(context);
            return;
        }

        var path = context.Request.Path;
        var allowedMutation = path.StartsWithSegments("/api/v1/demo/session/control")
            || path.StartsWithSegments("/api/v1/demo/public/launch")
            || path.StartsWithSegments("/api/v1/auth/logout");
        var sensitiveRead = path.StartsWithSegments("/api/admin")
            || path.StartsWithSegments("/api/v1/admin")
            || path.StartsWithSegments("/api/v1/auth/sessions")
            || path.StartsWithSegments("/api/v1/integrations")
            || path.StartsWithSegments("/api/internal");

        if ((!SafeMethods.Contains(context.Request.Method) && !allowedMutation) || sensitiveRead)
        {
            await Results.Problem(
                title: "Demo capability denied",
                detail: "Public Demo sessions are read-only outside the bounded scenario controls.",
                statusCode: StatusCodes.Status403Forbidden).ExecuteAsync(context);
            return;
        }

        await next(context);
    }
}
