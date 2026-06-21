using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Common;
using Orkystra.Domain.Identities;
using Orkystra.Domain.Transport.Events;
using Orkystra.Domain.ValueObjects;

namespace Orkystra.Domain.Transport;

public sealed class Route : AggregateRoot<RouteId>
{
    private readonly List<Stop> _stops = [];
    private readonly List<Shipment> _shipments = [];
    private readonly List<Delivery> _deliveries = [];

    private Route(RouteId id, Truck truck, string reference)
        : base(id)
    {
        Truck = truck;
        Reference = reference;
    }

    public string Reference { get; }

    public Truck Truck { get; }

    public Driver? Driver { get; private set; }

    public RouteStatus Status { get; private set; } = RouteStatus.Planned;

    public IReadOnlyCollection<Stop> Stops => _stops.AsReadOnly();

    public IReadOnlyCollection<Shipment> Shipments => _shipments.AsReadOnly();

    public IReadOnlyCollection<Delivery> Deliveries => _deliveries.AsReadOnly();

    public static Result<Route> Create(RouteId routeId, TruckId truckId, string truckReference, Weight capacity, string routeReference)
    {
        if (string.IsNullOrWhiteSpace(truckReference))
        {
            return Result.Failure<Route>(DomainErrors.Required(nameof(truckReference)));
        }

        if (string.IsNullOrWhiteSpace(routeReference))
        {
            return Result.Failure<Route>(DomainErrors.Required(nameof(routeReference)));
        }

        var truck = new Truck(truckId, truckReference.Trim(), capacity);
        var route = new Route(routeId, truck, routeReference.Trim());
        route.RaiseDomainEvent(new RouteCreated(route.Id, route.Truck.Id, route.Reference));

        return Result.Success(route);
    }

    public Result AssignDriver(DriverId driverId, string driverName)
    {
        if (string.IsNullOrWhiteSpace(driverName))
        {
            return Result.Failure(DomainErrors.Required(nameof(driverName)));
        }

        if (Driver is not null)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(driverId), "driver is already assigned"));
        }

        Driver = new Driver(driverId, driverName.Trim());
        Truck.AssignDriver(driverId);
        RaiseDomainEvent(new DriverAssigned(Id, Truck.Id, driverId));

        return Result.Success();
    }

    public Result AddStop(StopId stopId, int sequence, string name, GeoCoordinate coordinate, TimeWindow timeWindow)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(DomainErrors.Required(nameof(name)));
        }

        if (sequence <= 0)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(sequence), "sequence must be greater than zero"));
        }

        if (_stops.Any(stop => stop.Sequence == sequence))
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(sequence), "stop sequence must be unique within a route"));
        }

        _stops.Add(new Stop(stopId, sequence, name.Trim(), coordinate, timeWindow));

        return Result.Success();
    }

    public Result AddShipment(ShipmentId shipmentId, string reference, Weight loadWeight, StopId stopId, OrderId? orderId = null)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return Result.Failure(DomainErrors.Required(nameof(reference)));
        }

        if (_shipments.Any(shipment => shipment.Id == shipmentId))
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(shipmentId), "shipment already exists on this route"));
        }

        if (_stops.All(stop => stop.Id != stopId))
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(stopId), "delivery stop was not found"));
        }

        var projectedLoad = _shipments.Sum(shipment => shipment.LoadWeight.Kilograms) + loadWeight.Kilograms;
        if (projectedLoad > Truck.Capacity.Kilograms)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(loadWeight), "assigned load exceeds truck capacity"));
        }

        var shipment = new Shipment(shipmentId, reference.Trim(), loadWeight, orderId);
        shipment.AssignToRoute();
        _shipments.Add(shipment);

        var delivery = new Delivery(DeliveryId.New(), shipmentId, stopId);
        _deliveries.Add(delivery);
        RaiseDomainEvent(new ShipmentAssigned(Id, shipment.Id));

        return Result.Success();
    }

    public Result LoadShipment(ShipmentId shipmentId)
    {
        var shipment = FindShipment(shipmentId);
        if (shipment is null)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(shipmentId), "shipment was not found"));
        }

        if (shipment.Status != ShipmentStatus.Assigned)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(shipmentId), "shipment must be assigned before loading"));
        }

        shipment.Load();
        RaiseDomainEvent(new ShipmentLoaded(Id, shipment.Id));

        return Result.Success();
    }

    public Result Depart()
    {
        if (Driver is null)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(Driver), "route requires an assigned driver before departure"));
        }

        if (_stops.Count == 0)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(Stops), "route requires at least one stop before departure"));
        }

        if (_shipments.Count == 0)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(Shipments), "route requires at least one shipment before departure"));
        }

        if (_shipments.Any(shipment => shipment.Status != ShipmentStatus.Loaded))
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(Shipments), "all shipments must be loaded before departure"));
        }

        if (Status is RouteStatus.Departed or RouteStatus.Arrived or RouteStatus.Completed)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(Status), "route cannot depart from its current state"));
        }

        Status = RouteStatus.Departed;
        Truck.Depart();

        foreach (var shipment in _shipments)
        {
            shipment.Depart();
        }

        RaiseDomainEvent(new TruckDeparted(Id, Truck.Id));

        return Result.Success();
    }

    public Result ReportDelay(string reasonCode, int delayMinutes)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            return Result.Failure(DomainErrors.Required(nameof(reasonCode)));
        }

        if (delayMinutes <= 0)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(delayMinutes), "delay minutes must be greater than zero"));
        }

        if (Status is not RouteStatus.Departed and not RouteStatus.Delayed)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(Status), "route must be in transit before a delay can be reported"));
        }

        Status = RouteStatus.Delayed;
        Truck.Delay();
        RaiseDomainEvent(new TruckDelayed(Id, Truck.Id, reasonCode.Trim(), delayMinutes));

        return Result.Success();
    }

    public Result Arrive()
    {
        if (Status is not RouteStatus.Departed and not RouteStatus.Delayed)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(Status), "route must be in transit before arriving"));
        }

        Status = RouteStatus.Arrived;
        Truck.Arrive();

        foreach (var shipment in _shipments)
        {
            shipment.Arrive();
        }

        RaiseDomainEvent(new TruckArrived(Id, Truck.Id));

        return Result.Success();
    }

    public Result CompleteDelivery(DeliveryId deliveryId)
    {
        if (Status != RouteStatus.Arrived)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(Status), "route must arrive before deliveries can be completed"));
        }

        var delivery = _deliveries.SingleOrDefault(item => item.Id == deliveryId);
        if (delivery is null)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(deliveryId), "delivery was not found"));
        }

        if (delivery.IsCompleted)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(deliveryId), "delivery is already completed"));
        }

        var shipment = FindShipment(delivery.ShipmentId)!;
        delivery.Complete();
        shipment.Complete();
        RaiseDomainEvent(new DeliveryCompleted(Id, delivery.Id, shipment.Id));

        if (_deliveries.All(item => item.IsCompleted))
        {
            Status = RouteStatus.Completed;
        }

        return Result.Success();
    }

    public int TotalLoadKilograms => _shipments.Sum(shipment => (int)shipment.LoadWeight.Kilograms);

    private Shipment? FindShipment(ShipmentId shipmentId) =>
        _shipments.SingleOrDefault(shipment => shipment.Id == shipmentId);
}
