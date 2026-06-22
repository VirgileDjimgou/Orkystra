using Orkystra.Contracts.Eventing;

namespace Orkystra.Application.Eventing;

public static class IntegrationEventEnvelopeFactory
{
    public static EventEnvelope<TPayload> Create<TPayload>(
        TPayload payload,
        string boundedContext,
        string aggregateType,
        Guid aggregateId,
        string eventType,
        int schemaVersion = 1,
        string? topicOverride = null,
        Guid? correlationId = null,
        Guid? causationId = null,
        Guid? tenantId = null,
        IReadOnlyDictionary<string, string>? headers = null)
        where TPayload : notnull
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundedContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        return new EventEnvelope<TPayload>
        {
            MessageId = Guid.NewGuid(),
            Topic = topicOverride ?? EventTopic.BuildEventTopic(boundedContext, aggregateType, eventType, schemaVersion),
            EventType = eventType,
            SchemaVersion = schemaVersion,
            OccurredAt = DateTimeOffset.UtcNow,
            BoundedContext = boundedContext,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            CorrelationId = correlationId,
            CausationId = causationId,
            TenantId = tenantId,
            Headers = headers ?? new Dictionary<string, string>(),
            Payload = payload
        };
    }
}
