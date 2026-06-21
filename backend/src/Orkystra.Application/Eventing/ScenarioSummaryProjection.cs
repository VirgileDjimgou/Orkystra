using Orkystra.Contracts.Eventing;
using Orkystra.Contracts.Simulation;
using Orkystra.Domain.Simulation.Events;

namespace Orkystra.Application.Eventing;

public sealed class ScenarioSummaryProjection : IEventProjection
{
    private readonly Dictionary<Guid, ScenarioSummaryReadModel> _scenarios = [];

    public string ProjectionName => "scenario-summary";

    public bool CanProject(IEventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return envelope.PayloadType == typeof(ScenarioStarted) ||
               envelope.PayloadType == typeof(TimeAdvanced) ||
               envelope.PayloadType == typeof(RandomEventInjected) ||
               envelope.PayloadType == typeof(ScenarioPaused) ||
               envelope.PayloadType == typeof(ScenarioResumed) ||
               envelope.PayloadType == typeof(ScenarioCompleted);
    }

    public ValueTask ProjectAsync(IEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        switch (envelope.Payload)
        {
            case ScenarioStarted started:
                _scenarios[started.ScenarioId.Value] = new ScenarioSummaryReadModel(
                    started.ScenarioId.Value,
                    started.Name,
                    started.Seed,
                    "Running",
                    envelope.OccurredAt,
                    0);
                break;

            case TimeAdvanced advanced when _scenarios.TryGetValue(advanced.ScenarioId.Value, out var current):
                _scenarios[advanced.ScenarioId.Value] = current with
                {
                    CurrentTime = advanced.CurrentTime
                };
                break;

            case RandomEventInjected injected when _scenarios.TryGetValue(injected.ScenarioId.Value, out var current):
                _scenarios[injected.ScenarioId.Value] = current with
                {
                    InjectedEventCount = current.InjectedEventCount + 1
                };
                break;

            case ScenarioPaused paused when _scenarios.TryGetValue(paused.ScenarioId.Value, out var current):
                _scenarios[paused.ScenarioId.Value] = current with
                {
                    Status = "Paused",
                    CurrentTime = paused.PausedAt
                };
                break;

            case ScenarioResumed resumed when _scenarios.TryGetValue(resumed.ScenarioId.Value, out var current):
                _scenarios[resumed.ScenarioId.Value] = current with
                {
                    Status = "Running",
                    CurrentTime = resumed.ResumedAt
                };
                break;

            case ScenarioCompleted completed when _scenarios.TryGetValue(completed.ScenarioId.Value, out var current):
                _scenarios[completed.ScenarioId.Value] = current with
                {
                    Status = "Completed",
                    CurrentTime = completed.CompletedAt
                };
                break;
        }

        return ValueTask.CompletedTask;
    }

    public bool TryGet(Guid scenarioId, out ScenarioSummaryReadModel? readModel)
    {
        var found = _scenarios.TryGetValue(scenarioId, out var current);
        readModel = current;
        return found;
    }
}
