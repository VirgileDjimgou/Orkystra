namespace Orkystra.Contracts.Transport;

public sealed record RouteDetailReadModel(
    Guid RouteId,
    string Reference,
    Guid TruckId,
    string TruckReference,
    string DriverName,
    string Status,
    string TruckStatus,
    decimal TruckCapacityKilograms,
    decimal TotalLoadKilograms,
    int StopCount,
    int ShipmentCount,
    int CompletedDeliveryCount,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyCollection<TransportRouteStopReadModel> Stops,
    IReadOnlyCollection<TransportRouteShipmentReadModel> Shipments,
    IReadOnlyCollection<TransportRouteDeliveryReadModel> Deliveries);
