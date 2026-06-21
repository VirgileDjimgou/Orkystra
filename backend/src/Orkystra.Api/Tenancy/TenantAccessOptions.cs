namespace Orkystra.Api.Tenancy;

public sealed class TenantAccessOptions
{
    public const string SectionName = "TenantAccess";

    public bool RequireTenantHeader { get; init; } = false;

    public string TenantHeaderName { get; init; } = "X-Tenant-Id";

    public string DefaultTenantId { get; init; } = "local-demo-tenant";
}
