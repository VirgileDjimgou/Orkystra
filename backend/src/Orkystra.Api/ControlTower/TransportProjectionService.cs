using Orkystra.Application.Connectors;
using Orkystra.Api.Connectors;
using Orkystra.Contracts.Transport;

namespace Orkystra.Api.ControlTower;

public sealed class TransportProjectionService
{
    private readonly ProviderRegistryFactory _providerRegistryFactory;

    public TransportProjectionService()
    {
        _providerRegistryFactory = new ProviderRegistryFactory();
    }

    public TransportProjectionService(ProviderRegistryFactory providerRegistryFactory)
    {
        _providerRegistryFactory = providerRegistryFactory;
    }

    public async ValueTask<IReadOnlyCollection<RouteSummaryReadModel>> ListAsync(CancellationToken cancellationToken = default)
    {
        var transportProvider = _providerRegistryFactory.CreateRegistry().ListByDomain(ProviderDomain.Transport)
            .OfType<ITransportProviderAdapter>()
            .First();

        return await transportProvider.ReadRoutesAsync(cancellationToken);
    }

    public async ValueTask<RouteDetailReadModel?> GetByIdAsync(Guid routeId, CancellationToken cancellationToken = default)
    {
        var transportProvider = _providerRegistryFactory.CreateRegistry().ListByDomain(ProviderDomain.Transport)
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
