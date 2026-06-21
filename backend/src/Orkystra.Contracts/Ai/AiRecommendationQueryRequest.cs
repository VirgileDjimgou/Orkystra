namespace Orkystra.Contracts.Ai;

public sealed record AiRecommendationQueryRequest(
    string Question,
    string? ScenarioId);
