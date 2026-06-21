using Orkystra.Api.ControlTower;

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
}
