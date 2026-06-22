namespace Orkystra.Contracts.Transport;

public sealed record TransportSyncRouteDiffItemReadModel(
    string RouteReference,
    Guid? PreviousRouteId,
    Guid? CurrentRouteId,
    string ChangeType,
    string? PreviousStatus,
    string? CurrentStatus,
    int? PreviousStopCount,
    int? CurrentStopCount,
    int? PreviousShipmentCount,
    int? CurrentShipmentCount,
    int? PreviousCompletedDeliveryCount,
    int? CurrentCompletedDeliveryCount,
    string Summary);
