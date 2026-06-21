namespace Orkystra.Contracts.Transport;

public sealed record TransportRouteShipmentReadModel(
    string Reference,
    string Status,
    decimal LoadWeightKilograms,
    string OrderReference);
