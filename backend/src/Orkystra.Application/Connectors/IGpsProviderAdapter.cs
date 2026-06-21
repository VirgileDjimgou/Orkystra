using Orkystra.Contracts.Connectors;

namespace Orkystra.Application.Connectors;

public interface IGpsProviderAdapter : IProviderAdapter
{
    ValueTask<IReadOnlyCollection<GpsPositionSnapshot>> ReadPositionsAsync(CancellationToken cancellationToken = default);
}
