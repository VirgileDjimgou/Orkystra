namespace Orkystra.Contracts.Eventing;

public sealed record EventEnvelope<TPayload> : IEventEnvelope
    where TPayload : notnull
{
    public required Guid MessageId { get; init; }

    public required string Topic { get; init; }

    public required string EventType { get; init; }

    public required int SchemaVersion { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required string BoundedContext { get; init; }

    public required string AggregateType { get; init; }

    public required Guid AggregateId { get; init; }

    public Guid? CorrelationId { get; init; }

    public Guid? CausationId { get; init; }

    public Guid? TenantId { get; init; }

    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    public required TPayload Payload { get; init; }

    object IEventEnvelope.Payload => Payload;

    public Type PayloadType => typeof(TPayload);
}
