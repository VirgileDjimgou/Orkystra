using Orkystra.Application.Connectors;
using Orkystra.Contracts.Connectors;

namespace Orkystra.Api.Connectors;

public sealed class ProviderCatalogService
{
    private readonly ProviderRegistryFactory _providerRegistryFactory;
    private readonly ProviderRuntimeStore _runtimeStore;
    private readonly ProviderSecretStore? _secretStore;

    public ProviderCatalogService(ProviderRuntimeStore runtimeStore)
    {
        _runtimeStore = runtimeStore;
        _secretStore = null;
        _providerRegistryFactory = new ProviderRegistryFactory();
    }

    public ProviderCatalogService(
        ProviderRuntimeStore runtimeStore,
        ProviderSecretStore secretStore,
        ProviderRegistryFactory providerRegistryFactory)
    {
        _runtimeStore = runtimeStore;
        _secretStore = secretStore;
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
        var authMode = ProviderRuntimeMetadata.GetAuthMode(configuredProvider);
        var authConfigured = IsAuthConfigured(providerId, authMode);
        var writebackMode = ProviderRuntimeMetadata.GetWritebackMode(providerId, configuredProvider);
        var (writebackReadiness, writebackSummary) = BuildWritebackPosture(
            providerId,
            configuredProvider,
            authMode,
            authConfigured,
            writebackMode);

        if (configuredProvider is null)
        {
            return new ProviderConfigurationSummaryReadModel(
                false,
                "unconfigured",
                "Missing Configuration",
                [],
                requiredFields,
                editableFields.Select(field => new ProviderConfigurationSettingReadModel(
                    field,
                    GetEditableSettingValue(providerId, configuredProvider, field),
                    requiredFields.Contains(field, StringComparer.OrdinalIgnoreCase))).ToArray(),
                authMode,
                authConfigured,
                writebackMode,
                writebackReadiness,
                writebackSummary);
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

        string readiness;

        if (!configuredProvider.Enabled)
        {
            readiness = "Disabled";
        }
        else if (missingFields.Length > 0)
        {
            readiness = configuredFields.Length == 0 ? "Missing Configuration" : "Partial Configuration";
        }
        else if (!authConfigured && !string.Equals(authMode, "none", StringComparison.OrdinalIgnoreCase))
        {
            readiness = "Auth Key Missing";
        }
        else
        {
            readiness = "Configured";
        }

        return new ProviderConfigurationSummaryReadModel(
            configuredProvider.Enabled,
            configuredProvider.Environment,
            readiness,
            configuredFields,
            missingFields,
                editableFields
                .Select(field => new ProviderConfigurationSettingReadModel(
                    field,
                    GetEditableSettingValue(providerId, configuredProvider, field),
                    requiredFields.Contains(field, StringComparer.OrdinalIgnoreCase)))
                .ToArray(),
            authMode,
            authConfigured,
            writebackMode,
            writebackReadiness,
            writebackSummary);
    }

    private static string GetEditableSettingValue(
        string providerId,
        ProviderRuntimeSettings? configuredProvider,
        string field)
    {
        if (string.Equals(field, "writebackMode", StringComparison.OrdinalIgnoreCase))
        {
            return ProviderRuntimeMetadata.GetWritebackMode(providerId, configuredProvider);
        }

        return configuredProvider is not null &&
               configuredProvider.Settings.TryGetValue(field, out var value)
            ? value
            : string.Empty;
    }

    private static (string Readiness, string Summary) BuildWritebackPosture(
        string providerId,
        ProviderRuntimeSettings? configuredProvider,
        string authMode,
        bool authConfigured,
        string writebackMode)
    {
        if (!ProviderRuntimeMetadata.SupportsWriteback(providerId))
        {
            return (
                "Read-only",
                "This provider is currently exposed as read-only. No connector writeback path is available in the product shell.");
        }

        if (configuredProvider is null || !configuredProvider.Enabled)
        {
            return (
                "Disabled",
                "Writeback is disabled until the provider is enabled and intentionally configured.");
        }

        if (!string.Equals(authMode, "none", StringComparison.OrdinalIgnoreCase) && !authConfigured)
        {
            return (
                "Auth Required",
                "Writeback is blocked until the required authentication material is configured.");
        }

        return writebackMode switch
        {
            "enabled" => (
                "Writeback Enabled",
                "Writeback is enabled for this provider. Use only against an intentionally reviewed upstream environment."),
            "disabled" => (
                "Disabled",
                "Writeback is intentionally disabled for this provider."),
            _ => (
                "Dry-run Only",
                "Writeback remains in dry-run posture. Operator workflows may prepare actions, but upstream mutation should stay disabled.")
        };
    }

    private bool IsAuthConfigured(string providerId, string authMode)
    {
        if (string.Equals(authMode, "none", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(authMode, "api-key", StringComparison.OrdinalIgnoreCase))
        {
            return _secretStore?.HasSecret(providerId, "apiKey") == true;
        }

        // Unknown auth modes are treated as not configured.
        return false;
    }
}
