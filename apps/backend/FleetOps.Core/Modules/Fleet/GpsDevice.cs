using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Fleet;

public sealed class GpsDevice : TenantEntity
{
    private GpsDevice() { }

    public GpsDevice(Guid organizationId, string serialNumber, string? displayName = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        OrganizationId = organizationId;
        SerialNumber = RequireNonEmpty(serialNumber, nameof(serialNumber));
        DisplayName = NormalizeOptional(displayName);
    }

    public string SerialNumber { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public bool IsActive { get; private set; } = true;
    public long RowVersion { get; private set; }

    public void Rename(string? displayName)
    {
        DisplayName = NormalizeOptional(displayName);
        RowVersion++;
    }

    public void Activate()
    {
        if (IsActive)
        {
            throw new InvalidOperationException("Device is already active.");
        }

        IsActive = true;
        RowVersion++;
    }

    public void Deactivate()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Device is already inactive.");
        }

        IsActive = false;
        RowVersion++;
    }

    private static string RequireNonEmpty(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameter)
            : value.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
