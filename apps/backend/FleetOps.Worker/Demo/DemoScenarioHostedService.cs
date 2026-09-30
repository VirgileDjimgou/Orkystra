using System.Diagnostics;
using FleetOps.Core.Modules.Demo;
using FleetOps.Core.Observability;
using Microsoft.Extensions.Options;

namespace FleetOps.Worker.Demo;

public sealed partial class DemoScenarioHostedService(
    IDemoScenarioEngine engine,
    IDemoScenarioRepository scenarios,
    IDemoTelemetryEmitter telemetryEmitter,
    IDemoFleetSource fleetSource,
    IDemoScenarioStateStore stateStore,
    IOptions<DemoEngineOptions> options,
    ILogger<DemoScenarioHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            Log.EngineDisabled(logger);
            return;
        }

        var settings = options.Value;
        ValidateConfiguration(settings);
        var tick = TimeSpan.FromSeconds(Math.Clamp(settings.TickSeconds, 1, 60));
        var fleet = await fleetSource.LoadAsync(stoppingToken);
        var vehicles = fleet.Vehicles;
        if (vehicles.Count is < 10 or > 20)
            throw new InvalidOperationException("Hosted Demo fleet must contain 10 to 20 vehicles.");
        DemoFleetSimulator.ValidateBindings(fleet.OrganizationId, vehicles);
        var simulator = new DemoFleetSimulator(engine, scenarios, telemetryEmitter);
        var snapshot = await stateStore.LoadAsync(stoppingToken);
        if (snapshot is null)
        {
            engine.Start(ParseScenario(settings.Scenario), settings.Seed, settings.SpeedMultiplier);
            Log.ScenarioStarted(logger, engine.State.Scenario, vehicles.Count, engine.State.Seed);
        }
        else
        {
            engine.Restore(snapshot);
            Log.ScenarioRestored(logger, engine.State.Scenario, engine.State.Tick);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var scenarioTag = new KeyValuePair<string, object?>("scenario", engine.State.Scenario.ToString());
            var tickStopwatch = Stopwatch.StartNew();
            var emitted = await simulator.TickAsync(fleet.OrganizationId, vehicles, tick, stoppingToken);
            FleetOpsMetrics.DemoEngineTickDuration.Record(tickStopwatch.Elapsed.TotalMilliseconds, scenarioTag);
            FleetOpsMetrics.DemoEngineTelemetryEmitted.Add(emitted, scenarioTag);

            var saveStopwatch = Stopwatch.StartNew();
            await stateStore.SaveAsync(engine.Capture(), stoppingToken);
            FleetOpsMetrics.DemoEngineStateSaveDuration.Record(saveStopwatch.Elapsed.TotalMilliseconds);

            Log.ScenarioTick(logger, engine.State.Scenario, engine.State.LogicalUtc, emitted);
            await Task.Delay(tick, stoppingToken);
        }
    }

    private static void ValidateConfiguration(DemoEngineOptions settings)
    {
        if (!string.Equals(settings.RuntimeMode, "Demo", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Demo engine activation requires DemoEngine:RuntimeMode=Demo.");
        if (!settings.SideEffectsSandboxed)
            throw new InvalidOperationException("Demo engine activation requires side-effect sandboxing.");
        if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl) || !Uri.TryCreate(settings.ApiBaseUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException("DemoEngine:ApiBaseUrl must be an absolute URL.");
        if (settings.SpeedMultiplier is < 0.25 or > 20)
            throw new InvalidOperationException("DemoEngine:SpeedMultiplier must be between 0.25 and 20.");
    }

    private static DemoScenarioKind ParseScenario(string value) => value.Trim().ToUpperInvariant() switch
    {
        "NORMAL_SHIFT" => DemoScenarioKind.NormalShift,
        "LATE_DELIVERY" => DemoScenarioKind.LateDelivery,
        "VEHICLE_ISSUE" => DemoScenarioKind.VehicleIssue,
        "DRIVER_CONNECTIVITY_LOSS" => DemoScenarioKind.DriverConnectivityLoss,
        "COMPLIANCE_WARNING" => DemoScenarioKind.ComplianceWarning,
        _ => throw new InvalidOperationException($"Unknown Demo scenario '{value}'.")
    };

    private static partial class Log
    {
        [LoggerMessage(EventId = 2701, Level = LogLevel.Information, Message = "Demo scenario engine is disabled.")]
        public static partial void EngineDisabled(ILogger logger);

        [LoggerMessage(EventId = 2702, Level = LogLevel.Debug, Message = "Demo scenario tick {Scenario} at {LogicalUtc}; emitted {Emitted} telemetry events.")]
        public static partial void ScenarioTick(ILogger logger, DemoScenarioKind scenario, DateTimeOffset logicalUtc, int emitted);

        [LoggerMessage(EventId = 2703, Level = LogLevel.Information, Message = "Started Demo scenario {Scenario} with {VehicleCount} vehicles and seed {Seed}.")]
        public static partial void ScenarioStarted(ILogger logger, DemoScenarioKind scenario, int vehicleCount, int seed);

        [LoggerMessage(EventId = 2704, Level = LogLevel.Information, Message = "Restored Demo scenario {Scenario} at tick {Tick}.")]
        public static partial void ScenarioRestored(ILogger logger, DemoScenarioKind scenario, long tick);
    }
}
