using Orkystra.Application.Connectors;
using Orkystra.Application.Connectors.Providers;
using Orkystra.Contracts.Connectors;
using Microsoft.Extensions.Options;

namespace Orkystra.Api.Connectors;

public sealed class ProviderCatalogService
{
    private readonly ProviderRegistry _providerRegistry;
    private readonly ProviderRuntimeOptions _runtimeOptions;

    public ProviderCatalogService(IOptions<ProviderRuntimeOptions> runtimeOptions)
    {
        _runtimeOptions = runtimeOptions.Value;
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
                BuildConfigurationSummary(provider.ProviderId),
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

    private ProviderConfigurationSummaryReadModel BuildConfigurationSummary(string providerId)
    {
        var configuredProvider = _runtimeOptions.Providers.FirstOrDefault(provider =>
            string.Equals(provider.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));

        if (configuredProvider is null)
        {
            return new ProviderConfigurationSummaryReadModel(
                false,
                "unconfigured",
                "Missing Configuration",
                [],
                GetRequiredFields(providerId));
        }

        var configuredFields = configuredProvider.Settings
            .Where(setting => !string.IsNullOrWhiteSpace(setting.Value))
            .Select(setting => setting.Key)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var missingFields = GetRequiredFields(providerId)
            .Except(configuredFields, StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var readiness = !configuredProvider.Enabled
            ? "Disabled"
            : missingFields.Length == 0
                ? "Configured"
                : configuredFields.Length == 0
                    ? "Missing Configuration"
                    : "Partial Configuration";

        return new ProviderConfigurationSummaryReadModel(
            configuredProvider.Enabled,
            configuredProvider.Environment,
            readiness,
            configuredFields,
            missingFields);
    }

    private static IReadOnlyCollection<string> GetRequiredFields(string providerId)
    {
        return providerId switch
        {
            "csv-warehouse-import" => ["sourcePath", "importSchedule"],
            "rest-transport-adapter" => ["baseUrl", "authMode"],
            "gps-telematics-adapter" => ["streamTopic", "snapshotIntervalSeconds"],
            _ => []
        };
    }
}
