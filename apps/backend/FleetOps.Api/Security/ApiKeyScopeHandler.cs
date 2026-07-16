using Microsoft.AspNetCore.Authorization;

namespace FleetOps.Api.Security;

public sealed class ApiKeyScopeHandler : AuthorizationHandler<ApiKeyScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ApiKeyScopeRequirement requirement)
    {
        if (context.User.FindAll("scope").Any(x => string.Equals(x.Value, requirement.Scope, StringComparison.Ordinal)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
