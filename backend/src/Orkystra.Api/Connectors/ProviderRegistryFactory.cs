using Orkystra.Application.Connectors;
using Orkystra.Application.Connectors.Providers;

namespace Orkystra.Api.Connectors;

public sealed class ProviderRegistryFactory
{
    private readonly ProviderRuntimeStore? _runtimeStore;
    private readonly ProviderSecretStore? _secretStore;
    private readonly IHttpClientFactory? _httpClientFactory;

    public ProviderRegistryFactory()
    {
    }

    public ProviderRegistryFactory(
        ProviderRuntimeStore runtimeStore,
        IHttpClientFactory httpClientFactory,
        ProviderSecretStore secretStore)
    {
        _runtimeStore = runtimeStore;
        _httpClientFactory = httpClientFactory;
        _secretStore = secretStore;
    }

    public ProviderRegistry CreateRegistry()
    {
        return new ProviderRegistry(
        [
            new CsvWarehouseImportProvider(),
            BuildRestTransportProvider(),
            new GpsTelematicsProvider()
        ]);
    }

    private RestTransportProvider BuildRestTransportProvider()
    {
        var runtime = _runtimeStore?.GetProvider("rest-transport-adapter");

        // Resolve the API key from the secret store when auth mode requires it.
        string? apiKey = null;
        if (_secretStore is not null)
        {
            apiKey = _secretStore.GetSecret("rest-transport-adapter", "apiKey");
        }

        var configuration = runtime is null
            ? RestTransportProviderConfiguration.LocalDemo
            : new RestTransportProviderConfiguration(
                runtime.Enabled,
                runtime.Environment,
                runtime.Settings.TryGetValue("baseUrl", out var baseUrl) ? baseUrl : null,
                runtime.Settings.TryGetValue("authMode", out var authMode) ? authMode : "none",
                ApiKey: apiKey);

        return new RestTransportProvider(
            _httpClientFactory?.CreateClient("provider-rest-transport"),
            configuration);
    }
}
