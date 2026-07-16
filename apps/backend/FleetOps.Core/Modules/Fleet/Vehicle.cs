using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Fleet;

public sealed class Vehicle : TenantEntity
{
    private Vehicle() { }

    public Vehicle(Guid organizationId, string registrationNumber, string displayName)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        OrganizationId = organizationId;
        RegistrationNumber = RequireNonEmpty(registrationNumber, nameof(registrationNumber));
        DisplayName = RequireNonEmpty(displayName, nameof(displayName));
    }

    public string RegistrationNumber { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public int CurrentOdometerKm { get; private set; }
    public long RowVersion { get; private set; }

    public void Rename(string displayName)
    {
        DisplayName = RequireNonEmpty(displayName, nameof(displayName));
        RowVersion++;
    }

    public void Activate()
    {
        if (IsActive)
        {
            throw new InvalidOperationException("Vehicle is already active.");
        }

        IsActive = true;
        RowVersion++;
    }

    public void Deactivate()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Vehicle is already inactive.");
        }

        IsActive = false;
        RowVersion++;
    }

    public void UpdateCurrentOdometer(int currentOdometerKm)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentOdometerKm);
        CurrentOdometerKm = currentOdometerKm;
        RowVersion++;
    }

    private static string RequireNonEmpty(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameter)
            : value.Trim();
}
