namespace Orkystra.Contracts.Ai;

public sealed record AiRecommendationEnvelope(
    AiRecommendationResponse Recommendation,
    string Source,
    string? ErrorMessage);
