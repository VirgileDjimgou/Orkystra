extern alias worker;

using System.Collections.Concurrent;
using FleetOps.Core.Modules.Demo;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;
using DemoEngineOptions = worker::FleetOps.Worker.Demo.DemoEngineOptions;
using FileDemoScenarioStateStore = worker::FleetOps.Worker.Demo.FileDemoScenarioStateStore;
using IHostEnvironment = Microsoft.Extensions.Hosting.IHostEnvironment;

namespace FleetOps.UnitTests;

[Trait("Category", "Reliability")]
public sealed class DemoScenarioRestartReliabilityTests
{
    [Fact]
    public async Task WorkerRestartResumesDeterministicallyFromPersistedSnapshot()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fleetops-demo-state-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var options = Options.Create(new DemoEngineOptions { StatePath = "demo-state.json", Enabled = true, RuntimeMode = "Demo" });
            var store = new FileDemoScenarioStateStore(options, new StubHostEnvironment(directory));
            var organizationId = Guid.NewGuid();
            var bindings = Enumerable.Range(0, 12)
                .Select(index => new DemoVehicleBinding(Guid.NewGuid(), $"DEV-{index:D2}"))
                .ToList();
            var catalog = new DemoScenarioCatalog();
            var initialUtc = new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

            var firstEngine = new DeterministicDemoScenarioEngine(new DeterministicDemoClock(initialUtc));
            firstEngine.Start(DemoScenarioKind.NormalShift, 3101);
            var firstEmitter = new RecordingEmitter();
            var firstSimulator = new DemoFleetSimulator(firstEngine, catalog, firstEmitter);
            for (var tick = 0; tick < 5; tick++)
            {
                await firstSimulator.TickAsync(organizationId, bindings, TimeSpan.FromSeconds(5), CancellationToken.None);
            }

            await store.SaveAsync(firstEngine.Capture(), CancellationToken.None);
            Assert.Equal(5 * bindings.Count, firstEmitter.Events.Count);

            var snapshot = await store.LoadAsync(CancellationToken.None);
            Assert.NotNull(snapshot);

            var restartedEngine = new DeterministicDemoScenarioEngine(new DeterministicDemoClock(initialUtc.AddHours(1)));
            restartedEngine.Restore(snapshot!);
            var restartedEmitter = new RecordingEmitter();
            var restartedSimulator = new DemoFleetSimulator(restartedEngine, catalog, restartedEmitter);
            await restartedSimulator.TickAsync(organizationId, bindings, TimeSpan.FromSeconds(5), CancellationToken.None);

            Assert.Equal(6, restartedEngine.State.Tick);
            Assert.Equal(bindings.Count, restartedEmitter.Events.Count);
            Assert.All(restartedEmitter.Events, telemetry => Assert.EndsWith($"-{6:D10}", telemetry.EventId));
            Assert.Empty(restartedEmitter.Events.Select(x => x.EventId).Intersect(firstEmitter.Events.Select(x => x.EventId)));

            // Replaying the same persisted snapshot emits the exact same deterministic identifiers.
            var replayEngine = new DeterministicDemoScenarioEngine(new DeterministicDemoClock(initialUtc));
            replayEngine.Restore(snapshot!);
            var replayEmitter = new RecordingEmitter();
            var replaySimulator = new DemoFleetSimulator(replayEngine, catalog, replayEmitter);
            await replaySimulator.TickAsync(organizationId, bindings, TimeSpan.FromSeconds(5), CancellationToken.None);
            Assert.Equal(restartedEmitter.Events.Select(x => x.EventId), replayEmitter.Events.Select(x => x.EventId));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class RecordingEmitter : IDemoTelemetryEmitter
    {
        public ConcurrentQueue<DemoTelemetryEvent> Events { get; } = new();

        public Task EmitAsync(DemoTelemetryEvent telemetry, CancellationToken cancellationToken)
        {
            Events.Enqueue(telemetry);
            return Task.CompletedTask;
        }
    }

    private sealed class StubHostEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "FleetOps.Tests";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRoot);
    }
}
