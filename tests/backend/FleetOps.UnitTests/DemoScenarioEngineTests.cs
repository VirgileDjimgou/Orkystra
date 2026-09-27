using FleetOps.Core.Modules.Demo;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class DemoScenarioEngineTests
{
    [Fact]
    public void StartPauseContinueAndResetAreDeterministic()
    {
        var initial = new DateTimeOffset(2026, 9, 26, 8, 0, 0, TimeSpan.Zero);
        var clock = new DeterministicDemoClock(initial);
        var engine = new DeterministicDemoScenarioEngine(clock);

        engine.Start(DemoScenarioKind.LateDelivery, 42, 4);
        engine.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(initial.AddMinutes(20), engine.State.LogicalUtc);

        engine.Pause();
        engine.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(initial.AddMinutes(20), engine.State.LogicalUtc);

        engine.ContinueScenario();
        engine.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(initial.AddMinutes(24), engine.State.LogicalUtc);

        engine.Reset();
        Assert.Equal(DemoScenarioStatus.Stopped, engine.State.Status);
        Assert.Equal(initial, engine.State.LogicalUtc);
    }

    [Fact]
    public void StartRejectsUnboundedSpeed()
    {
        var engine = new DeterministicDemoScenarioEngine(new DeterministicDemoClock(DateTimeOffset.UnixEpoch));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Start(DemoScenarioKind.NormalShift, 1, 21));
    }

    [Fact]
    public void RouteInterpolationStaysWithinFixtureEndpoints()
    {
        var route = new DemoRoute("normal-shift", [new(48.49, 9.18), new(48.50, 9.20), new(48.51, 9.22)]);
        Assert.Equal(new DemoRoutePoint(48.49, 9.18), route.Interpolate(-1));
        Assert.Equal(new DemoRoutePoint(48.51, 9.22), route.Interpolate(2));
        Assert.Equal(new DemoRoutePoint(48.50, 9.20), route.Interpolate(0.5));
    }
}
