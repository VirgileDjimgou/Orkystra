using Orkystra.Contracts.Ai;
using Orkystra.Contracts.ControlTower;

namespace Orkystra.Api.AI;

public interface IAiProvider
{
    string Name { get; }

    Task<AiRecommendationEnvelope> BuildRecommendationAsync(
        string tenantId,
        AiRecommendationQueryRequest request,
        ControlTowerOverviewResponse overview,
        CancellationToken cancellationToken);
}
