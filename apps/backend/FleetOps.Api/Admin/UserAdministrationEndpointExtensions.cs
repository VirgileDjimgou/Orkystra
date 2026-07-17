using FleetOps.Api.Auditing;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Identity;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.Admin;

public static class UserAdministrationEndpointExtensions
{
    public static IEndpointRouteBuilder MapUserAdministrationEndpoints(this IEndpointRouteBuilder app)
    {
        MapGroup(app.MapGroup("/api/v1/admin"));
        var legacy = app.MapGroup("/api/admin")
            .AddEndpointFilter(async (context, next) =>
            {
                context.HttpContext.Response.Headers.Append("Deprecation", "true");
                context.HttpContext.Response.Headers.Append("Link", "</api/v1/admin>; rel=successor-version");
                return await next(context);
            });
        MapGroup(legacy);

        return app;
    }

    private static void MapGroup(RouteGroupBuilder group)
    {
        group.RequireAuthorization(AuthorizationPolicies.AdminOnly);

        group.MapGet("/users", ListUsersAsync);
        group.MapPost("/users", CreateUserAsync);
    }

    private static async Task<IResult> ListUsersAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var users = await userManager.Users
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);

        var response = new List<UserSummaryResponse>(users.Count);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            response.Add(new UserSummaryResponse(
                user.Id,
                user.Email ?? string.Empty,
                user.FullName,
                roles.FirstOrDefault() ?? string.Empty,
                user.IsActive,
                user.DriverId));
        }

        return Results.Ok(response);
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        if (!SystemRoles.All.Contains(request.Role))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["role"] = [$"Role must be one of: {string.Join(", ", SystemRoles.All)}."]
            });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            FullName = request.FullName.Trim(),
            OrganizationId = tenant.OrganizationId,
            EmailConfirmed = true,
            IsActive = true,
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return Results.ValidationProblem(createResult.Errors
                .GroupBy(x => x.Code)
                .ToDictionary(
                    x => x.Key,
                    x => x.Select(error => error.Description).ToArray()));
        }

        var roleResult = await userManager.AddToRoleAsync(user, request.Role);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return Results.ValidationProblem(roleResult.Errors
                .GroupBy(x => x.Code)
                .ToDictionary(
                    x => x.Key,
                    x => x.Select(error => error.Description).ToArray()));
        }

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "admin.user_created",
            "user",
            user.Id.ToString(),
            new { user.Email, request.Role },
            cancellationToken);

        return Results.Created($"/api/admin/users/{user.Id}", new UserSummaryResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            request.Role,
            user.IsActive,
            user.DriverId));
    }
}
