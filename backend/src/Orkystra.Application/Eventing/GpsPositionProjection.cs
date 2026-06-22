using System.Collections.Concurrent;
using Orkystra.Contracts.Connectors;
using Orkystra.Contracts.Eventing;

namespace Orkystra.Application.Eventing;

public sealed class GpsPositionProjection : IEventProjection
{
    public const string GpsPositionReportedEventType = "GpsPositionReported";

    private readonly ConcurrentDictionary<Guid, GpsPositionSnapshot> _positions = new();

    public string ProjectionName => "gps-position-latest";

    public bool CanProject(IEventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return envelope.PayloadType == typeof(GpsPositionSnapshot)
            && string.Equals(envelope.EventType, GpsPositionReportedEventType, StringComparison.Ordinal);
    }

    public ValueTask ProjectAsync(IEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.Payload is GpsPositionSnapshot position)
        {
            _positions[position.TruckId] = position;
        }

        return ValueTask.CompletedTask;
    }

    public IReadOnlyCollection<GpsPositionSnapshot> ListAll()
    {
        return _positions.Values
            .OrderByDescending(static position => position.RecordedAt)
            .ToArray();
    }
}
