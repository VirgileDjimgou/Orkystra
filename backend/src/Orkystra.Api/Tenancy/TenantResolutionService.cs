using Microsoft.Extensions.Options;

namespace Orkystra.Api.Tenancy;

public sealed class TenantResolutionService
{
    private readonly TenantAccessOptions _options;

    public TenantResolutionService(IOptions<TenantAccessOptions> options)
    {
        _options = options.Value;
    }

    public string HeaderName => _options.TenantHeaderName;

    public TenantResolutionResult Resolve(IHeaderDictionary headers)
    {
        if (headers.TryGetValue(_options.TenantHeaderName, out var tenantId) &&
            !string.IsNullOrWhiteSpace(tenantId))
        {
            return new TenantResolutionResult(true, tenantId.ToString(), null);
        }

        if (_options.RequireTenantHeader)
        {
            return new TenantResolutionResult(false, null, $"Missing tenant header '{_options.TenantHeaderName}'.");
        }

        return new TenantResolutionResult(true, _options.DefaultTenantId, null);
    }
}
