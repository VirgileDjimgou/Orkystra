using System.Text.Json;
using Orkystra.Application.Connectors;
using Orkystra.Api.Connectors;
using Orkystra.Api.Persistence;
using Orkystra.Contracts.Transport;

namespace Orkystra.Api.ControlTower;

public sealed class TransportProjectionService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ProviderRegistryFactory? _providerRegistryFactory;
    private readonly ITransportProjectionProviderAdapter? _transportProvider;
    private readonly IOperationalPersistenceStore? _persistenceStore;

    public TransportProjectionService()
    {
        _providerRegistryFactory = new ProviderRegistryFactory();
    }

    public TransportProjectionService(ProviderRegistryFactory providerRegistryFactory, IOperationalPersistenceStore persistenceStore)
    {
        _providerRegistryFactory = providerRegistryFactory;
        _persistenceStore = persistenceStore;
    }

    public TransportProjectionService(ITransportProjectionProviderAdapter transportProvider, IOperationalPersistenceStore persistenceStore)
    {
        _transportProvider = transportProvider;
        _persistenceStore = persistenceStore;
    }

    public async ValueTask<IReadOnlyCollection<RouteSummaryReadModel>> ListAsync(
        string tenantId = "local-demo-tenant",
        CancellationToken cancellationToken = default)
    {
        var persistedSnapshot = await ReadPersistedAsync<IReadOnlyCollection<RouteSummaryReadModel>>(
            tenantId,
            "route-summaries",
            "all",
            cancellationToken);
        if (persistedSnapshot is { Count: > 0 })
        {
            return persistedSnapshot;
        }

        var transportProvider = ResolveTransportProvider();

        return await transportProvider.ReadRoutesAsync(cancellationToken);
    }

    public async ValueTask<RouteDetailReadModel?> GetByIdAsync(
        Guid routeId,
        string tenantId = "local-demo-tenant",
        CancellationToken cancellationToken = default)
    {
        var persistedRoute = await ReadPersistedAsync<RouteDetailReadModel>(
            tenantId,
            "route-detail",
            routeId.ToString("D"),
            cancellationToken);
        if (persistedRoute is not null)
        {
            return persistedRoute;
        }

        var transportProvider = ResolveTransportProvider();
        var routes = await transportProvider.ReadRouteDetailsAsync(cancellationToken);
        return routes.FirstOrDefault(route => route.RouteId == routeId);
    }

    private ITransportProjectionProviderAdapter ResolveTransportProvider()
    {
        if (_transportProvider is not null)
        {
            return _transportProvider;
        }

        return _providerRegistryFactory!.CreateRegistry().ListByDomain(ProviderDomain.Transport)
            .OfType<ITransportProjectionProviderAdapter>()
            .First();
    }

    private async ValueTask<TPayload?> ReadPersistedAsync<TPayload>(
        string tenantId,
        string projectionName,
        string projectionKey,
        CancellationToken cancellationToken)
    {
        if (_persistenceStore is null)
        {
            return default;
        }

        var snapshot = await _persistenceStore.ReadProjectionSnapshotAsync(
            tenantId,
            projectionName,
            projectionKey,
            cancellationToken);
        if (snapshot is null)
        {
            return default;
        }

        return JsonSerializer.Deserialize<TPayload>(snapshot.PayloadJson, SerializerOptions);
    }
}
