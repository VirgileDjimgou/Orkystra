using Orkystra.Application.Connectors;
using Orkystra.Contracts.Connectors;

namespace Orkystra.Api.Connectors;

public sealed class ProviderCatalogService
{
    private readonly ProviderRegistryFactory _providerRegistryFactory;
    private readonly ProviderRuntimeStore _runtimeStore;

    public ProviderCatalogService(ProviderRuntimeStore runtimeStore)
    {
        _runtimeStore = runtimeStore;
        _providerRegistryFactory = new ProviderRegistryFactory();
    }

    public ProviderCatalogService(ProviderRuntimeStore runtimeStore, ProviderRegistryFactory providerRegistryFactory)
    {
        _runtimeStore = runtimeStore;
        _providerRegistryFactory = providerRegistryFactory;
    }

    public async ValueTask<ProviderCatalogResponse> BuildCatalogAsync(CancellationToken cancellationToken = default)
    {
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var providers = new List<ProviderCatalogItemReadModel>();
        var providerRegistry = _providerRegistryFactory.CreateRegistry();

        foreach (var provider in providerRegistry.ListAll())
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
            ITransportProjectionProviderAdapter => ["RouteSummaryReadModel", "RouteDetailReadModel"],
            ITransportProviderAdapter => ["RouteSummaryReadModel"],
            IGpsProviderAdapter => ["GpsPositionSnapshot"],
            _ => []
        };
    }

    private ProviderConfigurationSummaryReadModel BuildConfigurationSummary(string providerId)
    {
        var configuredProvider = _runtimeStore.GetProvider(providerId);
        var editableFields = ProviderRuntimeMetadata.GetEditableFields(providerId);
        var requiredFields = ProviderRuntimeMetadata.GetRequiredFields(providerId);

        if (configuredProvider is null)
        {
            return new ProviderConfigurationSummaryReadModel(
                false,
                "unconfigured",
                "Missing Configuration",
                [],
                requiredFields,
                editableFields.Select(field => new ProviderConfigurationSettingReadModel(field, string.Empty, requiredFields.Contains(field, StringComparer.OrdinalIgnoreCase))).ToArray());
        }

        var configuredFields = configuredProvider.Settings
            .Where(setting => !string.IsNullOrWhiteSpace(setting.Value))
            .Select(setting => setting.Key)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var missingFields = requiredFields
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
            missingFields,
            editableFields
                .Select(field => new ProviderConfigurationSettingReadModel(
                    field,
                    configuredProvider.Settings.TryGetValue(field, out var value) ? value : string.Empty,
                    requiredFields.Contains(field, StringComparer.OrdinalIgnoreCase)))
                .ToArray());
    }
}
