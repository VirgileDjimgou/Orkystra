using System.IdentityModel.Tokens.Jwt;
using FleetOps.Api.Auditing;
using FleetOps.Api.Auth;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Identity;
using FleetOps.Core.Observability;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FleetOps.Api.Demo;

public sealed record DemoLaunchResponse(
    DateTimeOffset ExpiresAtUtc,
    AuthenticatedUserResponse User,
    string CsrfToken,
    string Label,
    DemoSessionState Scenario);

public sealed record DemoControlRequest(string Action, string? Scenario);

public static class PublicDemoEndpointExtensions
{
    public static IEndpointRouteBuilder MapPublicDemoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/demo");
        group.MapGet("/public/status", GetStatus);
        group.MapPost("/public/launch", LaunchAsync).RequireRateLimiting("demo-launch");
        group.MapGet("/session/control", GetControl).RequireAuthorization();
        group.MapPost("/session/control", ApplyControlAsync)
            .RequireAuthorization()
            .RequireRateLimiting("demo-control");
        return app;
    }

    private static IResult GetStatus(IHostEnvironment environment, IOptions<PublicDemoOptions> options) =>
        Results.Ok(new
        {
            Enabled = IsEnabled(environment, options.Value),
            Label = "SIMULATED DEMO",
        });

    private static async Task<IResult> LaunchAsync(
        HttpContext context,
        IHostEnvironment environment,
        IOptions<PublicDemoOptions> options,
        FleetOpsDbContext db,
        UserManager<ApplicationUser> userManager,
        IJwtTokenIssuer tokenIssuer,
        IDemoSessionStateStore states,
        IAuditService audit,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!IsEnabled(environment, settings))
        {
            RecordLaunch("disabled");
            return Results.NotFound();
        }

        var now = timeProvider.GetUtcNow();
        var expired = await db.UserSessions
            .Where(x => x.ClientType == "public-demo" && x.RevokedAtUtc == null && x.ExpiresAtUtc <= now)
            .OrderBy(x => x.ExpiresAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);
        foreach (var session in expired) session.Revoke(Guid.Empty, "expired", now);
        states.RemoveExpired(now, 100);

        var activeCount = await db.UserSessions.CountAsync(
            x => x.ClientType == "public-demo" && x.RevokedAtUtc == null && x.ExpiresAtUtc > now,
            cancellationToken);
        if (activeCount >= settings.MaxConcurrentSessions)
        {
            RecordLaunch("capacity");
            return Results.Problem(
                title: "Demo capacity reached",
                detail: "All public Demo slots are currently in use. Try again shortly.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var organization = await db.Organizations.SingleAsync(x => x.Slug == PublicDemoOptions.TenantSlug, cancellationToken);
        var user = await userManager.FindByEmailAsync(PublicDemoOptions.OperatorEmail);
        if (user is null || !user.IsActive || user.OrganizationId != organization.Id)
        {
            RecordLaunch("unavailable");
            return Results.Problem(title: "Demo unavailable", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var expiresAtUtc = now.AddSeconds(settings.SessionLifetimeSeconds);
        var sessionRecord = new UserSession(organization.Id, user.Id, "public-demo", expiresAtUtc);
        db.UserSessions.Add(sessionRecord);
        await db.SaveChangesAsync(cancellationToken);

        var token = await tokenIssuer.IssueAsync(
            user,
            organization.Name,
            sessionRecord.Id,
            cancellationToken,
            expiresAtUtc,
            isDemoSession: true);
        var csrfToken = WebSessionSecurity.SetSessionCookies(context, token);
        var state = states.GetOrCreate(sessionRecord.Id, expiresAtUtc);
        await audit.WriteAsync(
            organization.Id,
            user.Id,
            "demo.public_session_launched",
            "session",
            sessionRecord.Id.ToString(),
            new { expiresAtUtc, sandboxed = true },
            cancellationToken);
        RecordLaunch("launched");

        return Results.Ok(new DemoLaunchResponse(
            expiresAtUtc,
            new AuthenticatedUserResponse(
                user.Id,
                user.Email ?? PublicDemoOptions.OperatorEmail,
                user.FullName,
                organization.Name,
                null,
                [SystemRoles.Operator],
                false,
                true),
            csrfToken,
            "SIMULATED DEMO",
            state));
    }

    private static IResult GetControl(
        HttpContext context,
        IDemoSessionStateStore states,
        TimeProvider timeProvider)
    {
        if (!TryGetDemoSession(context, out var sessionId, out var expiresAtUtc)) return Results.Forbid();
        if (expiresAtUtc <= timeProvider.GetUtcNow()) return Results.Unauthorized();
        return Results.Ok(states.GetOrCreate(sessionId, expiresAtUtc));
    }

    private static async Task<IResult> ApplyControlAsync(
        DemoControlRequest request,
        HttpContext context,
        IDemoSessionStateStore states,
        ICurrentTenantAccessor tenantAccessor,
        IAuditService audit,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetDemoSession(context, out var sessionId, out var expiresAtUtc)) return Results.Forbid();
        if (expiresAtUtc <= timeProvider.GetUtcNow()) return Results.Unauthorized();
        try
        {
            var state = states.Apply(sessionId, expiresAtUtc, request.Action, request.Scenario);
            FleetOpsMetrics.PublicDemoControls.Add(
                1,
                new KeyValuePair<string, object?>("action", state.Status == "RUNNING" ? "START" : state.Status == "PAUSED" ? "PAUSE" : "RESET"));
            var tenant = tenantAccessor.GetRequiredTenant(context.User);
            await audit.WriteAsync(
                tenant.OrganizationId,
                tenant.UserId,
                "demo.session_controlled",
                "session",
                sessionId.ToString(),
                new { state.Scenario, state.Status, state.Revision },
                cancellationToken);
            return Results.Ok(state);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["control"] = [exception.Message] });
        }
    }

    private static bool TryGetDemoSession(HttpContext context, out Guid sessionId, out DateTimeOffset expiresAtUtc)
    {
        sessionId = Guid.Empty;
        expiresAtUtc = DateTimeOffset.MinValue;
        return string.Equals(context.User.FindFirst(TenantClaimTypes.DemoSession)?.Value, "true", StringComparison.Ordinal)
            && Guid.TryParse(context.User.FindFirst(JwtRegisteredClaimNames.Sid)?.Value, out sessionId)
            && long.TryParse(context.User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value, out var unixExpiry)
            && TrySetExpiry(unixExpiry, out expiresAtUtc);
    }

    private static bool TrySetExpiry(long unixExpiry, out DateTimeOffset expiresAtUtc)
    {
        expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(unixExpiry);
        return true;
    }

    private static bool IsEnabled(IHostEnvironment environment, PublicDemoOptions options) =>
        (environment.IsEnvironment("Demo") || environment.IsEnvironment("DemoTesting"))
        && options.Enabled
        && options.SideEffectsSandboxed;

    private static void RecordLaunch(string result) =>
        FleetOpsMetrics.PublicDemoSessions.Add(1, new KeyValuePair<string, object?>("result", result));
}
