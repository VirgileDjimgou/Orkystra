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

    [Fact]
    public void CatalogContainsAllNamedScenariosAndCredibleRoutes()
    {
        var catalog = new DemoScenarioCatalog();

        Assert.Equal(5, catalog.List().Count);
        Assert.Equal(
            ["NORMAL_SHIFT", "LATE_DELIVERY", "VEHICLE_ISSUE", "DRIVER_CONNECTIVITY_LOSS", "COMPLIANCE_WARNING"],
            catalog.List().Select(scenario => scenario.Code));
        Assert.All(catalog.List(), scenario =>
        {
            Assert.True(scenario.Routes.Count >= 4);
            Assert.All(scenario.Routes, route => Assert.True(route.Points.Count >= 4));
        });
    }

    [Fact]
    public async Task TwelveVehicleReplayIsDeterministicAndBounded()
    {
        var first = await RunReplayAsync(seed: 2701);
        var second = await RunReplayAsync(seed: 2701);

        Assert.Equal(120, first.Count);
        Assert.Equal(first, second);
        Assert.Equal(12, first.Select(item => item.VehicleId).Distinct().Count());
        Assert.All(first, item =>
        {
            Assert.InRange(item.Latitude, 48.68, 48.84);
            Assert.InRange(item.Longitude, 9.09, 9.29);
        });
    }

    [Fact]
    public async Task RestoredSnapshotContinuesAtTheNextDeterministicTick()
    {
        var initial = new DateTimeOffset(2026, 9, 27, 8, 0, 0, TimeSpan.Zero);
        var beforeRestart = new DeterministicDemoScenarioEngine(new DeterministicDemoClock(initial));
        var emitter = new RecordingEmitter();
        var vehicles = CreateVehicles(10);
        var simulator = new DemoFleetSimulator(beforeRestart, new DemoScenarioCatalog(), emitter);
        beforeRestart.Start(DemoScenarioKind.NormalShift, 44);
        await simulator.TickAsync(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), vehicles, TimeSpan.FromSeconds(5), default);

        var restored = new DeterministicDemoScenarioEngine(new DeterministicDemoClock(initial.AddDays(1)));
        restored.Restore(beforeRestart.Capture());
        var restoredEmitter = new RecordingEmitter();
        var restoredSimulator = new DemoFleetSimulator(restored, new DemoScenarioCatalog(), restoredEmitter);
        await restoredSimulator.TickAsync(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), vehicles, TimeSpan.FromSeconds(5), default);

        Assert.Equal(2, restored.State.Tick);
        Assert.All(restoredEmitter.Events, item => Assert.EndsWith("0000000002", item.EventId, StringComparison.Ordinal));
        restored.Reset();
        Assert.Equal(initial, restored.State.LogicalUtc);
    }

    [Fact]
    public async Task RejectsMissingTenantAndDuplicateVehicleBindings()
    {
        var engine = new DeterministicDemoScenarioEngine(new DeterministicDemoClock(DateTimeOffset.UnixEpoch));
        engine.Start(DemoScenarioKind.NormalShift, 1);
        var simulator = new DemoFleetSimulator(engine, new DemoScenarioCatalog(), new RecordingEmitter());
        var duplicate = new DemoVehicleBinding(Guid.NewGuid(), "same-device");

        await Assert.ThrowsAsync<ArgumentException>(() => simulator.TickAsync(Guid.Empty, [duplicate], TimeSpan.FromSeconds(1), default));
        await Assert.ThrowsAsync<ArgumentException>(() => simulator.TickAsync(Guid.NewGuid(), [duplicate, duplicate], TimeSpan.FromSeconds(1), default));
    }

    private static async Task<List<DemoTelemetryEvent>> RunReplayAsync(int seed)
    {
        var engine = new DeterministicDemoScenarioEngine(new DeterministicDemoClock(new DateTimeOffset(2026, 9, 27, 8, 0, 0, TimeSpan.Zero)));
        var emitter = new RecordingEmitter();
        var simulator = new DemoFleetSimulator(engine, new DemoScenarioCatalog(), emitter);
        engine.Start(DemoScenarioKind.NormalShift, seed, 4);
        for (var tick = 0; tick < 10; tick++)
            await simulator.TickAsync(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), CreateVehicles(12), TimeSpan.FromSeconds(5), default);
        return emitter.Events;
    }

    private static List<DemoVehicleBinding> CreateVehicles(int count) => Enumerable.Range(1, count)
        .Select(index => new DemoVehicleBinding(Guid.Parse($"00000000-0000-0000-0000-{index:D12}"), $"DEMO-GPS-{index:D2}"))
        .ToList();

    private sealed class RecordingEmitter : IDemoTelemetryEmitter
    {
        public List<DemoTelemetryEvent> Events { get; } = [];
        public Task EmitAsync(DemoTelemetryEvent telemetry, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add(telemetry);
            return Task.CompletedTask;
        }
    }
}
