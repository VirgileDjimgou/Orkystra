using System.Text;
using Orkystra.Application.Eventing;

namespace Orkystra.Api.Eventing;

public sealed class EventBackboneMessageDispatcher
{
    private readonly MqttEnvelopeSerializer _serializer;
    private readonly IdempotentProjectionRunner _projectionRunner;
    private readonly EventBackboneTelemetryStore _telemetryStore;
    private readonly ILogger<EventBackboneMessageDispatcher> _logger;

    public EventBackboneMessageDispatcher(
        MqttEnvelopeSerializer serializer,
        IdempotentProjectionRunner projectionRunner,
        EventBackboneTelemetryStore telemetryStore,
        ILogger<EventBackboneMessageDispatcher> logger)
    {
        _serializer = serializer;
        _projectionRunner = projectionRunner;
        _telemetryStore = telemetryStore;
        _logger = logger;
    }

    public async ValueTask DispatchAsync(
        string topic,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        var serializedEnvelope = Encoding.UTF8.GetString(payload.Span);
        var envelope = _serializer.Deserialize(serializedEnvelope);
        var dispatchResult = await _projectionRunner.DispatchAsync(envelope, cancellationToken);
        _telemetryStore.RecordConsumed(topic, envelope.EventType, dispatchResult, DateTimeOffset.UtcNow);

        _logger.LogInformation(
            "Dispatched MQTT event {EventType} from topic {Topic}. Applied projections: {AppliedCount}, skipped projections: {SkippedCount}.",
            envelope.EventType,
            topic,
            dispatchResult.AppliedCount,
            dispatchResult.SkippedCount);
    }
}
