using FleetOps.Core.Modules.Demo;
using Microsoft.Extensions.Options;

namespace FleetOps.Worker.Demo;

public sealed partial class DemoScenarioHostedService(
    IDemoScenarioEngine engine,
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

        var tick = TimeSpan.FromSeconds(Math.Clamp(options.Value.TickSeconds, 1, 60));
        engine.Start(DemoScenarioKind.NormalShift, seed: 2701);
        while (!stoppingToken.IsCancellationRequested)
        {
            engine.Advance(tick);
            Log.ScenarioTick(logger, engine.State.Scenario, engine.State.LogicalUtc);
            await Task.Delay(tick, stoppingToken);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 2701, Level = LogLevel.Information, Message = "Demo scenario engine is disabled.")]
        public static partial void EngineDisabled(ILogger logger);

        [LoggerMessage(EventId = 2702, Level = LogLevel.Debug, Message = "Demo scenario tick {Scenario} at {LogicalUtc}.")]
        public static partial void ScenarioTick(ILogger logger, DemoScenarioKind scenario, DateTimeOffset logicalUtc);
    }
}
