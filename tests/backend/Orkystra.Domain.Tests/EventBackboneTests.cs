using Orkystra.Application.Eventing;
using Orkystra.Contracts.Eventing;
using Orkystra.Domain.Identities;
using Orkystra.Domain.Simulation.Events;

namespace Orkystra.Domain.Tests;

public sealed class EventBackboneTests
{
    [Fact]
    public void EventTopic_builds_expected_kebab_case_path()
    {
        var topic = EventTopic.BuildEventTopic("Simulation", "Scenario", "ScenarioStarted", 1);

        Assert.Equal("orkystra/events/simulation/scenario/scenario-started/v1", topic);
    }

    [Fact]
    public void DomainEventEnvelopeFactory_creates_envelope_with_routing_metadata()
    {
        var scenarioId = ScenarioId.New();
        var correlationId = Guid.NewGuid();
        var tenantId = TenantId.New();
        var domainEvent = new ScenarioStarted(scenarioId, "Baseline", 42);

        var envelope = DomainEventEnvelopeFactory.Create(domainEvent, correlationId: correlationId, tenantId: tenantId.Value);

        Assert.Equal(domainEvent.EventId, envelope.MessageId);
        Assert.Equal("ScenarioStarted", envelope.EventType);
        Assert.Equal("Simulation", envelope.BoundedContext);
        Assert.Equal("Scenario", envelope.AggregateType);
        Assert.Equal(scenarioId.Value, envelope.AggregateId);
        Assert.Equal(correlationId, envelope.CorrelationId);
        Assert.Equal(tenantId.Value, envelope.TenantId);
        Assert.Equal("orkystra/events/simulation/scenario/scenario-started/v1", envelope.Topic);
        Assert.Same(domainEvent, envelope.Payload);
    }

    [Fact]
    public async Task IdempotentProjectionRunner_applies_each_message_once_per_projection()
    {
        var projection = new ScenarioSummaryProjection();
        var runner = new IdempotentProjectionRunner([projection], new InMemoryInboxStateStore());
        var scenarioId = ScenarioId.New();
        var started = DomainEventEnvelopeFactory.Create(new ScenarioStarted(scenarioId, "Baseline", 42));

        var firstDispatch = await runner.DispatchAsync(started);
        var secondDispatch = await runner.DispatchAsync(started);

        Assert.Equal(1, firstDispatch.AppliedCount);
        Assert.Empty(firstDispatch.SkippedProjections);
        Assert.Equal(0, secondDispatch.AppliedCount);
        Assert.Equal(["scenario-summary"], secondDispatch.SkippedProjections);
    }

    [Fact]
    public async Task ScenarioSummaryProjection_tracks_started_advanced_disrupted_and_completed_state()
    {
        var projection = new ScenarioSummaryProjection();
        var runner = new IdempotentProjectionRunner([projection], new InMemoryInboxStateStore());
        var scenarioId = ScenarioId.New();
        var occurredAt = new DateTimeOffset(2026, 06, 20, 10, 00, 00, TimeSpan.Zero);
        var currentTime = occurredAt.AddMinutes(15);
        var completedAt = occurredAt.AddMinutes(30);

        await runner.DispatchAsync(
            DomainEventEnvelopeFactory.Create(
                new ScenarioStarted(scenarioId, "Baseline", 42),
                headers: new Dictionary<string, string> { ["source"] = "simulation" }));

        await runner.DispatchAsync(
            DomainEventEnvelopeFactory.Create(
                new TimeAdvanced(scenarioId, currentTime, TimeSpan.FromMinutes(15).Ticks)));

        await runner.DispatchAsync(
            DomainEventEnvelopeFactory.Create(
                new RandomEventInjected(
                    scenarioId,
                    "dock-blocked",
                    "Warehouse",
                    "forklift-maintenance",
                    2)));

        await runner.DispatchAsync(
            DomainEventEnvelopeFactory.Create(
                new ScenarioCompleted(scenarioId, completedAt)));

        Assert.True(projection.TryGet(scenarioId.Value, out var summary));
        Assert.NotNull(summary);
        Assert.Equal("Baseline", summary!.Name);
        Assert.Equal(42, summary.Seed);
        Assert.Equal("Completed", summary.Status);
        Assert.Equal(completedAt, summary.CurrentTime);
        Assert.Equal(1, summary.InjectedEventCount);
    }
}
