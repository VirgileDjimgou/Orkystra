using System.Text.Json;

namespace Orkystra.Api.Eventing;

internal sealed record SerializedEventEnvelope
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

    public Dictionary<string, string> Headers { get; init; } = [];

    public required JsonElement Payload { get; init; }
}
