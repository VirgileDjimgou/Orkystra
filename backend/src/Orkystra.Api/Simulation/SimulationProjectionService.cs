using Orkystra.Application.Eventing;
using Orkystra.Contracts.Simulation;

namespace Orkystra.Api.Simulation;

public sealed class SimulationProjectionService
{
    private readonly ScenarioSummaryProjection _scenarioSummaryProjection;

    public SimulationProjectionService(ScenarioSummaryProjection scenarioSummaryProjection)
    {
        _scenarioSummaryProjection = scenarioSummaryProjection;
    }

    public ValueTask<IReadOnlyCollection<ScenarioSummaryReadModel>> ListAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(_scenarioSummaryProjection.ListAll());
    }
}
