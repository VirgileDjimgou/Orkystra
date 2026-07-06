namespace Orkystra.Api.Eventing;

public sealed class OutboxRecoveryService
{
    private readonly EventOutboxStore _outboxStore;
    private readonly MqttEnvelopeSerializer _serializer;
    private readonly IRawEventBackbonePublisher _publisher;
    private readonly EventBackboneTelemetryStore _telemetryStore;
    private readonly ILogger<OutboxRecoveryService> _logger;

    public OutboxRecoveryService(
        EventOutboxStore outboxStore,
        MqttEnvelopeSerializer serializer,
        IRawEventBackbonePublisher publisher,
        EventBackboneTelemetryStore telemetryStore,
        ILogger<OutboxRecoveryService> logger)
    {
        _outboxStore = outboxStore;
        _serializer = serializer;
        _publisher = publisher;
        _telemetryStore = telemetryStore;
        _logger = logger;
    }

    public async Task<OutboxRecoveryResult> ReplayPendingAsync(
        int batchSize,
        bool triggeredAutomatically,
        CancellationToken cancellationToken = default)
    {
        var effectiveBatchSize = Math.Max(1, batchSize);
        var pendingEntries = await _outboxStore.GetPendingEntriesAsync(effectiveBatchSize, cancellationToken);
        var replayed = 0;
        var failed = 0;

        foreach (var entry in pendingEntries)
        {
            try
            {
                var envelope = _serializer.Deserialize(entry.PayloadJson);
                await _publisher.PublishAsync(envelope, cancellationToken);
                await _outboxStore.MarkPublishedAsync(entry.Id, cancellationToken);
                replayed++;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Replay failed for outbox entry {EntryId}.", entry.Id);
                await _outboxStore.MarkFailedAsync(entry.Id, exception.Message, cancellationToken);
                failed++;
            }
        }

        var result = new OutboxRecoveryResult(
            Replayed: replayed,
            Failed: failed,
            Total: pendingEntries.Count,
            TriggeredAutomatically: triggeredAutomatically,
            ProcessedAtUtc: DateTimeOffset.UtcNow);

        _telemetryStore.RecordRecovery(result);
        return result;
    }
}
