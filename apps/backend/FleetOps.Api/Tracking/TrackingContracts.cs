namespace FleetOps.Api.Tracking;

public sealed record IngestTelemetryRequest(
    Guid OrganizationId,
    Guid VehicleId,
    string DeviceId,
    string EventId,
    DateTimeOffset RecordedAtUtc,
    double Latitude,
    double Longitude,
    double SpeedKph,
    double HeadingDegrees);

public sealed record TelemetryIngestionResponse(
    string Status,
    bool Duplicate,
    bool OutOfOrder,
    bool CurrentPositionUpdated,
    int RetentionDeletedCount);

public sealed record TrackingPositionResponse(
    Guid VehicleId,
    string RegistrationNumber,
    string DisplayName,
    string DeviceId,
    DateTimeOffset RecordedAtUtc,
    double Latitude,
    double Longitude,
    double SpeedKph,
    double HeadingDegrees);

public sealed record TrackingHistoryItemResponse(
    string EventId,
    Guid VehicleId,
    string DeviceId,
    DateTimeOffset RecordedAtUtc,
    DateTimeOffset IngestedAtUtc,
    double Latitude,
    double Longitude,
    double SpeedKph,
    double HeadingDegrees);

public sealed record TrackingHistoryPageResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<TrackingHistoryItemResponse> Items);

public sealed record TrackingMetricsResponse(
    int CurrentVehicleCount,
    int HistoryPointCount,
    long AcceptedCount,
    long DuplicateCount,
    long OutOfOrderCount,
    int RetentionDays);

public sealed record TrackingScenarioResponse(
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    IReadOnlyList<TrackingScenarioVehicleResponse> Vehicles);

public sealed record TrackingScenarioVehicleResponse(
    Guid VehicleId,
    string RegistrationNumber,
    string DisplayName,
    string DeviceId);

public sealed record TrackingScenarioResetResponse(
    int DeletedHistoryPoints,
    int DeletedCurrentPositions);
