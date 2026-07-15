using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Fleet;

public sealed class Driver : TenantEntity
{
    private Driver() { }

    public Driver(Guid organizationId, string fullName, string licenseNumber, string? phoneNumber = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        OrganizationId = organizationId;
        FullName = RequireNonEmpty(fullName, nameof(fullName));
        LicenseNumber = RequireNonEmpty(licenseNumber, nameof(licenseNumber));
        PhoneNumber = NormalizeOptional(phoneNumber);
    }

    public string FullName { get; private set; } = string.Empty;
    public string LicenseNumber { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public bool IsActive { get; private set; } = true;
    public long RowVersion { get; private set; }

    public void Update(string fullName, string? phoneNumber)
    {
        FullName = RequireNonEmpty(fullName, nameof(fullName));
        PhoneNumber = NormalizeOptional(phoneNumber);
        RowVersion++;
    }

    public void Activate()
    {
        if (IsActive)
        {
            throw new InvalidOperationException("Driver is already active.");
        }

        IsActive = true;
        RowVersion++;
    }

    public void Deactivate()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Driver is already inactive.");
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
