using Orkystra.Domain.Identities;
using Orkystra.Domain.Transport;
using Orkystra.Domain.Transport.Events;
using Orkystra.Domain.ValueObjects;
using RouteAggregate = Orkystra.Domain.Transport.Route;

namespace Orkystra.Domain.Tests;

public sealed class TransportTests
{
    [Fact]
    public void RouteCreate_RaisesRouteCreatedEvent()
    {
        var result = RouteAggregate.Create(RouteId.New(), TruckId.New(), "TRUCK-01", Weight.Create(500m).Value, "ROUTE-01");

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value.DomainEvents, domainEvent => domainEvent is RouteCreated);
    }

    [Fact]
    public void AssignDriver_AddStop_AddShipment_AndLoad_Succeeds()
    {
        var route = CreateRoute();
        var stopId = StopId.New();
        var shipmentId = ShipmentId.New();

        var assignDriver = route.AssignDriver(DriverId.New(), "Alex Driver");
        var addStop = route.AddStop(stopId, 1, "Customer A", GeoCoordinate.Create(48.8566m, 2.3522m).Value, CreateTimeWindow(8, 10));
        var addShipment = route.AddShipment(shipmentId, "SHIP-01", Weight.Create(120m).Value, stopId, OrderId.New());
        var loadShipment = route.LoadShipment(shipmentId);

        Assert.True(assignDriver.IsSuccess);
        Assert.True(addStop.IsSuccess);
        Assert.True(addShipment.IsSuccess);
        Assert.True(loadShipment.IsSuccess);
        Assert.Contains(route.DomainEvents, domainEvent => domainEvent is ShipmentAssigned assigned && assigned.ShipmentId == shipmentId);
        Assert.Contains(route.DomainEvents, domainEvent => domainEvent is ShipmentLoaded loaded && loaded.ShipmentId == shipmentId);
    }

    [Fact]
    public void AddShipment_FailsWhenTruckCapacityWouldBeExceeded()
    {
        var route = CreateRoute(capacityKilograms: 100m);
        var stopId = StopId.New();
        route.AddStop(stopId, 1, "Customer A", GeoCoordinate.Create(48m, 2m).Value, CreateTimeWindow(8, 10));

        var result = route.AddShipment(ShipmentId.New(), "SHIP-HEAVY", Weight.Create(150m).Value, stopId);

        Assert.True(result.IsFailure);
        Assert.Contains("capacity", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Depart_FailsWithoutAssignedDriver()
    {
        var route = CreateRoute();
        var stopId = StopId.New();
        var shipmentId = ShipmentId.New();

        route.AddStop(stopId, 1, "Customer A", GeoCoordinate.Create(48m, 2m).Value, CreateTimeWindow(8, 10));
        route.AddShipment(shipmentId, "SHIP-01", Weight.Create(40m).Value, stopId);
        route.LoadShipment(shipmentId);

        var result = route.Depart();

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Depart_FailsWhenNotAllShipmentsAreLoaded()
    {
        var route = CreateConfiguredRoute(loadShipments: false);

        var result = route.Depart();

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Depart_ThenDelay_ThenArrive_Succeeds()
    {
        var route = CreateConfiguredRoute();

        var depart = route.Depart();
        var delay = route.ReportDelay("traffic", 15);
        var arrive = route.Arrive();

        Assert.True(depart.IsSuccess);
        Assert.True(delay.IsSuccess);
        Assert.True(arrive.IsSuccess);
        Assert.Equal(RouteStatus.Arrived, route.Status);
        Assert.Contains(route.DomainEvents, domainEvent => domainEvent is TruckDeparted);
        Assert.Contains(route.DomainEvents, domainEvent => domainEvent is TruckDelayed delayed && delayed.DelayMinutes == 15);
        Assert.Contains(route.DomainEvents, domainEvent => domainEvent is TruckArrived);
    }

    [Fact]
    public void ReportDelay_FailsBeforeDeparture()
    {
        var route = CreateConfiguredRoute();

        var result = route.ReportDelay("traffic", 10);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void CompleteDelivery_CompletesRouteWhenAllDeliveriesAreDone()
    {
        var route = CreateConfiguredRoute();
        route.Depart();
        route.Arrive();
        var deliveryId = route.Deliveries.Single().Id;

        var result = route.CompleteDelivery(deliveryId);

        Assert.True(result.IsSuccess);
        Assert.Equal(RouteStatus.Completed, route.Status);
        Assert.Contains(route.DomainEvents, domainEvent => domainEvent is DeliveryCompleted completed && completed.DeliveryId == deliveryId);
    }

    private static RouteAggregate CreateRoute(decimal capacityKilograms = 500m)
    {
        return RouteAggregate.Create(RouteId.New(), TruckId.New(), "TRUCK-01", Weight.Create(capacityKilograms).Value, "ROUTE-01").Value;
    }

    private static RouteAggregate CreateConfiguredRoute(bool loadShipments = true)
    {
        var route = CreateRoute();
        var stopId = StopId.New();
        var shipmentId = ShipmentId.New();

        route.AssignDriver(DriverId.New(), "Alex Driver");
        route.AddStop(stopId, 1, "Customer A", GeoCoordinate.Create(48.8566m, 2.3522m).Value, CreateTimeWindow(8, 10));
        route.AddShipment(shipmentId, "SHIP-01", Weight.Create(120m).Value, stopId);

        if (loadShipments)
        {
            route.LoadShipment(shipmentId);
        }

        return route;
    }

    private static TimeWindow CreateTimeWindow(int startHour, int endHour)
    {
        var start = new DateTimeOffset(2026, 6, 20, startHour, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 6, 20, endHour, 0, 0, TimeSpan.Zero);
        return TimeWindow.Create(start, end).Value;
    }
}
