using FleetOps.Core.Modules.Demo;
using Microsoft.Extensions.Options;

namespace FleetOps.Worker.Demo;

public sealed partial class VirtualDriverHostedService(
    IDemoScenarioEngine engine,
    IAgentDecisionProvider decisions,
    IVirtualDriverTools tools,
    IAgentActivitySink activities,
    IOptions<DemoEngineOptions> options,
    ILogger<VirtualDriverHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled || settings.Agents.Count == 0) return;
        if (settings.Agents.Count > 20) throw new InvalidOperationException("At most 20 virtual drivers may run concurrently.");
        var agents = settings.Agents.Select(item => (IVirtualDriverAgent)new VirtualDriverAgent(
            new(item.AgentId, item.OrganizationId, item.DriverId, item.VehicleId, item.MissionId, item.StopId), decisions, tools, activities)).ToList();
        var interval = TimeSpan.FromSeconds(Math.Clamp(settings.TickSeconds, 1, 60));
        while (!stoppingToken.IsCancellationRequested)
        {
            if (engine.State.Status == DemoScenarioStatus.Running)
            {
                var results = await VirtualDriverAgentCoordinator.StepAsync(agents, engine.State.Scenario, engine.State.Tick, engine.State.LogicalUtc, stoppingToken);
                Log.AgentBatch(logger, results.Length, engine.State.Tick);
            }
            await Task.Delay(interval, stoppingToken);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 2801, Level = LogLevel.Information, Message = "Advanced {AgentCount} virtual drivers at Demo tick {Tick}.")]
        public static partial void AgentBatch(ILogger logger, int agentCount, long tick);
    }
}
