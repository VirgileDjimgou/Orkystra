using Orkystra.Application.Eventing;
using Orkystra.Contracts.Connectors;

namespace Orkystra.Api.Gps;

public sealed class GpsProjectionService
{
    private readonly GpsPositionProjection _gpsPositionProjection;

    public GpsProjectionService(GpsPositionProjection gpsPositionProjection)
    {
        _gpsPositionProjection = gpsPositionProjection;
    }

    public ValueTask<IReadOnlyCollection<GpsPositionSnapshot>> ListAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(_gpsPositionProjection.ListAll());
    }
}
