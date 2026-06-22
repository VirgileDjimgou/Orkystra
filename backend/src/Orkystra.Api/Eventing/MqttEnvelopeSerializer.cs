using System.Text.Json;
using Orkystra.Application.Eventing;
using Orkystra.Contracts.Connectors;
using Orkystra.Contracts.Eventing;
using Orkystra.Domain.Simulation.Events;

namespace Orkystra.Api.Eventing;

public sealed class MqttEnvelopeSerializer
{
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public string Serialize(IEventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var payloadElement = JsonSerializer.SerializeToElement(
            envelope.Payload,
            envelope.PayloadType,
            _serializerOptions);

        var serialized = new SerializedEventEnvelope
        {
            MessageId = envelope.MessageId,
            Topic = envelope.Topic,
            EventType = envelope.EventType,
            SchemaVersion = envelope.SchemaVersion,
            OccurredAt = envelope.OccurredAt,
            BoundedContext = envelope.BoundedContext,
            AggregateType = envelope.AggregateType,
            AggregateId = envelope.AggregateId,
            CorrelationId = envelope.CorrelationId,
            CausationId = envelope.CausationId,
            TenantId = envelope.TenantId,
            Headers = envelope.Headers.ToDictionary(static pair => pair.Key, static pair => pair.Value),
            Payload = payloadElement
        };

        return JsonSerializer.Serialize(serialized, _serializerOptions);
    }

    public IEventEnvelope Deserialize(string serializedEnvelope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serializedEnvelope);

        var envelope = JsonSerializer.Deserialize<SerializedEventEnvelope>(serializedEnvelope, _serializerOptions)
            ?? throw new InvalidOperationException("Serialized event envelope payload was empty.");

        return envelope.EventType switch
        {
            nameof(ScenarioStarted) => BuildEnvelope(envelope, DeserializePayload<ScenarioStarted>(envelope)),
            nameof(TimeAdvanced) => BuildEnvelope(envelope, DeserializePayload<TimeAdvanced>(envelope)),
            nameof(RandomEventInjected) => BuildEnvelope(envelope, DeserializePayload<RandomEventInjected>(envelope)),
            nameof(ScenarioPaused) => BuildEnvelope(envelope, DeserializePayload<ScenarioPaused>(envelope)),
            nameof(ScenarioResumed) => BuildEnvelope(envelope, DeserializePayload<ScenarioResumed>(envelope)),
            nameof(ScenarioCompleted) => BuildEnvelope(envelope, DeserializePayload<ScenarioCompleted>(envelope)),
            GpsPositionProjection.GpsPositionReportedEventType => BuildEnvelope(envelope, DeserializePayload<GpsPositionSnapshot>(envelope)),
            _ => throw new NotSupportedException($"Unsupported event type '{envelope.EventType}' for MQTT event deserialization.")
        };
    }

    private TPayload DeserializePayload<TPayload>(SerializedEventEnvelope envelope)
        where TPayload : notnull
    {
        return envelope.Payload.Deserialize<TPayload>(_serializerOptions)
            ?? throw new InvalidOperationException($"Unable to deserialize payload for event type '{envelope.EventType}'.");
    }

    private static EventEnvelope<TPayload> BuildEnvelope<TPayload>(SerializedEventEnvelope envelope, TPayload payload)
        where TPayload : notnull
    {
        return new EventEnvelope<TPayload>
        {
            MessageId = envelope.MessageId,
            Topic = envelope.Topic,
            EventType = envelope.EventType,
            SchemaVersion = envelope.SchemaVersion,
            OccurredAt = envelope.OccurredAt,
            BoundedContext = envelope.BoundedContext,
            AggregateType = envelope.AggregateType,
            AggregateId = envelope.AggregateId,
            CorrelationId = envelope.CorrelationId,
            CausationId = envelope.CausationId,
            TenantId = envelope.TenantId,
            Headers = envelope.Headers,
            Payload = payload
        };
    }
}
