using Orkystra.Application.Connectors;
using Orkystra.Application.Connectors.Providers;
using Orkystra.Contracts.ControlTower;

namespace Orkystra.Api.ControlTower;

public sealed class ControlTowerOverviewService
{
    private readonly ProviderRegistry _providerRegistry;

    public ControlTowerOverviewService()
    {
        _providerRegistry = new ProviderRegistry(
        [
            new CsvWarehouseImportProvider(),
            new RestTransportProvider(),
            new GpsTelematicsProvider()
        ]);
    }

    public async ValueTask<ControlTowerOverviewResponse> BuildOverviewAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var warehouseProvider = _providerRegistry.ListByDomain(ProviderDomain.Warehouse)
            .OfType<IWarehouseProviderAdapter>()
            .First();
        var transportProvider = _providerRegistry.ListByDomain(ProviderDomain.Transport)
            .OfType<ITransportProviderAdapter>()
            .First();

        var warehouses = await warehouseProvider.ReadWarehousesAsync(cancellationToken);
        var routes = await transportProvider.ReadRoutesAsync(cancellationToken);
        var providers = await BuildProviderStatusesAsync(cancellationToken);

        IReadOnlyCollection<Orkystra.Contracts.Simulation.ScenarioSummaryReadModel> scenarios =
        [
            new Orkystra.Contracts.Simulation.ScenarioSummaryReadModel(
                Guid.Parse("9d4e8f09-cf15-48d8-90a6-e96c833fd741"),
                "Baseline day shift",
                42,
                "Running",
                DateTimeOffset.Parse("2026-06-20T10:15:00Z"),
                2),
            new Orkystra.Contracts.Simulation.ScenarioSummaryReadModel(
                Guid.Parse("4172df1e-3fb6-4d56-b04c-775b9fcd8620"),
                "Dock saturation stress",
                77,
                "Paused",
                DateTimeOffset.Parse("2026-06-20T11:05:00Z"),
                5),
            new Orkystra.Contracts.Simulation.ScenarioSummaryReadModel(
                Guid.Parse("0a59d24d-b1fc-45dd-9000-508862c4af53"),
                "Late carrier recovery",
                103,
                "Completed",
                DateTimeOffset.Parse("2026-06-20T13:40:00Z"),
                3)
        ];

        var alerts = BuildOperationalAlerts(warehouses, routes);
        var eventFeed = BuildEventFeed(routes, warehouses, providers);

        return new ControlTowerOverviewResponse(tenantId, generatedAtUtc, scenarios, warehouses, routes, alerts, eventFeed, providers);
    }

    private async ValueTask<IReadOnlyCollection<ProviderStatusReadModel>> BuildProviderStatusesAsync(CancellationToken cancellationToken)
    {
        var providers = _providerRegistry.ListAll();
        var providerStatuses = new List<ProviderStatusReadModel>(providers.Count);

        foreach (var provider in providers)
        {
            var health = await provider.GetHealthAsync(cancellationToken);
            var sync = await provider.GetSyncStatusAsync(cancellationToken);

            providerStatuses.Add(new ProviderStatusReadModel(
                provider.ProviderId,
                provider.ProviderName,
                provider.Domain.ToString(),
                health.Status.ToString(),
                sync.Status,
                sync.LastSuccessfulSyncAt,
                sync.LastAttemptedSyncAt,
                health.Summary));
        }

        return providerStatuses;
    }

    private static IReadOnlyCollection<OperationalAlertReadModel> BuildOperationalAlerts(
        IReadOnlyCollection<Orkystra.Contracts.Warehouse.WarehouseSummaryReadModel> warehouses,
        IReadOnlyCollection<Orkystra.Contracts.Transport.RouteSummaryReadModel> routes)
    {
        var alerts = new List<OperationalAlertReadModel>();

        var delayedRoute = routes.FirstOrDefault(route => string.Equals(route.Status, "Delayed", StringComparison.OrdinalIgnoreCase));
        if (delayedRoute is not null)
        {
            alerts.Add(new OperationalAlertReadModel(
                "Critical",
                "Cross-dock queue building",
                $"Carrier handoff is slipping beyond plan for route {delayedRoute.Reference} handled by {delayedRoute.TruckReference}."));
        }

        var busiestWarehouse = warehouses
            .OrderByDescending(warehouse => warehouse.SlotCount == 0 ? 0d : (double)warehouse.StoredPalletCount / warehouse.SlotCount)
            .FirstOrDefault();

        if (busiestWarehouse is not null)
        {
            var occupancy = busiestWarehouse.SlotCount == 0
                ? 0
                : (int)Math.Round((double)busiestWarehouse.StoredPalletCount / busiestWarehouse.SlotCount * 100);

            alerts.Add(new OperationalAlertReadModel(
                "Warning",
                "Ambient picking under pressure",
                $"{busiestWarehouse.Name} is operating at {occupancy}% slot occupancy and should be watched for congestion."));
        }

        alerts.Add(new OperationalAlertReadModel(
            "Info",
            "Scenario branch available",
            "The current simulation can fork from 10:15 UTC to test an alternate carrier assignment."));

        return alerts;
    }

    private static IReadOnlyCollection<EventFeedItemReadModel> BuildEventFeed(
        IReadOnlyCollection<Orkystra.Contracts.Transport.RouteSummaryReadModel> routes,
        IReadOnlyCollection<Orkystra.Contracts.Warehouse.WarehouseSummaryReadModel> warehouses,
        IReadOnlyCollection<ProviderStatusReadModel> providers)
    {
        var eventFeed = new List<EventFeedItemReadModel>
        {
            new("evt-1", "10:11", "ScenarioStarted", "Baseline day shift resumed with deterministic seed 42.")
        };

        foreach (var route in routes.Take(3))
        {
            eventFeed.Add(new EventFeedItemReadModel(
                $"route-{route.Reference.ToLowerInvariant()}",
                "10:13",
                "RouteProjectionUpdated",
                $"{route.Reference} for {route.TruckReference} is {route.Status} with {route.ShipmentCount} active shipments."));
        }

        var busiestWarehouse = warehouses.OrderByDescending(warehouse => warehouse.StoredPalletCount).FirstOrDefault();
        if (busiestWarehouse is not null)
        {
            eventFeed.Add(new EventFeedItemReadModel(
                "warehouse-topload",
                "10:14",
                "WarehouseProjectionUpdated",
                $"{busiestWarehouse.Name} is carrying {busiestWarehouse.StoredPalletCount} pallets across {busiestWarehouse.SlotCount} slots."));
        }

        var degradedProvider = providers.FirstOrDefault(provider => !string.Equals(provider.HealthStatus, "Healthy", StringComparison.OrdinalIgnoreCase));
        if (degradedProvider is not null)
        {
            eventFeed.Add(new EventFeedItemReadModel(
                $"provider-{degradedProvider.ProviderId}",
                "10:15",
                "ProviderHealthObserved",
                $"{degradedProvider.ProviderName} is reporting {degradedProvider.HealthStatus.ToLowerInvariant()} health with sync state {degradedProvider.SyncStatus}."));
        }

        return eventFeed;
    }
}
