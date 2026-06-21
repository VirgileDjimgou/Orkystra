namespace Orkystra.Contracts.Ai;

public sealed record AiRecommendationResponse(
    string Intent,
    string DirectAnswer,
    IReadOnlyCollection<AiEvidenceReadModel> Evidence,
    IReadOnlyCollection<string> Assumptions,
    IReadOnlyCollection<AiRecommendedActionReadModel> RecommendedActions,
    string ConfidenceLevel,
    string? AlternativeScenarioNote,
    IReadOnlyCollection<string> MissingData,
    IReadOnlyCollection<string> SpecialistAgents);
