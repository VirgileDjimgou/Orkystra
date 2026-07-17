using System.Text;
using FleetOps.Api.Auditing;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Identity;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.Admin;

public static class SecurityAdministrationEndpointExtensions
{
    private const string AuthenticatorIssuer = "Orkystra FleetOps";

    public static IEndpointRouteBuilder MapSecurityAdministrationEndpoints(this IEndpointRouteBuilder app)
    {
        MapSecurityGroup(app.MapGroup("/api/v1/admin/security"));
        var legacy = app.MapGroup("/api/admin/security")
            .AddEndpointFilter(async (context, next) =>
            {
                context.HttpContext.Response.Headers.Append("Deprecation", "true");
                context.HttpContext.Response.Headers.Append("Link", "</api/v1/admin/security>; rel=successor-version");
                return await next(context);
            });
        MapSecurityGroup(legacy);

        return app;
    }

    private static void MapSecurityGroup(RouteGroupBuilder group)
    {
        group
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        group.MapGet("/mfa", GetMfaStatusAsync);
        group.MapPost("/mfa/setup", CreateMfaSetupAsync);
        group.MapPost("/mfa/verify", VerifyMfaSetupAsync);
        group.MapPost("/mfa/disable", DisableMfaAsync);
        group.MapPost("/users/{userId:guid}/sessions/revoke", RevokeUserSessionsAsync);
    }

    private static async Task<IResult> RevokeUserSessionsAsync(
        Guid userId,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var targetExists = await dbContext.Users.AnyAsync(
            x => x.Id == userId && x.OrganizationId == tenant.OrganizationId,
            cancellationToken);
        if (!targetExists)
        {
            return Results.NotFound();
        }

        var sessions = await dbContext.UserSessions
            .Where(x => x.OrganizationId == tenant.OrganizationId && x.UserId == userId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        foreach (var session in sessions)
        {
            session.Revoke(tenant.UserId, "administrator_revocation", now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "admin.security.sessions_revoked",
            "user",
            userId.ToString(),
            new { revokedSessionCount = sessions.Count },
            cancellationToken);
        return Results.Ok(new { RevokedSessionCount = sessions.Count, EffectiveAtUtc = now });
    }

    private static async Task<IResult> GetMfaStatusAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(httpContext, userManager, currentTenantAccessor, cancellationToken);
        var sharedKey = await userManager.GetAuthenticatorKeyAsync(user);
        return Results.Ok(new MfaStatusResponse(
            user.TwoFactorEnabled,
            !string.IsNullOrWhiteSpace(sharedKey),
            user.Email ?? string.Empty));
    }

    private static async Task<IResult> CreateMfaSetupAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var user = await GetCurrentUserAsync(httpContext, userManager, currentTenantAccessor, cancellationToken);

        await ApplyIdentityResultAsync(userManager.SetTwoFactorEnabledAsync(user, false));
        await ApplyIdentityResultAsync(userManager.ResetAuthenticatorKeyAsync(user));
        await ApplyIdentityResultAsync(userManager.UpdateSecurityStampAsync(user));

        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key))
        {
            return Results.Problem("Unable to generate an authenticator key for the current administrator.");
        }

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "admin.security.mfa_setup_generated",
            "user",
            user.Id.ToString(),
            new { user.Email },
            cancellationToken);

        return Results.Ok(new MfaSetupResponse(
            user.TwoFactorEnabled,
            FormatKey(key),
            key,
            BuildAuthenticatorUri(user.Email ?? tenant.Email, key)));
    }

    private static async Task<IResult> VerifyMfaSetupAsync(
        VerifyMfaRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["code"] = ["A 6-digit authenticator code is required."]
            });
        }

        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var user = await GetCurrentUserAsync(httpContext, userManager, currentTenantAccessor, cancellationToken);
        var sharedKey = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(sharedKey))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["mfa"] = ["Generate a setup secret before enabling MFA."]
            });
        }

        var isValidCode = await userManager.VerifyTwoFactorTokenAsync(
            user,
            userManager.Options.Tokens.AuthenticatorTokenProvider,
            NormalizeCode(request.Code));
        if (!isValidCode)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["code"] = ["The authenticator code is invalid or expired."]
            });
        }

        await ApplyIdentityResultAsync(userManager.SetTwoFactorEnabledAsync(user, true));
        await ApplyIdentityResultAsync(userManager.UpdateSecurityStampAsync(user));
        var recoveryCodes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 8) ?? []).ToArray();

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "admin.security.mfa_enabled",
            "user",
            user.Id.ToString(),
            new { user.Email, recoveryCodeCount = recoveryCodes.Length },
            cancellationToken);

        return Results.Ok(new VerifyMfaResponse(true, recoveryCodes));
    }

    private static async Task<IResult> DisableMfaAsync(
        DisableMfaRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["code"] = ["A 6-digit authenticator code is required to disable MFA."]
            });
        }

        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var user = await GetCurrentUserAsync(httpContext, userManager, currentTenantAccessor, cancellationToken);
        if (!user.TwoFactorEnabled)
        {
            return Results.Ok(new MfaStatusResponse(false, false, user.Email ?? string.Empty));
        }

        var isValidCode = await userManager.VerifyTwoFactorTokenAsync(
            user,
            userManager.Options.Tokens.AuthenticatorTokenProvider,
            NormalizeCode(request.Code));
        if (!isValidCode)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["code"] = ["The authenticator code is invalid or expired."]
            });
        }

        await ApplyIdentityResultAsync(userManager.SetTwoFactorEnabledAsync(user, false));
        await ApplyIdentityResultAsync(userManager.ResetAuthenticatorKeyAsync(user));
        await ApplyIdentityResultAsync(userManager.UpdateSecurityStampAsync(user));

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "admin.security.mfa_disabled",
            "user",
            user.Id.ToString(),
            new { user.Email },
            cancellationToken);

        return Results.Ok(new MfaStatusResponse(false, false, user.Email ?? string.Empty));
    }

    private static async Task<ApplicationUser> GetCurrentUserAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var user = await userManager.Users.SingleOrDefaultAsync(
            x => x.Id == tenant.UserId,
            cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new InvalidOperationException("The current administrator account is unavailable.");
        }

        return user;
    }

    private static async Task ApplyIdentityResultAsync(Task<IdentityResult> operation)
    {
        var result = await operation;
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
        }
    }

    private static string NormalizeCode(string code)
    {
        var digits = code.Where(char.IsAsciiDigit).ToArray();
        return new string(digits);
    }

    private static string FormatKey(string unformattedKey)
    {
        var result = new StringBuilder();
        for (var index = 0; index < unformattedKey.Length; index++)
        {
            if (index > 0 && index % 4 == 0)
            {
                result.Append(' ');
            }

            result.Append(char.ToUpperInvariant(unformattedKey[index]));
        }

        return result.ToString();
    }

    private static string BuildAuthenticatorUri(string email, string key)
    {
        var issuer = Uri.EscapeDataString(AuthenticatorIssuer);
        var account = Uri.EscapeDataString($"{AuthenticatorIssuer}:{email}");
        return $"otpauth://totp/{account}?secret={key}&issuer={issuer}&digits=6";
    }
}
