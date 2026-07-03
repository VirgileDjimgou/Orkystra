using Orkystra.Contracts.Ai;
using Orkystra.Contracts.ControlTower;

namespace Orkystra.Api.AI;

public sealed class AiWorkflowService
{
    private readonly IAiProvider _provider;
    private readonly ILogger<AiWorkflowService> _logger;

    public AiWorkflowService(IAiProvider provider, ILogger<AiWorkflowService> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public async Task<AiRecommendationEnvelope> BuildRecommendationAsync(
        string tenantId,
        AiRecommendationQueryRequest request,
        ControlTowerOverviewResponse overview,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "AI workflow routing to provider '{ProviderName}' for tenant {TenantId}",
            _provider.Name,
            tenantId);

        return await _provider.BuildRecommendationAsync(tenantId, request, overview, cancellationToken);
    }
}
