using FleetOps.Api.Auditing;
using FleetOps.Api.Security;
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

        group.MapPost("/login", LoginAsync);
        group.MapGet("/me", GetCurrentUserAsync).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
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

        if (user is null || !user.IsActive || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Results.Unauthorized();
        }

        var organization = await dbContext.Organizations.SingleAsync(
            x => x.Id == user.OrganizationId,
            cancellationToken);

        var token = await tokenIssuer.IssueAsync(user, organization.Name, cancellationToken);
        var roles = await userManager.GetRolesAsync(user);
        await auditService.WriteAsync(
            user.OrganizationId,
            user.Id,
            "auth.login",
            "user",
            user.Id.ToString(),
            new { user.Email, organization = organization.Slug },
            cancellationToken);

        return Results.Ok(new LoginResponse(
            token.AccessToken,
            token.ExpiresAtUtc,
            new AuthenticatedUserResponse(
                user.Id,
                user.Email ?? string.Empty,
                user.FullName,
                organization.Name,
                roles.OrderBy(x => x).ToArray())));
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
            roles.OrderBy(x => x).ToArray()));
    }
}
