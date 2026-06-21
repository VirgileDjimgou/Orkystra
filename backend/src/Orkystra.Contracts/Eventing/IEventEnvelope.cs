namespace Orkystra.Contracts.Eventing;

public interface IEventEnvelope
{
    Guid MessageId { get; }

    string Topic { get; }

    string EventType { get; }

    int SchemaVersion { get; }

    DateTimeOffset OccurredAt { get; }

    string BoundedContext { get; }

    string AggregateType { get; }

    Guid AggregateId { get; }

    Guid? CorrelationId { get; }

    Guid? CausationId { get; }

    Guid? TenantId { get; }

    IReadOnlyDictionary<string, string> Headers { get; }

    object Payload { get; }

    Type PayloadType { get; }
}
