namespace Orkystra.Contracts.Ai;

public sealed record AiRecommendedActionReadModel(
    string Title,
    string Rationale,
    string Priority);
