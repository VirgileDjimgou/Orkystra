using FleetOps.Core.Modules.Tracking;

namespace FleetOps.Api.Tracking;

public static class TrackingPositionMapper
{
    public static TrackingPositionResponse FromCurrent(
        CurrentVehiclePosition position,
        string registrationNumber,
        string displayName,
        DateTimeOffset now)
    {
        var status = GetStatus(position.QualityScore, position.AnomalyFlags, position.AccuracyMeters, position.IngestedAtUtc, now);
        return new TrackingPositionResponse(
            position.VehicleId,
            registrationNumber,
            displayName,
            position.DeviceId,
            position.RecordedAtUtc,
            position.Latitude,
            position.Longitude,
            position.SpeedKph,
            position.HeadingDegrees,
            position.SequenceNumber,
            position.AccuracyMeters,
            position.Source,
            position.QualityScore,
            status.Status,
            status.Reason);
    }

    public static TrackingPositionResponse FromTelemetry(
        TelemetryPoint point,
        string registrationNumber,
        string displayName,
        DateTimeOffset now)
    {
        var status = GetStatus(point.QualityScore, point.AnomalyFlags, point.AccuracyMeters, point.IngestedAtUtc, now);
        return new TrackingPositionResponse(
            point.VehicleId,
            registrationNumber,
            displayName,
            point.DeviceId,
            point.RecordedAtUtc,
            point.Latitude,
            point.Longitude,
            point.SpeedKph,
            point.HeadingDegrees,
            point.SequenceNumber,
            point.AccuracyMeters,
            point.Source,
            point.QualityScore,
            status.Status,
            status.Reason);
    }

    public static (string Status, string Reason) GetStatus(
        int qualityScore,
        string anomalyFlags,
        double? accuracyMeters,
        DateTimeOffset ingestedAtUtc,
        DateTimeOffset now)
    {
        if (qualityScore < 50 || anomalyFlags.Contains("implausible-jump", StringComparison.Ordinal) || anomalyFlags.Contains("clock-skew", StringComparison.Ordinal))
        {
            return ("Invalid", string.IsNullOrEmpty(anomalyFlags) ? "Telemetry failed quality checks." : anomalyFlags);
        }

        if (accuracyMeters is > 100) return ("Inaccurate", "GPS accuracy is above 100 metres.");
        if (now - ingestedAtUtc > TimeSpan.FromMinutes(10)) return ("Silent", "No recent communication was received.");
        if (now - ingestedAtUtc > TimeSpan.FromMinutes(2)) return ("Delayed", "Last telemetry is older than two minutes.");
        return ("Fresh", "Position is reliable.");
    }
}
