using Orkystra.Contracts.Ai;
using Orkystra.Contracts.ControlTower;

namespace Orkystra.Api.AI;

public sealed class LocalAiProvider : IAiProvider
{
    private readonly ILogger<LocalAiProvider>? _logger;

    public LocalAiProvider(ILogger<LocalAiProvider>? logger)
    {
        _logger = logger;
    }

    public string Name => "local";

    public Task<AiRecommendationEnvelope> BuildRecommendationAsync(
        string tenantId,
        AiRecommendationQueryRequest request,
        ControlTowerOverviewResponse overview,
        CancellationToken cancellationToken)
    {
        _logger?.LogInformation(
            "Local AI provider building recommendation for tenant {TenantId}, question '{Question}'",
            tenantId,
            request.Question);

        return Task.FromResult(new AiRecommendationEnvelope(
            AiRecommendationBuilder.BuildFromProjections(request, overview),
            "api",
            null,
            Name));
    }
}
