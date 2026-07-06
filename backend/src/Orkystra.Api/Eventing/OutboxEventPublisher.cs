using Orkystra.Contracts.Eventing;

namespace Orkystra.Api.Eventing;

public sealed class OutboxEventPublisher : IEventBackbonePublisher
{
    private readonly IEventBackbonePublisher _inner;
    private readonly EventOutboxStore _outboxStore;
    private readonly MqttEnvelopeSerializer _serializer;
    private readonly ILogger<OutboxEventPublisher> _logger;

    public OutboxEventPublisher(
        IEventBackbonePublisher inner,
        EventOutboxStore outboxStore,
        MqttEnvelopeSerializer serializer,
        ILogger<OutboxEventPublisher> logger)
    {
        _inner = inner;
        _outboxStore = outboxStore;
        _serializer = serializer;
        _logger = logger;
    }

    public async ValueTask PublishAsync(IEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var serializedEnvelope = _serializer.Serialize(envelope);
        var entryId = await _outboxStore.RecordPendingSerializedAsync(
            envelope.MessageId.ToString("D"),
            envelope.EventType,
            envelope.Topic,
            serializedEnvelope,
            cancellationToken);

        try
        {
            await _inner.PublishAsync(envelope, cancellationToken);
            await _outboxStore.MarkPublishedAsync(entryId, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to publish event {EventType} to MQTT topic {Topic}. Outbox entry {EntryId} marked as failed.",
                envelope.EventType, envelope.Topic, entryId);
            await _outboxStore.MarkFailedAsync(entryId, exception.Message, cancellationToken);
            throw;
        }
    }

}
