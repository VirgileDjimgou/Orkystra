using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orkystra.Api.Eventing;
using Orkystra.Api.Persistence;
using Orkystra.Contracts.Eventing;
using Orkystra.Domain.Identities;
using Orkystra.Domain.Simulation.Events;

namespace Orkystra.Domain.Tests;

public sealed class OutboxRecoveryServiceTests : IDisposable
{
    private readonly TemporaryDirectory _tempDir = new("orkystra-outbox-recovery-tests");

    public void Dispose()
    {
        _tempDir.Dispose();
    }

    [Fact]
    public async Task ReplayPendingAsync_republishes_pending_entries_and_updates_telemetry()
    {
        using var store = new EventOutboxStore(
            Options.Create(new OperationalPersistenceOptions
            {
                DatabasePath = Path.Combine("data", "recovery.db")
            }),
            _tempDir.FullName);

        var serializer = new MqttEnvelopeSerializer();
        var telemetry = new EventBackboneTelemetryStore(Options.Create(new EventBackboneOptions()));
        var publisher = new RecordingRawPublisher();
        var service = new OutboxRecoveryService(
            store,
            serializer,
            publisher,
            telemetry,
            NullLogger<OutboxRecoveryService>.Instance);

        var envelope = CreateEnvelope();
        await store.RecordPendingSerializedAsync(
            envelope.MessageId.ToString("D"),
            envelope.EventType,
            envelope.Topic,
            serializer.Serialize(envelope));

        var result = await service.ReplayPendingAsync(10, triggeredAutomatically: true);

        Assert.Equal(1, result.Replayed);
        Assert.Equal(0, result.Failed);
        Assert.Equal(1, result.Total);
        Assert.Single(publisher.Envelopes);
        Assert.Empty(await store.GetPendingEntriesAsync(10));

        var snapshot = telemetry.Snapshot();
        Assert.Equal(1, snapshot.RecoveryRunCount);
        Assert.Equal(1, snapshot.RecoveryReplayedCount);
        Assert.Equal(0, snapshot.RecoveryFailedCount);
        Assert.NotNull(snapshot.LastRecoveryAtUtc);
    }

    [Fact]
    public async Task ReplayPendingAsync_marks_failed_entries_when_publisher_throws()
    {
        using var store = new EventOutboxStore(
            Options.Create(new OperationalPersistenceOptions
            {
                DatabasePath = Path.Combine("data", "recovery-failed.db")
            }),
            _tempDir.FullName);

        var serializer = new MqttEnvelopeSerializer();
        var telemetry = new EventBackboneTelemetryStore(Options.Create(new EventBackboneOptions()));
        var service = new OutboxRecoveryService(
            store,
            serializer,
            new ThrowingRawPublisher(),
            telemetry,
            NullLogger<OutboxRecoveryService>.Instance);

        var envelope = CreateEnvelope();
        await store.RecordPendingSerializedAsync(
            envelope.MessageId.ToString("D"),
            envelope.EventType,
            envelope.Topic,
            serializer.Serialize(envelope));

        var result = await service.ReplayPendingAsync(10, triggeredAutomatically: false);

        Assert.Equal(0, result.Replayed);
        Assert.Equal(1, result.Failed);
        Assert.Equal(1, result.Total);

        var pending = await store.GetPendingEntriesAsync(10);
        var entry = Assert.Single(pending);
        Assert.Equal("failed", entry.Status);

        var snapshot = telemetry.Snapshot();
        Assert.Equal(1, snapshot.RecoveryRunCount);
        Assert.Equal(0, snapshot.RecoveryReplayedCount);
        Assert.Equal(1, snapshot.RecoveryFailedCount);
    }

    private static IEventEnvelope CreateEnvelope()
    {
        var scenarioId = ScenarioId.New();
        return new EventEnvelope<JsonElement>
        {
            MessageId = Guid.NewGuid(),
            EventType = nameof(ScenarioStarted),
            SchemaVersion = 1,
            OccurredAt = DateTimeOffset.UtcNow,
            TenantId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            CausationId = null,
            BoundedContext = "Simulation",
            AggregateType = "Scenario",
            AggregateId = scenarioId.Value,
            Topic = "orkystra/events/simulation/scenario/scenario-started/v1",
            Headers = new Dictionary<string, string>(),
            Payload = JsonSerializer.SerializeToElement(new ScenarioStarted(scenarioId, "Recovery Demo", 42))
        };
    }

    private sealed class RecordingRawPublisher : IRawEventBackbonePublisher
    {
        public List<IEventEnvelope> Envelopes { get; } = [];

        public ValueTask PublishAsync(IEventEnvelope envelope, CancellationToken cancellationToken = default)
        {
            Envelopes.Add(envelope);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingRawPublisher : IRawEventBackbonePublisher
    {
        public ValueTask PublishAsync(IEventEnvelope envelope, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("publisher unavailable");
    }
}
