namespace Orkystra.Contracts.Optimization;

public sealed record RouteOptimizationResultReadModel(
    Guid RouteId,
    string RouteReference,
    string Status,
    decimal? ObjectiveScore,
    IReadOnlyCollection<string> OrderedStopReferences,
    IReadOnlyDictionary<string, int> EtaMinutes,
    IReadOnlyDictionary<string, int> LoadDistribution,
    IReadOnlyCollection<string> ConstraintViolations,
    RouteOptimizationExplanationReadModel Explanation,
    IReadOnlyCollection<RouteOptimizationAlternativeReadModel> Alternatives,
    string SolverBackend);
