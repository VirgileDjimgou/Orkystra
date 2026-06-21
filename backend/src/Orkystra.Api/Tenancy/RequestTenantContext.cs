namespace Orkystra.Api.Tenancy;

public sealed class RequestTenantContext
{
    public string? TenantId { get; private set; }

    public void SetTenant(string tenantId)
    {
        TenantId = tenantId;
    }
}
