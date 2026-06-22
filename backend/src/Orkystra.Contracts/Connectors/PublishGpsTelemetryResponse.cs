namespace Orkystra.Contracts.Connectors;

public sealed record PublishGpsTelemetryResponse(
    string Topic,
    int PublishedCount,
    IReadOnlyCollection<GpsPositionSnapshot> Positions,
    DateTimeOffset PublishedAtUtc);
