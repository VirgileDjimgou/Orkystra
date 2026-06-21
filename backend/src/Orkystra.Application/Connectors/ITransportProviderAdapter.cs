using Orkystra.Contracts.Transport;

namespace Orkystra.Application.Connectors;

public interface ITransportProviderAdapter : IProviderAdapter
{
    ValueTask<IReadOnlyCollection<RouteSummaryReadModel>> ReadRoutesAsync(CancellationToken cancellationToken = default);
}
