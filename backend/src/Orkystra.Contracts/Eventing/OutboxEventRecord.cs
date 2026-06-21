namespace Orkystra.Contracts.Eventing;

public sealed record OutboxEventRecord<TPayload>
    where TPayload : notnull
{
    public required Guid MessageId { get; init; }

    public required string Topic { get; init; }

    public required EventEnvelope<TPayload> Envelope { get; init; }

    public required DateTimeOffset StoredAt { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public int PublishAttemptCount { get; init; }

    public string? LastError { get; init; }
}
