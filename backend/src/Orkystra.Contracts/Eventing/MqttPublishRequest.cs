namespace Orkystra.Contracts.Eventing;

public sealed record MqttPublishRequest<TPayload>
    where TPayload : notnull
{
    public required string Topic { get; init; }

    public required EventEnvelope<TPayload> Envelope { get; init; }

    public int QualityOfService { get; init; } = 1;

    public bool Retain { get; init; }
}
