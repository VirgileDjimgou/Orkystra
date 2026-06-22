using Microsoft.Extensions.Options;
using Orkystra.Api.Persistence;
using Orkystra.Api.ControlTower;
using Orkystra.Contracts.Transport;

namespace Orkystra.Domain.Tests;

public sealed class TransportProjectionTests
{
    [Fact]
    public async Task TransportProjectionService_lists_route_projections()
    {
        var service = new TransportProjectionService();

        var routes = await service.ListAsync();

        Assert.Equal(3, routes.Count);
        Assert.Contains(routes, route => route.Reference == "RT-204" && route.Status == "On time");
        Assert.Contains(routes, route => route.Reference == "RT-412" && route.CompletedDeliveryCount == 3);
    }

    [Fact]
    public async Task TransportProjectionService_returns_detail_projection_for_known_route()
    {
        var service = new TransportProjectionService();

        var route = await service.GetByIdAsync(Guid.Parse("9f91e82e-226a-48f7-a94c-907b79431739"));

        Assert.NotNull(route);
        Assert.Equal("RT-412", route!.Reference);
        Assert.Equal("Delayed", route.Status);
        Assert.Equal(6, route.Stops.Count);
        Assert.Contains(route.Stops, stop => stop.Sequence == 6 && stop.Name == "West Flow Center");
        Assert.Contains(route.Shipments, shipment => shipment.Reference == "SHIP-412-06" && shipment.Status == "Completed");
        Assert.Contains(route.Deliveries, delivery => delivery.Reference == "DLV-412-05" && delivery.Status == "Pending");
    }

    [Fact]
    public async Task TransportProjectionService_returns_null_for_unknown_route()
    {
        var service = new TransportProjectionService();

        var route = await service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(route);
    }

    [Fact]
    public async Task TransportProjectionService_prefers_persisted_snapshot_when_available()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("orkystra-transport-projection-tests");

        try
        {
            var store = new OperationalPersistenceStore(
                Options.Create(new OperationalPersistenceOptions
                {
                    DatabasePath = Path.Combine("data", "operations.db")
                }),
                tempDirectory.FullName);

            var routeId = Guid.NewGuid();
            await store.UpsertProjectionAsync(
                "tenant-a",
                "route-detail",
                routeId.ToString("D"),
                "live",
                new RouteDetailReadModel(
                    routeId,
                    "SYNC-501",
                    Guid.NewGuid(),
                    "TRK-SYNC-1",
                    "Imported Driver",
                    "On time",
                    "In transit",
                    650m,
                    320m,
                    2,
                    1,
                    0,
                    DateTimeOffset.Parse("2026-06-22T12:00:00Z"),
                    [
                        new TransportRouteStopReadModel(1, "Sync Hub", "48.8000, 2.3000", "08:00-08:30"),
                        new TransportRouteStopReadModel(2, "Sync Store", "48.8200, 2.3400", "08:45-09:15")
                    ],
                    [
                        new TransportRouteShipmentReadModel("SYNC-SHIP-1", "Loaded", 100m, "SYNC-ORDER-1")
                    ],
                    [
                        new TransportRouteDeliveryReadModel("SYNC-DLV-1", 2, "Sync Store", "SYNC-SHIP-1", "Pending")
                    ]));

            var service = new TransportProjectionService(new Orkystra.Application.Connectors.Providers.RestTransportProvider(), store);

            var route = await service.GetByIdAsync(routeId, "tenant-a");

            Assert.NotNull(route);
            Assert.Equal("SYNC-501", route!.Reference);
            Assert.Equal("Imported Driver", route.DriverName);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }
}
