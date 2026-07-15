using FleetOps.Core.Modules.Tracking;

namespace FleetOps.UnitTests;

public sealed class TelemetryPointTests
{
    [Fact]
    public void Constructor_rejects_invalid_latitude()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TelemetryPoint(
            Guid.NewGuid(), Guid.NewGuid(), "device", DateTimeOffset.UtcNow, 91, 7.5, 10));
    }
}
