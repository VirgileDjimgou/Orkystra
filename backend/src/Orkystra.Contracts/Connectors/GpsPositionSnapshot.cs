namespace Orkystra.Contracts.Connectors;

public sealed record GpsPositionSnapshot(
    Guid TruckId,
    string TruckReference,
    decimal Latitude,
    decimal Longitude,
    decimal SpeedKph,
    DateTimeOffset RecordedAt);
