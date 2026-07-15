namespace FleetOps.Api;

public sealed record TelemetryContract(
    Guid OrganizationId,
    Guid VehicleId,
    string DeviceId,
    DateTimeOffset RecordedAtUtc,
    double Latitude,
    double Longitude,
    double SpeedKph,
    double HeadingDegrees);
