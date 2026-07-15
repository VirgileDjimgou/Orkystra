using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Fleet;

public sealed class Vehicle : TenantEntity
{
    private Vehicle() { }

    public Vehicle(Guid organizationId, string registrationNumber, string displayName)
    {
        OrganizationId = organizationId;
        RegistrationNumber = Require(registrationNumber, nameof(registrationNumber));
        DisplayName = Require(displayName, nameof(displayName));
    }

    public string RegistrationNumber { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private static string Require(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameter)
            : value.Trim();
}
