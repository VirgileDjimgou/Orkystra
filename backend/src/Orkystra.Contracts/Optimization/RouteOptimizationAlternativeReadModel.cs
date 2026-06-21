namespace Orkystra.Contracts.Optimization;

public sealed record RouteOptimizationAlternativeReadModel(
    string Label,
    IReadOnlyCollection<string> OrderedStopReferences,
    decimal ObjectiveScore,
    string Summary);
