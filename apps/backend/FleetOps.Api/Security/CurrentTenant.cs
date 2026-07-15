using System.Security.Claims;

namespace FleetOps.Api.Security;

public sealed record CurrentTenant(Guid OrganizationId, string OrganizationName, Guid UserId, string Email);

public interface ICurrentTenantAccessor
{
    CurrentTenant GetRequiredTenant(ClaimsPrincipal principal);
}

public sealed class CurrentTenantAccessor : ICurrentTenantAccessor
{
    public CurrentTenant GetRequiredTenant(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var organizationIdValue = principal.FindFirstValue(TenantClaimTypes.OrganizationId);
        var organizationName = principal.FindFirstValue(TenantClaimTypes.OrganizationName);
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(ClaimTypes.Email);

        if (!Guid.TryParse(organizationIdValue, out var organizationId))
        {
            throw new InvalidOperationException("Missing organization_id claim.");
        }

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new InvalidOperationException("Missing user identifier claim.");
        }

        if (string.IsNullOrWhiteSpace(organizationName) || string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Missing identity claims.");
        }

        return new CurrentTenant(organizationId, organizationName, userId, email);
    }
}
