using Orkystra.Application.Connectors;
using Orkystra.Application.Connectors.Providers;
using Orkystra.Contracts.Connectors;

namespace Orkystra.Api.Connectors;

public sealed class ProviderCatalogService
{
    private readonly ProviderRegistry _providerRegistry;

    public ProviderCatalogService()
    {
        _providerRegistry = new ProviderRegistry(
        [
            new CsvWarehouseImportProvider(),
            new RestTransportProvider(),
            new GpsTelematicsProvider()
        ]);
    }

    public async ValueTask<ProviderCatalogResponse> BuildCatalogAsync(CancellationToken cancellationToken = default)
    {
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var providers = new List<ProviderCatalogItemReadModel>();

        foreach (var provider in _providerRegistry.ListAll())
        {
            var health = await provider.GetHealthAsync(cancellationToken);
            var syncStatus = await provider.GetSyncStatusAsync(cancellationToken);
            var capabilities = await provider.GetCapabilitiesAsync(cancellationToken);
            var schema = await provider.DescribeSchemaAsync(cancellationToken);

            providers.Add(new ProviderCatalogItemReadModel(
                provider.ProviderId,
                provider.ProviderName,
                provider.Domain.ToString(),
                provider.Kind.ToString(),
                health,
                syncStatus,
                capabilities,
                schema,
                GetSupportedReadModels(provider)));
        }

        return new ProviderCatalogResponse(generatedAtUtc, providers);
    }

    private static IReadOnlyCollection<string> GetSupportedReadModels(IProviderAdapter provider)
    {
        return provider switch
        {
            IWarehouseProviderAdapter => ["WarehouseSummaryReadModel"],
            ITransportProviderAdapter => ["RouteSummaryReadModel"],
            IGpsProviderAdapter => ["GpsPositionSnapshot"],
            _ => []
        };
    }
}
