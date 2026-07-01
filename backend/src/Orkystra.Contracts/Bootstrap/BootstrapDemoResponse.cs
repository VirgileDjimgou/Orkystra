using Orkystra.Contracts.Simulation;

namespace Orkystra.Contracts.Bootstrap;

public sealed record BootstrapDemoResponse(
    PublishScenarioEventsResponse Scenario,
    int WarehouseCount,
    int RouteCount,
    int GpsPositionCount,
    DateTimeOffset BootstrappedAtUtc);
