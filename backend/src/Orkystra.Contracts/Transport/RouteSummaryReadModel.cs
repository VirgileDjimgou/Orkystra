namespace Orkystra.Contracts.Transport;

public sealed record RouteSummaryReadModel(
    Guid RouteId,
    string Reference,
    Guid TruckId,
    string TruckReference,
    string Status,
    int StopCount,
    int ShipmentCount,
    int CompletedDeliveryCount);
