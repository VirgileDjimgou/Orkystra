namespace Orkystra.Contracts.Transport;

public sealed record TransportRouteStopReadModel(
    int Sequence,
    string Name,
    string CoordinateLabel,
    string TimeWindowLabel);
