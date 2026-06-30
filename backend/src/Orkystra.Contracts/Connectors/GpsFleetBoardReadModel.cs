namespace Orkystra.Contracts.Connectors;

public sealed record GpsFleetBoardReadModel(
    DateTimeOffset GeneratedAtUtc,
    int PositionCount,
    int RouteLinkedCount,
    int FreshCount,
    int AgingCount,
    int StaleCount,
    int MovingCount,
    int IdleCount,
    int SpeedingCount,
    string Summary,
    string? FocusTruckReference,
    string? FocusRouteReference,
    string FocusSummary,
    IReadOnlyCollection<GpsFleetPositionReadModel> Positions);
