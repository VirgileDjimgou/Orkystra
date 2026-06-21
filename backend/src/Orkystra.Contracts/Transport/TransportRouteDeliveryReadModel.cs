namespace Orkystra.Contracts.Transport;

public sealed record TransportRouteDeliveryReadModel(
    string Reference,
    int StopSequence,
    string StopName,
    string ShipmentReference,
    string Status);
