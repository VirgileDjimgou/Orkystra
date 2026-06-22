using Orkystra.Application.Connectors;
using Orkystra.Application.Connectors.Providers;

namespace Orkystra.Api.Connectors;

public sealed class ProviderRegistryFactory
{
    private readonly ProviderRuntimeStore? _runtimeStore;
    private readonly IHttpClientFactory? _httpClientFactory;

    public ProviderRegistryFactory()
    {
    }

    public ProviderRegistryFactory(ProviderRuntimeStore runtimeStore, IHttpClientFactory httpClientFactory)
    {
        _runtimeStore = runtimeStore;
        _httpClientFactory = httpClientFactory;
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

        var configuration = runtime is null
            ? RestTransportProviderConfiguration.LocalDemo
            : new RestTransportProviderConfiguration(
                runtime.Enabled,
                runtime.Environment,
                runtime.Settings.TryGetValue("baseUrl", out var baseUrl) ? baseUrl : null,
                runtime.Settings.TryGetValue("authMode", out var authMode) ? authMode : "none",
                ApiKey: null);

        return new RestTransportProvider(
            _httpClientFactory?.CreateClient("provider-rest-transport"),
            configuration);
    }
}
