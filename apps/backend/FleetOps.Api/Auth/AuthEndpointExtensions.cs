using FleetOps.Api.Auditing;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Identity;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.Auth;

public static class AuthEndpointExtensions
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/login", LoginAsync).RequireRateLimiting("auth-login");
        group.MapGet("/me", GetCurrentUserAsync).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        FleetOpsDbContext dbContext,
        IJwtTokenIssuer tokenIssuer,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["credentials"] = ["Email and password are required."]
            });
        }

        var normalizedEmail = request.Email.Trim();
        var user = await userManager.Users.SingleOrDefaultAsync(
            x => x.Email == normalizedEmail,
            cancellationToken);

        if (user is null || !user.IsActive)
        {
            return Results.Unauthorized();
        }

        var passwordResult = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);
        if (!passwordResult.Succeeded)
        {
            return Results.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        var twoFactorChallengeMessage = await GetTwoFactorChallengeMessageAsync(
            user,
            roles,
            request.TwoFactorCode,
            userManager);
        if (twoFactorChallengeMessage is not null)
        {
            return Results.Ok(new LoginResponse(
                string.Empty,
                DateTimeOffset.MinValue,
                new AuthenticatedUserResponse(
                    Guid.Empty,
                    normalizedEmail,
                    string.Empty,
                    string.Empty,
                    null,
                    Array.Empty<string>(),
                    true),
                true,
                "authenticator",
                twoFactorChallengeMessage));
        }

        var organization = await dbContext.Organizations.SingleAsync(
            x => x.Id == user.OrganizationId,
            cancellationToken);

        var token = await tokenIssuer.IssueAsync(user, organization.Name, cancellationToken);
        await auditService.WriteAsync(
            user.OrganizationId,
            user.Id,
            "auth.login",
            "user",
            user.Id.ToString(),
            new { user.Email, organization = organization.Slug, mfa = user.TwoFactorEnabled },
            cancellationToken);

        return Results.Ok(new LoginResponse(
            token.AccessToken,
            token.ExpiresAtUtc,
            new AuthenticatedUserResponse(
                user.Id,
                user.Email ?? string.Empty,
                user.FullName,
                organization.Name,
                user.DriverId,
                roles.OrderBy(x => x).ToArray(),
                user.TwoFactorEnabled),
            false,
            null,
            null));
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

    private static async Task<string?> GetTwoFactorChallengeMessageAsync(
        ApplicationUser user,
        IEnumerable<string> roles,
        string? code,
        UserManager<ApplicationUser> userManager)
    {
        var requiresAdminTwoFactor = user.TwoFactorEnabled
            && roles.Contains(SystemRoles.Admin, StringComparer.Ordinal);
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

    private static string NormalizeCode(string code)
    {
        var buffer = code.Where(char.IsAsciiDigit).ToArray();
        return new string(buffer);
    }
}
