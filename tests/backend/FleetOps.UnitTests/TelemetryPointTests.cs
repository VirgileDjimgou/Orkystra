using FleetOps.Core.Modules.Tracking;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class TelemetryPointTests
{
    [Fact]
    public void ConstructorRejectsInvalidLatitude()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TelemetryPoint(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "device",
            "evt-1",
            DateTimeOffset.UtcNow,
            91,
            7.5,
            10,
            180,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ConstructorStoresNormalizedTrackingValues()
    {
        var recordedAt = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.FromHours(2));
        var ingestedAt = new DateTimeOffset(2026, 7, 15, 10, 0, 1, TimeSpan.Zero);

        var point = new TelemetryPoint(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "  NW-GPS-100  ",
            "  evt-100  ",
            recordedAt,
            48.4,
            9.2,
            42,
            90,
            ingestedAt);

        Assert.Equal("NW-GPS-100", point.DeviceId);
        Assert.Equal("evt-100", point.EventId);
        Assert.Equal(recordedAt.ToUniversalTime(), point.RecordedAtUtc);
        Assert.Equal(ingestedAt, point.IngestedAtUtc);
        Assert.Equal(90, point.HeadingDegrees);
    }
}

public sealed class CurrentVehiclePositionTests
{
    [Fact]
    public void UpdateFromTelemetryPointReplacesCurrentSnapshot()
    {
        var organizationId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var current = new CurrentVehiclePosition(
            organizationId,
            vehicleId,
            "NW-GPS-100",
            "evt-1",
            DateTimeOffset.UtcNow.AddMinutes(-1),
            48.4,
            9.2,
            32,
            45);
        var incoming = new TelemetryPoint(
            organizationId,
            vehicleId,
            "NW-GPS-100",
            "evt-2",
            DateTimeOffset.UtcNow,
            48.5,
            9.3,
            38,
            120,
            DateTimeOffset.UtcNow);

        current.UpdateFrom(incoming);

        Assert.Equal("evt-2", current.EventId);
        Assert.Equal(48.5, current.Latitude);
        Assert.Equal(9.3, current.Longitude);
        Assert.Equal(38, current.SpeedKph);
        Assert.Equal(120, current.HeadingDegrees);
    }
}
