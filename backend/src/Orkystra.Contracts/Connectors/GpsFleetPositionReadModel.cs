namespace Orkystra.Contracts.Connectors;

public sealed record GpsFleetPositionReadModel(
    Guid TruckId,
    string TruckReference,
    Guid? RouteId,
    string? RouteReference,
    string? RouteStatus,
    decimal Latitude,
    decimal Longitude,
    decimal SpeedKph,
    DateTimeOffset RecordedAtUtc,
    int MinutesSinceReading,
    string FreshnessPosture,
    string MovementPosture,
    string AlertPosture,
    string AlertSummary);
