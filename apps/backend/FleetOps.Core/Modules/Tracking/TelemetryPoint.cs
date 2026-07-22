using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Tracking;

public sealed class TelemetryPoint : TenantEntity
{
    private TelemetryPoint() { }

    public TelemetryPoint(
        Guid organizationId,
        Guid vehicleId,
        string deviceId,
        string eventId,
        DateTimeOffset recordedAtUtc,
        double latitude,
        double longitude,
        double speedKph,
        double headingDegrees,
        DateTimeOffset ingestedAtUtc,
        long? sequenceNumber = null,
        double? accuracyMeters = null,
        string source = "unknown",
        int qualityScore = 100,
        string anomalyFlags = "")
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        if (vehicleId == Guid.Empty) throw new ArgumentException("Vehicle identifier is required.", nameof(vehicleId));
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (latitude is < -90 or > 90) throw new ArgumentOutOfRangeException(nameof(latitude));
        if (longitude is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(longitude));
        ArgumentOutOfRangeException.ThrowIfNegative(speedKph);
        if (headingDegrees is < 0 or > 360) throw new ArgumentOutOfRangeException(nameof(headingDegrees));

        OrganizationId = organizationId;
        VehicleId = vehicleId;
        DeviceId = deviceId.Trim();
        EventId = eventId.Trim();
        RecordedAtUtc = recordedAtUtc.ToUniversalTime();
        Latitude = latitude;
        Longitude = longitude;
        SpeedKph = speedKph;
        HeadingDegrees = headingDegrees;
        IngestedAtUtc = ingestedAtUtc.ToUniversalTime();
        SequenceNumber = sequenceNumber;
        AccuracyMeters = accuracyMeters;
        Source = string.IsNullOrWhiteSpace(source) ? "unknown" : source.Trim();
        QualityScore = Math.Clamp(qualityScore, 0, 100);
        AnomalyFlags = anomalyFlags?.Trim() ?? string.Empty;
    }

    public Guid VehicleId { get; private set; }
    public string DeviceId { get; private set; } = string.Empty;
    public string EventId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAtUtc { get; private set; }
    public DateTimeOffset IngestedAtUtc { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public double SpeedKph { get; private set; }
    public double HeadingDegrees { get; private set; }
    public long? SequenceNumber { get; private set; }
    public double? AccuracyMeters { get; private set; }
    public string Source { get; private set; } = "unknown";
    public int QualityScore { get; private set; }
    public string AnomalyFlags { get; private set; } = string.Empty;
}
