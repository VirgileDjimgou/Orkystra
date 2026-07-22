using FleetOps.Api.Tracking;

namespace FleetOps.Api.Integrations;

public sealed record CanonicalTelematicsEvent(
    string ProviderEventId,
    string ExternalDeviceId,
    DateTimeOffset OccurredAtUtc,
    double Latitude,
    double Longitude,
    double SpeedKph,
    double HeadingDegrees,
    long? SequenceNumber,
    double? AccuracyMeters,
    int? OdometerKm,
    string EventKind);

public sealed class SandboxTelematicsAdapter
{
    public const string ContractVersion = "sandbox-telematics.v1";

    public CanonicalTelematicsEvent Normalize(SandboxTelematicsEventRequest request)
    {
        if (!string.Equals(request.ContractVersion, ContractVersion, StringComparison.Ordinal))
        {
            throw new TrackingValidationException("contractVersion", $"Expected {ContractVersion}.");
        }

        if (string.IsNullOrWhiteSpace(request.ProviderEventId) || string.IsNullOrWhiteSpace(request.DeviceExternalId))
        {
            throw new TrackingValidationException("event", "Provider event and external device identifiers are required.");
        }

        if (request.EventKind is not ("position" or "heartbeat" or "diagnostic"))
        {
            throw new TrackingValidationException("eventKind", "Event kind must be position, heartbeat, or diagnostic.");
        }

        return new CanonicalTelematicsEvent(
            request.ProviderEventId.Trim(), request.DeviceExternalId.Trim(), request.OccurredAtUtc,
            request.Latitude, request.Longitude, request.SpeedKph, request.HeadingDegrees,
            request.SequenceNumber, request.AccuracyMeters, request.OdometerKm, request.EventKind);
    }
}
