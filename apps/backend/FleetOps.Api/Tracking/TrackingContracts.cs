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
    double HeadingDegrees,
    long? SequenceNumber = null,
    double? AccuracyMeters = null,
    string Source = "unknown");

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
    double HeadingDegrees,
    long? SequenceNumber = null,
    double? AccuracyMeters = null,
    string Source = "unknown",
    int QualityScore = 100,
    string QualityStatus = "Fresh",
    string QualityReason = "Position is reliable.");

public sealed record TrackingHistoryItemResponse(
    string EventId,
    Guid VehicleId,
    string DeviceId,
    DateTimeOffset RecordedAtUtc,
    DateTimeOffset IngestedAtUtc,
    double Latitude,
    double Longitude,
    double SpeedKph,
    double HeadingDegrees,
    long? SequenceNumber = null,
    double? AccuracyMeters = null,
    string Source = "unknown",
    int QualityScore = 100,
    string AnomalyFlags = "");

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

public sealed record TrackingDiagnosticResponse(
    Guid VehicleId,
    string RegistrationNumber,
    string DisplayName,
    string? DriverName,
    string DeviceId,
    DateTimeOffset? LastCommunicationAtUtc,
    string Status,
    string Reason,
    int QualityScore,
    double? AccuracyMeters,
    string Source,
    long? SequenceNumber);

public sealed record TrackingTripResponse(Guid Id, Guid VehicleId, DateTimeOffset StartedAtUtc, DateTimeOffset EndedAtUtc, double DistanceKm, int StopCount, int PointCount, string AlgorithmVersion);
public sealed record RecalculateTripsRequest(Guid VehicleId, DateTimeOffset FromUtc, DateTimeOffset ToUtc);
public sealed record RecalculateTripsResponse(int DeletedCount, int CreatedCount, string AlgorithmVersion);

public sealed record TrackingCoordinateRequest(double Latitude, double Longitude);
public sealed record CreateTrackingGeofenceRequest(string Name, string Shape, double? CenterLatitude, double? CenterLongitude, double? RadiusMeters, IReadOnlyList<TrackingCoordinateRequest>? Polygon);
public sealed record TrackingGeofenceResponse(Guid Id, string Name, string Shape, double? CenterLatitude, double? CenterLongitude, double? RadiusMeters, IReadOnlyList<TrackingCoordinateRequest> Polygon);
public sealed record TrackingGeofenceEventResponse(Guid Id, Guid GeofenceId, string GeofenceName, Guid VehicleId, string RegistrationNumber, string Transition, DateTimeOffset OccurredAtUtc, string TelemetryEventId);
