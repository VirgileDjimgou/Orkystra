using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Identities;
using Orkystra.Domain.ValueObjects;

namespace Orkystra.Domain.Transport;

public sealed class Stop : Entity<StopId>
{
    public Stop(StopId id, int sequence, string name, GeoCoordinate coordinate, TimeWindow timeWindow)
        : base(id)
    {
        Sequence = sequence;
        Name = name;
        Coordinate = coordinate;
        TimeWindow = timeWindow;
    }

    public int Sequence { get; }

    public string Name { get; }

    public GeoCoordinate Coordinate { get; }

    public TimeWindow TimeWindow { get; }
}
