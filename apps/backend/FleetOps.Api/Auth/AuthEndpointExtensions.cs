using System.IdentityModel.Tokens.Jwt;
using FleetOps.Api.Auditing;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Identity;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FleetOps.Api.Auth;

public static class AuthEndpointExtensions
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        MapVersionedEndpoints(app.MapGroup("/api/v1/auth"));

        var legacy = app.MapGroup("/api/auth")
            .AddEndpointFilter(async (context, next) =>
            {
                context.HttpContext.Response.Headers.Append("Deprecation", "true");
                context.HttpContext.Response.Headers.Append("Link", "</api/v1/auth>; rel=successor-version");
                return await next(context);
            });
        legacy.MapPost("/login", LoginAsync).RequireRateLimiting("auth-login");
        legacy.MapGet("/me", GetCurrentUserAsync).RequireAuthorization();

        return app;
    }

    private static void MapVersionedEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/login", LoginAsync).RequireRateLimiting("auth-login");
        group.MapPost("/web/login", WebLoginAsync).RequireRateLimiting("auth-login");
        group.MapGet("/me", GetCurrentUserAsync).RequireAuthorization();
        group.MapGet("/csrf", GetCsrfToken).RequireAuthorization();
        group.MapGet("/sessions", ListSessionsAsync).RequireAuthorization();
        group.MapPost("/sessions/rotate", RotateSessionAsync).RequireAuthorization();
        group.MapPost("/logout", LogoutAsync).RequireAuthorization();
        group.MapPost("/logout-all", LogoutAllAsync).RequireAuthorization();
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        FleetOpsDbContext dbContext,
        IJwtTokenIssuer tokenIssuer,
        IAuditService auditService,
        IOptions<JwtOptions> jwtOptions,
        CancellationToken cancellationToken)
    {
        var result = await AuthenticateAsync(
            request,
            "android",
            userManager,
            signInManager,
            dbContext,
            tokenIssuer,
            auditService,
            jwtOptions.Value,
            cancellationToken);

        if (result.Problem is not null)
        {
            return result.Problem;
        }

        if (result.ChallengeMessage is not null)
        {
            return Results.Ok(new LoginResponse(
                string.Empty,
                DateTimeOffset.MinValue,
                EmptyUser(request.Email),
                true,
                "authenticator",
                result.ChallengeMessage));
        }

        return Results.Ok(new LoginResponse(
            result.Token!.AccessToken,
            result.Token.ExpiresAtUtc,
            result.User!,
            false,
            null,
            null));
    }

    private static async Task<IResult> WebLoginAsync(
        LoginRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        FleetOpsDbContext dbContext,
        IJwtTokenIssuer tokenIssuer,
        IAuditService auditService,
        IOptions<JwtOptions> jwtOptions,
        CancellationToken cancellationToken)
    {
        var result = await AuthenticateAsync(
            request,
            "web",
            userManager,
            signInManager,
            dbContext,
            tokenIssuer,
            auditService,
            jwtOptions.Value,
            cancellationToken);

        if (result.Problem is not null)
        {
            return result.Problem;
        }

        if (result.ChallengeMessage is not null)
        {
            return Results.Ok(new WebLoginResponse(
                DateTimeOffset.MinValue,
                EmptyUser(request.Email),
                string.Empty,
                true,
                "authenticator",
                result.ChallengeMessage));
        }

        var csrfToken = SetWebSessionCookies(httpContext, result.Token!);
        return Results.Ok(new WebLoginResponse(
            result.Token!.ExpiresAtUtc,
            result.User!,
            csrfToken,
            false,
            null,
            null));
    }

    private static async Task<AuthenticationResult> AuthenticateAsync(
        LoginRequest request,
        string clientType,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        FleetOpsDbContext dbContext,
        IJwtTokenIssuer tokenIssuer,
        IAuditService auditService,
        JwtOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return AuthenticationResult.Failed(Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["credentials"] = ["Email and password are required."]
            }));
        }

        var normalizedEmail = request.Email.Trim();
        var user = await userManager.Users.SingleOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return AuthenticationResult.Failed(Results.Unauthorized());
        }

        var passwordResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!passwordResult.Succeeded)
        {
            return AuthenticationResult.Failed(Results.Unauthorized());
        }

        var roles = await userManager.GetRolesAsync(user);
        var challenge = await GetTwoFactorChallengeMessageAsync(user, roles, request.TwoFactorCode, userManager);
        if (challenge is not null)
        {
            return AuthenticationResult.Challenged(challenge);
        }

        var organization = await dbContext.Organizations.SingleAsync(x => x.Id == user.OrganizationId, cancellationToken);
        var session = new UserSession(
            user.OrganizationId,
            user.Id,
            clientType,
            DateTimeOffset.UtcNow.AddHours(Math.Max(1, options.SessionLifetimeHours)));
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        var token = await tokenIssuer.IssueAsync(user, organization.Name, session.Id, cancellationToken);
        await auditService.WriteAsync(
            user.OrganizationId,
            user.Id,
            "auth.login",
            "session",
            session.Id.ToString(),
            new { user.Email, organization = organization.Slug, clientType, mfa = user.TwoFactorEnabled },
            cancellationToken);

        return AuthenticationResult.Succeeded(
            token,
            new AuthenticatedUserResponse(
                user.Id,
                user.Email ?? string.Empty,
                user.FullName,
                organization.Name,
                user.DriverId,
                roles.OrderBy(x => x).ToArray(),
                user.TwoFactorEnabled));
    }

    [Authorize]
    private static async Task<IResult> GetCurrentUserAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var user = await userManager.FindByIdAsync(tenant.UserId.ToString());
        if (user is null || !user.IsActive)
        {
            return Results.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        return Results.Ok(new AuthenticatedUserResponse(
            user.Id,
            user.Email ?? tenant.Email,
            user.FullName,
            tenant.OrganizationName,
            user.DriverId,
            roles.OrderBy(x => x).ToArray(),
            user.TwoFactorEnabled));
    }

    private static IResult GetCsrfToken(HttpContext httpContext)
    {
        var token = httpContext.Request.Cookies[WebSessionSecurity.CsrfCookie];
        if (string.IsNullOrWhiteSpace(token))
        {
            token = WebSessionSecurity.CreateCsrfToken();
            httpContext.Response.Cookies.Append(
                WebSessionSecurity.CsrfCookie,
                token,
                WebSessionSecurity.CreateCookieOptions(DateTimeOffset.UtcNow.AddHours(1), httpContext.Request.IsHttps, httpOnly: true));
        }

        return Results.Ok(new CsrfTokenResponse(token));
    }

    private static async Task<IResult> ListSessionsAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor tenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = tenantAccessor.GetRequiredTenant(httpContext.User);
        var currentSessionId = GetSessionId(httpContext.User);
        var now = DateTimeOffset.UtcNow;
        var sessions = await dbContext.UserSessions
            .Where(x => x.OrganizationId == tenant.OrganizationId && x.UserId == tenant.UserId && x.RevokedAtUtc == null && x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new UserSessionResponse(x.Id, x.ClientType, x.CreatedAtUtc, x.ExpiresAtUtc, x.Id == currentSessionId))
            .ToListAsync(cancellationToken);
        return Results.Ok(sessions);
    }

    private static async Task<IResult> RotateSessionAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ICurrentTenantAccessor tenantAccessor,
        IJwtTokenIssuer tokenIssuer,
        IAuditService auditService,
        IOptions<JwtOptions> options,
        CancellationToken cancellationToken)
    {
        var tenant = tenantAccessor.GetRequiredTenant(httpContext.User);
        var currentSession = await FindCurrentSessionAsync(httpContext, dbContext, cancellationToken);
        if (currentSession is null)
        {
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(tenant.UserId.ToString());
        if (user is null || !user.IsActive)
        {
            return Results.Unauthorized();
        }

        var now = DateTimeOffset.UtcNow;
        currentSession.Revoke(tenant.UserId, "rotated", now);
        var replacement = new UserSession(
            tenant.OrganizationId,
            tenant.UserId,
            currentSession.ClientType,
            now.AddHours(Math.Max(1, options.Value.SessionLifetimeHours)));
        dbContext.UserSessions.Add(replacement);
        await dbContext.SaveChangesAsync(cancellationToken);

        var token = await tokenIssuer.IssueAsync(user, tenant.OrganizationName, replacement.Id, cancellationToken);
        var csrfToken = SetWebSessionCookies(httpContext, token);
        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "auth.session_rotated",
            "session",
            replacement.Id.ToString(),
            new { previousSessionId = currentSession.Id, currentSession.ClientType },
            cancellationToken);
        return Results.Ok(new { token.ExpiresAtUtc, CsrfToken = csrfToken });
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor tenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = tenantAccessor.GetRequiredTenant(httpContext.User);
        var session = await FindCurrentSessionAsync(httpContext, dbContext, cancellationToken);
        if (session is not null)
        {
            session.Revoke(tenant.UserId, "logout", DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditService.WriteAsync(
                tenant.OrganizationId,
                tenant.UserId,
                "auth.logout",
                "session",
                session.Id.ToString(),
                new { session.ClientType },
                cancellationToken);
        }

        DeleteWebSessionCookies(httpContext);
        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAllAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor tenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = tenantAccessor.GetRequiredTenant(httpContext.User);
        var now = DateTimeOffset.UtcNow;
        var sessions = await dbContext.UserSessions
            .Where(x => x.OrganizationId == tenant.OrganizationId && x.UserId == tenant.UserId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.Revoke(tenant.UserId, "global_logout", now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "auth.global_logout",
            "user",
            tenant.UserId.ToString(),
            new { revokedSessionCount = sessions.Count },
            cancellationToken);
        DeleteWebSessionCookies(httpContext);
        return Results.NoContent();
    }

    private static Task<UserSession?> FindCurrentSessionAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var sessionId = GetSessionId(httpContext.User);
        return sessionId is null
            ? Task.FromResult<UserSession?>(null)
            : dbContext.UserSessions.FirstOrDefaultAsync(x => x.Id == sessionId.Value, cancellationToken);
    }

    private static Guid? GetSessionId(System.Security.Claims.ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst(JwtRegisteredClaimNames.Sid)?.Value, out var sessionId) ? sessionId : null;

    private static string SetWebSessionCookies(HttpContext httpContext, IssuedToken token)
    {
        var secure = httpContext.Request.IsHttps;
        httpContext.Response.Cookies.Append(
            WebSessionSecurity.AuthenticationCookie,
            token.AccessToken,
            WebSessionSecurity.CreateCookieOptions(token.ExpiresAtUtc, secure, httpOnly: true));
        var csrfToken = WebSessionSecurity.CreateCsrfToken();
        httpContext.Response.Cookies.Append(
            WebSessionSecurity.CsrfCookie,
            csrfToken,
            WebSessionSecurity.CreateCookieOptions(token.ExpiresAtUtc, secure, httpOnly: true));
        return csrfToken;
    }

    private static void DeleteWebSessionCookies(HttpContext httpContext)
    {
        var options = WebSessionSecurity.CreateCookieOptions(DateTimeOffset.UnixEpoch, httpContext.Request.IsHttps, httpOnly: true);
        httpContext.Response.Cookies.Delete(WebSessionSecurity.AuthenticationCookie, options);
        httpContext.Response.Cookies.Delete(WebSessionSecurity.CsrfCookie, options);
    }

    private static AuthenticatedUserResponse EmptyUser(string email) =>
        new(Guid.Empty, email.Trim(), string.Empty, string.Empty, null, [], true);

    private static async Task<string?> GetTwoFactorChallengeMessageAsync(
        ApplicationUser user,
        IEnumerable<string> roles,
        string? code,
        UserManager<ApplicationUser> userManager)
    {
        var requiresAdminTwoFactor = user.TwoFactorEnabled && roles.Contains(SystemRoles.Admin, StringComparer.Ordinal);
        if (!requiresAdminTwoFactor)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return "Enter the 6-digit code from your authenticator app.";
        }

        var isValidCode = await userManager.VerifyTwoFactorTokenAsync(
            user,
            userManager.Options.Tokens.AuthenticatorTokenProvider,
            NormalizeCode(code));
        return isValidCode ? null : "Invalid one-time code. Try again.";
    }

    private static string NormalizeCode(string code) => new(code.Where(char.IsAsciiDigit).ToArray());

    private sealed record AuthenticationResult(
        IResult? Problem,
        string? ChallengeMessage,
        IssuedToken? Token,
        AuthenticatedUserResponse? User)
    {
        public static AuthenticationResult Failed(IResult problem) => new(problem, null, null, null);
        public static AuthenticationResult Challenged(string message) => new(null, message, null, null);
        public static AuthenticationResult Succeeded(IssuedToken token, AuthenticatedUserResponse user) => new(null, null, token, user);
    }
}
