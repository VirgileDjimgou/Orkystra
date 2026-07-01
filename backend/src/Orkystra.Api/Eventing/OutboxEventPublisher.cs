using Orkystra.Contracts.Eventing;

namespace Orkystra.Api.Eventing;

public sealed class OutboxEventPublisher : IEventBackbonePublisher
{
    private readonly IEventBackbonePublisher _inner;
    private readonly EventOutboxStore _outboxStore;
    private readonly ILogger<OutboxEventPublisher> _logger;

    public OutboxEventPublisher(
        IEventBackbonePublisher inner,
        EventOutboxStore outboxStore,
        ILogger<OutboxEventPublisher> logger)
    {
        _inner = inner;
        _outboxStore = outboxStore;
        _logger = logger;
    }

    public async ValueTask PublishAsync(IEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var entryId = await _outboxStore.RecordPendingAsync(
            envelope.MessageId.ToString("D"),
            envelope.EventType,
            envelope.Topic,
            new
            {
                envelope.MessageId,
                envelope.EventType,
                envelope.SchemaVersion,
                envelope.OccurredAt,
                tenantId = envelope.TenantId,
                envelope.CorrelationId,
                envelope.CausationId,
                envelope.BoundedContext,
                envelope.AggregateType,
                envelope.AggregateId,
                envelope.Payload
            },
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
