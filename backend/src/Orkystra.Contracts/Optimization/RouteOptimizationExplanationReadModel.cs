namespace Orkystra.Contracts.Optimization;

public sealed record RouteOptimizationExplanationReadModel(
    string SelectedVehicleReason,
    string PrioritizationReason,
    IReadOnlyCollection<string> TightConstraints,
    string? InfeasibilityReason,
    IReadOnlyCollection<string> TradeOffs);
