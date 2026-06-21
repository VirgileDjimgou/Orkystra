using Orkystra.Contracts.Transport;

namespace Orkystra.Application.Connectors;

public interface ITransportProjectionProviderAdapter : ITransportProviderAdapter
{
    ValueTask<IReadOnlyCollection<RouteDetailReadModel>> ReadRouteDetailsAsync(CancellationToken cancellationToken = default);
}
