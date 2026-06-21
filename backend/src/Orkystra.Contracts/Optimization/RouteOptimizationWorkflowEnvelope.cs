namespace Orkystra.Contracts.Optimization;

public sealed record RouteOptimizationWorkflowEnvelope(
    RouteOptimizationResultReadModel Optimization,
    string Source,
    string? ErrorMessage);
