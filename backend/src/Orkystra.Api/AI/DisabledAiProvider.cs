using Orkystra.Contracts.Ai;
using Orkystra.Contracts.ControlTower;

namespace Orkystra.Api.AI;

public sealed class DisabledAiProvider : IAiProvider
{
    public string Name => "disabled";

    public Task<AiRecommendationEnvelope> BuildRecommendationAsync(
        string tenantId,
        AiRecommendationQueryRequest request,
        ControlTowerOverviewResponse overview,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new AiRecommendationEnvelope(
            new AiRecommendationResponse(
                "unknown",
                "The AI provider is disabled. Set AiService__Provider to 'http' or 'local' in configuration to enable recommendations.",
                [],
                ["AI has been explicitly disabled through the AiService:Provider setting."],
                [],
                "low",
                "Enable an AI provider in configuration to receive operational recommendations.",
                ["AiService provider configuration"],
                ["supervisor-agent"]),
            "api",
            null,
            Name));
    }
}
