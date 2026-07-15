using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Tracking;

public sealed class TelemetryPoint : TenantEntity
{
    private TelemetryPoint() { }

    public TelemetryPoint(
        Guid organizationId,
        Guid vehicleId,
        string deviceId,
        DateTimeOffset recordedAtUtc,
        double latitude,
        double longitude,
        double speedKph)
    {
        if (latitude is < -90 or > 90) throw new ArgumentOutOfRangeException(nameof(latitude));
        if (longitude is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(longitude));
        if (speedKph < 0) throw new ArgumentOutOfRangeException(nameof(speedKph));

        OrganizationId = organizationId;
        VehicleId = vehicleId;
        DeviceId = deviceId.Trim();
        RecordedAtUtc = recordedAtUtc.ToUniversalTime();
        Latitude = latitude;
        Longitude = longitude;
        SpeedKph = speedKph;
    }

    public Guid VehicleId { get; private set; }
    public string DeviceId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAtUtc { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public double SpeedKph { get; private set; }
}
