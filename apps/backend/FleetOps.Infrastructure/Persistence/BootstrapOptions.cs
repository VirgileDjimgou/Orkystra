namespace FleetOps.Infrastructure.Persistence;

public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public bool SeedDemoData { get; init; }

    public string OrganizationName { get; init; } = string.Empty;

    public string OrganizationSlug { get; init; } = string.Empty;

    public string AdminEmail { get; init; } = string.Empty;

    public string AdminPassword { get; init; } = string.Empty;

    public bool HasProvisioningValues =>
        !string.IsNullOrWhiteSpace(OrganizationName)
        && !string.IsNullOrWhiteSpace(OrganizationSlug)
        && !string.IsNullOrWhiteSpace(AdminEmail)
        && !string.IsNullOrWhiteSpace(AdminPassword);
}
