using Orkystra.Application.Connectors;
using Orkystra.Application.Connectors.Providers;
using Orkystra.Contracts.Transport;

namespace Orkystra.Api.ControlTower;

public sealed class TransportProjectionService
{
    private readonly ProviderRegistry _providerRegistry;

    public TransportProjectionService()
    {
        _providerRegistry = new ProviderRegistry(
        [
            new CsvWarehouseImportProvider(),
            new RestTransportProvider(),
            new GpsTelematicsProvider()
        ]);
    }

    public async ValueTask<IReadOnlyCollection<RouteSummaryReadModel>> ListAsync(CancellationToken cancellationToken = default)
    {
        var transportProvider = _providerRegistry.ListByDomain(ProviderDomain.Transport)
            .OfType<ITransportProviderAdapter>()
            .First();

        return await transportProvider.ReadRoutesAsync(cancellationToken);
    }

    public async ValueTask<RouteDetailReadModel?> GetByIdAsync(Guid routeId, CancellationToken cancellationToken = default)
    {
        var transportProvider = _providerRegistry.ListByDomain(ProviderDomain.Transport)
            .OfType<ITransportProjectionProviderAdapter>()
            .FirstOrDefault();

        if (transportProvider is null)
        {
            return null;
        }

        var routes = await transportProvider.ReadRouteDetailsAsync(cancellationToken);
        return routes.FirstOrDefault(route => route.RouteId == routeId);
    }
}
