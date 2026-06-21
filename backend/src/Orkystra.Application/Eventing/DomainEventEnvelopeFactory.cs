using System.Reflection;
using Orkystra.Contracts.Eventing;
using Orkystra.Domain.Events;

namespace Orkystra.Application.Eventing;

public static class DomainEventEnvelopeFactory
{
    public static EventEnvelope<TDomainEvent> Create<TDomainEvent>(
        TDomainEvent domainEvent,
        Guid? correlationId = null,
        Guid? causationId = null,
        Guid? tenantId = null,
        IReadOnlyDictionary<string, string>? headers = null)
        where TDomainEvent : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var boundedContext = ResolveBoundedContext(typeof(TDomainEvent));
        var aggregateIdentity = ResolveAggregateIdentity(domainEvent);
        var aggregateType = aggregateIdentity.Name[..^2];

        return new EventEnvelope<TDomainEvent>
        {
            MessageId = domainEvent.EventId,
            Topic = EventTopic.BuildEventTopic(boundedContext, aggregateType, domainEvent.EventType, domainEvent.SchemaVersion),
            EventType = domainEvent.EventType,
            SchemaVersion = domainEvent.SchemaVersion,
            OccurredAt = domainEvent.OccurredAt,
            BoundedContext = boundedContext,
            AggregateType = aggregateType,
            AggregateId = aggregateIdentity.Value,
            CorrelationId = correlationId,
            CausationId = causationId,
            TenantId = tenantId,
            Headers = headers ?? new Dictionary<string, string>(),
            Payload = domainEvent
        };
    }

    private static string ResolveBoundedContext(Type domainEventType)
    {
        var segments = domainEventType.Namespace?.Split('.');

        if (segments is null)
        {
            throw new InvalidOperationException($"Cannot resolve namespace for event type '{domainEventType.Name}'.");
        }

        var eventsIndex = Array.LastIndexOf(segments, "Events");

        if (eventsIndex <= 0)
        {
            throw new InvalidOperationException($"Cannot resolve bounded context for event type '{domainEventType.Name}'.");
        }

        return segments[eventsIndex - 1];
    }

    private static (string Name, Guid Value) ResolveAggregateIdentity<TDomainEvent>(TDomainEvent domainEvent)
        where TDomainEvent : DomainEvent
    {
        var identityProperty = domainEvent
            .GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(property =>
                property.Name.EndsWith("Id", StringComparison.Ordinal) &&
                property.PropertyType.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public) is not null);

        if (identityProperty is null)
        {
            throw new InvalidOperationException($"Cannot resolve aggregate identity for event type '{domainEvent.EventType}'.");
        }

        var identity = identityProperty.GetValue(domainEvent);
        var valueProperty = identityProperty.PropertyType.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);

        if (identity is null || valueProperty?.GetValue(identity) is not Guid aggregateId)
        {
            throw new InvalidOperationException($"Cannot resolve aggregate id value for event type '{domainEvent.EventType}'.");
        }

        return (identityProperty.Name, aggregateId);
    }
}
