using Orkystra.Api.Eventing;
using Orkystra.Application.Eventing;
using Orkystra.Contracts.Eventing;
using Orkystra.Contracts.Simulation;
using Orkystra.Domain.Identities;
using Orkystra.Domain.Simulation.Events;

namespace Orkystra.Api.Simulation;

public sealed class ScenarioEventWorkflowService
{
    private readonly IEventBackbonePublisher _eventPublisher;

    public ScenarioEventWorkflowService(IEventBackbonePublisher eventPublisher)
    {
        _eventPublisher = eventPublisher;
    }

    public async ValueTask<PublishScenarioEventsResponse> PublishDemoScenarioAsync(
        string tenantId,
        PublishScenarioEventsRequest request,
        CancellationToken cancellationToken = default)
    {
        var scenarioId = ScenarioId.New();
        var correlationId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;
        var advancedAt = startedAt.AddMinutes(request.AdvanceMinutes);
        var headers = new Dictionary<string, string>
        {
            ["source"] = "simulation-api",
            ["tenant-key"] = tenantId
        };

        var envelopes = new List<IEventEnvelope>
        {
            DomainEventEnvelopeFactory.Create(
                new ScenarioStarted(scenarioId, request.Name, request.Seed),
                correlationId: correlationId,
                headers: headers),
            DomainEventEnvelopeFactory.Create(
                new TimeAdvanced(scenarioId, advancedAt, TimeSpan.FromMinutes(request.AdvanceMinutes).Ticks),
                correlationId: correlationId,
                headers: headers)
        };

        if (request.IncludeDisruption)
        {
            envelopes.Add(
                DomainEventEnvelopeFactory.Create(
                    new RandomEventInjected(
                        scenarioId,
                        "dock-blocked",
                        "Warehouse",
                        "forklift-maintenance",
                        2),
                    correlationId: correlationId,
                    headers: headers));
        }

        if (request.CompleteScenario)
        {
            envelopes.Add(
                DomainEventEnvelopeFactory.Create(
                    new ScenarioCompleted(scenarioId, advancedAt.AddMinutes(5)),
                    correlationId: correlationId,
                    headers: headers));
        }

        foreach (var envelope in envelopes)
        {
            await _eventPublisher.PublishAsync(envelope, cancellationToken);
        }

        return new PublishScenarioEventsResponse(
            scenarioId.Value,
            envelopes.First().Topic,
            envelopes.Count,
            envelopes.Select(static envelope => envelope.EventType).ToArray(),
            DateTimeOffset.UtcNow);
    }
}
