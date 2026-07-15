using FleetOps.Core.Modules.Tracking;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class TelemetryPointTests
{
    [Fact]
    public void ConstructorRejectsInvalidLatitude()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TelemetryPoint(
            Guid.NewGuid(), Guid.NewGuid(), "device", DateTimeOffset.UtcNow, 91, 7.5, 10));
    }
}
