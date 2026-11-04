using FleetOps.Core.Modules.Demo;
using FleetOps.Core.Modules.Tracking;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class DemoMotionTests
{
    [Theory]
    [InlineData(DemoScenarioKind.NormalShift)]
    [InlineData(DemoScenarioKind.LateDelivery)]
    [InlineData(DemoScenarioKind.VehicleIssue)]
    [InlineData(DemoScenarioKind.DriverConnectivityLoss)]
    [InlineData(DemoScenarioKind.ComplianceWarning)]
    public void FullDayTraceHasBoundedDisplacementAccelerationAndStableDwell(DemoScenarioKind kind)
    {
        var scenario = new DemoScenarioCatalog().GetByKind(kind);
        foreach (var route in scenario.Routes)
        {
            var previous = DemoRouteMotion.Sample(route, 0, scenario.NominalSpeedKph);
            var visited = new HashSet<int>();
            for (var second = 1; second <= 86400; second++)
            {
                var current = DemoRouteMotion.Sample(route, second, scenario.NominalSpeedKph);
                var speed = TrackingGeometry.DistanceKm(previous.Position.Latitude, previous.Position.Longitude, current.Position.Latitude, current.Position.Longitude) * 3600;
                Assert.InRange(speed, 0, scenario.NominalSpeedKph + 0.1);
                Assert.InRange(current.SpeedKph, 0, scenario.NominalSpeedKph);
                Assert.InRange(Math.Abs(current.SpeedKph - previous.SpeedKph), 0, 2);
                Assert.InRange(current.HeadingDegrees, 0, 359.999999);
                if (current.IsDwelling)
                {
                    visited.Add(current.AnchorIndex);
                    Assert.Equal(route.Points[current.AnchorIndex], current.Position);
                    if (previous.IsDwelling && current.AnchorIndex == previous.AnchorIndex)
                        Assert.Equal(previous.HeadingDegrees, current.HeadingDegrees);
                }
                previous = current;
            }
            Assert.Equal(route.Points.Count - 1, visited.Count);
        }
    }

    [Fact]
    public async Task VariableCadenceAndSnapshotRestoreProduceIdenticalMotion()
    {
        var origin = DateTimeOffset.UtcNow;
        var engine = new DeterministicDemoScenarioEngine(new DeterministicDemoClock(origin));
        engine.Start(DemoScenarioKind.NormalShift, 34);
        engine.Advance(TimeSpan.FromSeconds(200));
        var snapshot = engine.Capture();
        var restored = new DeterministicDemoScenarioEngine(new DeterministicDemoClock(origin.AddDays(1)));
        restored.Restore(snapshot);
        var first = new Collector();
        var second = new Collector();
        var fleet = Enumerable.Range(1, 20).Select(i => new DemoVehicleBinding(Guid.NewGuid(), $"TEST-{i}")).ToList();
        var organization = Guid.NewGuid();
        await new DemoFleetSimulator(engine, new DemoScenarioCatalog(), first).TickAsync(organization, fleet, TimeSpan.FromSeconds(7), default);
        await new DemoFleetSimulator(restored, new DemoScenarioCatalog(), second).TickAsync(organization, fleet, TimeSpan.FromSeconds(7), default);
        Assert.Equal(first.Events, second.Events);
        var fresh = new DeterministicDemoScenarioEngine(new DeterministicDemoClock(origin));
        fresh.Start(DemoScenarioKind.NormalShift, 34);
        fresh.Advance(TimeSpan.FromSeconds(207));
        Assert.Equal(engine.State.MotionSeconds, fresh.State.MotionSeconds);
        Assert.Throws<ArgumentException>(() => restored.Restore(snapshot with { MotionVersion = 1 }));
    }

    private sealed class Collector : IDemoTelemetryEmitter
    {
        public List<DemoTelemetryEvent> Events { get; } = [];
        public Task EmitAsync(DemoTelemetryEvent telemetry, CancellationToken cancellationToken)
        {
            Events.Add(telemetry);
            return Task.CompletedTask;
        }
    }
}
