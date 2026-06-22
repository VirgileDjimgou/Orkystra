using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orkystra.Api.Eventing;
using Orkystra.Api.Simulation;
using Orkystra.Application.Eventing;
using Orkystra.Contracts.Eventing;
using Orkystra.Contracts.Simulation;
using Orkystra.Domain.Identities;
using Orkystra.Domain.Simulation.Events;

namespace Orkystra.Domain.Tests;

public sealed class EventBackboneActivationTests
{
    [Fact]
    public void MqttEnvelopeSerializer_roundtrips_scenario_started_envelope()
    {
        var serializer = new MqttEnvelopeSerializer();
        var scenarioId = ScenarioId.New();
        var envelope = DomainEventEnvelopeFactory.Create(
            new ScenarioStarted(scenarioId, "Broker Demo", 42),
            correlationId: Guid.NewGuid(),
            headers: new Dictionary<string, string> { ["source"] = "test" });

        var serialized = serializer.Serialize(envelope);
        var roundTripped = serializer.Deserialize(serialized);

        Assert.Equal(envelope.MessageId, roundTripped.MessageId);
        Assert.Equal(envelope.Topic, roundTripped.Topic);
        Assert.Equal(envelope.EventType, roundTripped.EventType);
        Assert.Equal(envelope.AggregateId, roundTripped.AggregateId);
        Assert.IsType<ScenarioStarted>(roundTripped.Payload);
        Assert.Equal("Broker Demo", ((ScenarioStarted)roundTripped.Payload).Name);
    }

    [Fact]
    public async Task EventBackboneMessageDispatcher_projects_serialized_events_and_tracks_duplicate_skips()
    {
        var serializer = new MqttEnvelopeSerializer();
        var projection = new ScenarioSummaryProjection();
        var runner = new IdempotentProjectionRunner([projection], new InMemoryInboxStateStore());
        var telemetryStore = new EventBackboneTelemetryStore(Options.Create(new EventBackboneOptions()));
        var dispatcher = new EventBackboneMessageDispatcher(
            serializer,
            runner,
            telemetryStore,
            NullLogger<EventBackboneMessageDispatcher>.Instance);

        var scenarioId = ScenarioId.New();
        var envelope = DomainEventEnvelopeFactory.Create(new ScenarioStarted(scenarioId, "Broker Demo", 42));
        var payload = Encoding.UTF8.GetBytes(serializer.Serialize(envelope));

        await dispatcher.DispatchAsync(envelope.Topic, payload);
        await dispatcher.DispatchAsync(envelope.Topic, payload);

        Assert.True(projection.TryGet(scenarioId.Value, out var summary));
        Assert.NotNull(summary);
        Assert.Equal("Broker Demo", summary!.Name);

        var snapshot = telemetryStore.Snapshot();
        Assert.Equal(2, snapshot.ConsumedCount);
        Assert.Equal(["scenario-summary"], snapshot.LastSkippedProjections);
    }

    [Fact]
    public async Task ScenarioEventWorkflowService_publishes_demo_sequence_to_event_backbone()
    {
        var publisher = new RecordingEventBackbonePublisher();
        var service = new ScenarioEventWorkflowService(publisher);
        var request = new PublishScenarioEventsRequest("Broker Demo", 42, 15, IncludeDisruption: true, CompleteScenario: true);

        var result = await service.PublishDemoScenarioAsync("tenant-alpha", request);

        Assert.Equal(4, result.PublishedCount);
        Assert.Equal(4, publisher.Envelopes.Count);
        Assert.Equal(
            [nameof(ScenarioStarted), nameof(TimeAdvanced), nameof(RandomEventInjected), nameof(ScenarioCompleted)],
            publisher.Envelopes.Select(static envelope => envelope.EventType).ToArray());
        Assert.All(publisher.Envelopes, envelope => Assert.StartsWith("orkystra/events/simulation/scenario/", envelope.Topic));
        Assert.All(publisher.Envelopes, envelope => Assert.Equal("tenant-alpha", envelope.Headers["tenant-key"]));
    }

    private sealed class RecordingEventBackbonePublisher : IEventBackbonePublisher
    {
        public List<IEventEnvelope> Envelopes { get; } = [];

        public ValueTask PublishAsync(IEventEnvelope envelope, CancellationToken cancellationToken = default)
        {
            Envelopes.Add(envelope);
            return ValueTask.CompletedTask;
        }
    }
}
