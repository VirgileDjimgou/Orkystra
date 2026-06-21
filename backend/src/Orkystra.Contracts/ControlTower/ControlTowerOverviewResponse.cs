using Orkystra.Contracts.Simulation;
using Orkystra.Contracts.Transport;
using Orkystra.Contracts.Warehouse;

namespace Orkystra.Contracts.ControlTower;

public sealed record ControlTowerOverviewResponse(
    string TenantId,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyCollection<ScenarioSummaryReadModel> Scenarios,
    IReadOnlyCollection<WarehouseSummaryReadModel> Warehouses,
    IReadOnlyCollection<RouteSummaryReadModel> Routes,
    IReadOnlyCollection<OperationalAlertReadModel> Alerts,
    IReadOnlyCollection<EventFeedItemReadModel> EventFeed,
    IReadOnlyCollection<ProviderStatusReadModel> Providers);
