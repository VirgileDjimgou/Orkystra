using Microsoft.AspNetCore.Authorization;

namespace FleetOps.Api.Security;

public sealed class ApiKeyScopeRequirement(string scope) : IAuthorizationRequirement
{
    public string Scope { get; } = scope;
}
