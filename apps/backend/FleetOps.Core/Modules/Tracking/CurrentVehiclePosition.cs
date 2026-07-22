using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Tracking;

public sealed class CurrentVehiclePosition : TenantEntity
{
    private CurrentVehiclePosition() { }

    public CurrentVehiclePosition(
        Guid organizationId,
        Guid vehicleId,
        string deviceId,
        string eventId,
        DateTimeOffset recordedAtUtc,
        double latitude,
        double longitude,
        double speedKph,
        double headingDegrees,
        DateTimeOffset? ingestedAtUtc = null,
        long? sequenceNumber = null,
        double? accuracyMeters = null,
        string source = "unknown",
        int qualityScore = 100,
        string anomalyFlags = "")
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        OrganizationId = organizationId;

        Apply(
            vehicleId,
            deviceId,
            eventId,
            recordedAtUtc,
            latitude,
            longitude,
            speedKph,
            headingDegrees);
        IngestedAtUtc = (ingestedAtUtc ?? recordedAtUtc).ToUniversalTime();
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
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public double SpeedKph { get; private set; }
    public double HeadingDegrees { get; private set; }
    public DateTimeOffset IngestedAtUtc { get; private set; }
    public long? SequenceNumber { get; private set; }
    public double? AccuracyMeters { get; private set; }
    public string Source { get; private set; } = "unknown";
    public int QualityScore { get; private set; } = 100;
    public string AnomalyFlags { get; private set; } = string.Empty;

    public void UpdateFrom(TelemetryPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);

        Apply(
            point.VehicleId,
            point.DeviceId,
            point.EventId,
            point.RecordedAtUtc,
            point.Latitude,
            point.Longitude,
            point.SpeedKph,
            point.HeadingDegrees);
        IngestedAtUtc = point.IngestedAtUtc;
        SequenceNumber = point.SequenceNumber;
        AccuracyMeters = point.AccuracyMeters;
        Source = point.Source;
        QualityScore = point.QualityScore;
        AnomalyFlags = point.AnomalyFlags;
    }

    private void Apply(
        Guid vehicleId,
        string deviceId,
        string eventId,
        DateTimeOffset recordedAtUtc,
        double latitude,
        double longitude,
        double speedKph,
        double headingDegrees)
    {
        if (vehicleId == Guid.Empty) throw new ArgumentException("Vehicle identifier is required.", nameof(vehicleId));
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (latitude is < -90 or > 90) throw new ArgumentOutOfRangeException(nameof(latitude));
        if (longitude is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(longitude));
        ArgumentOutOfRangeException.ThrowIfNegative(speedKph);
        if (headingDegrees is < 0 or > 360) throw new ArgumentOutOfRangeException(nameof(headingDegrees));

        VehicleId = vehicleId;
        DeviceId = deviceId.Trim();
        EventId = eventId.Trim();
        RecordedAtUtc = recordedAtUtc.ToUniversalTime();
        Latitude = latitude;
        Longitude = longitude;
        SpeedKph = speedKph;
        HeadingDegrees = headingDegrees;
    }
}
