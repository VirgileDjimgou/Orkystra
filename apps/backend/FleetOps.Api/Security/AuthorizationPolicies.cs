using FleetOps.Core.Modules.Identity;

namespace FleetOps.Api.Security;

public static class AuthorizationPolicies
{
    public const string AdminOnly = "fleetops-admin-only";
    public const string FleetRead = "fleetops-fleet-read";
    public const string OperationsWrite = "fleetops-operations-write";
    public const string DriverOnly = "fleetops-driver-only";

    public static IReadOnlyDictionary<string, string[]> RoleMatrix { get; } =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [AdminOnly] = [SystemRoles.Admin],
            [FleetRead] = [SystemRoles.Admin, SystemRoles.Operator],
            [OperationsWrite] = [SystemRoles.Admin, SystemRoles.Operator],
            [DriverOnly] = [SystemRoles.Driver],
        };
}
