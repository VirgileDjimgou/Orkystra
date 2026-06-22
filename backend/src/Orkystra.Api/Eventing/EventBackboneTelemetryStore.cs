using Microsoft.Extensions.Options;
using Orkystra.Application.Eventing;

namespace Orkystra.Api.Eventing;

public sealed class EventBackboneTelemetryStore
{
    private readonly EventBackboneOptions _options;
    private readonly Lock _lock = new();
    private int _publishedCount;
    private int _consumedCount;
    private DateTimeOffset? _lastPublishedAtUtc;
    private DateTimeOffset? _lastConsumedAtUtc;
    private string? _lastTopic;
    private string? _lastEventType;
    private string? _lastError;
    private IReadOnlyCollection<string> _lastAppliedProjections = [];
    private IReadOnlyCollection<string> _lastSkippedProjections = [];

    public EventBackboneTelemetryStore(IOptions<EventBackboneOptions> options)
    {
        _options = options.Value;
    }

    public void RecordPublished(string topic, string eventType, DateTimeOffset publishedAtUtc)
    {
        lock (_lock)
        {
            _publishedCount++;
            _lastPublishedAtUtc = publishedAtUtc;
            _lastTopic = topic;
            _lastEventType = eventType;
            _lastError = null;
        }
    }

    public void RecordConsumed(string topic, string eventType, ProjectionDispatchResult dispatchResult, DateTimeOffset consumedAtUtc)
    {
        lock (_lock)
        {
            _consumedCount++;
            _lastConsumedAtUtc = consumedAtUtc;
            _lastTopic = topic;
            _lastEventType = eventType;
            _lastAppliedProjections = dispatchResult.AppliedProjections.ToArray();
            _lastSkippedProjections = dispatchResult.SkippedProjections.ToArray();
            _lastError = null;
        }
    }

    public void RecordError(string message)
    {
        lock (_lock)
        {
            _lastError = message;
        }
    }

    public EventBackboneTelemetrySnapshot Snapshot()
    {
        lock (_lock)
        {
            return new EventBackboneTelemetrySnapshot(
                _options.Enabled,
                _options.BrokerUrl,
                _options.SimulationTopicFilter,
                _publishedCount,
                _consumedCount,
                _lastPublishedAtUtc,
                _lastConsumedAtUtc,
                _lastTopic,
                _lastEventType,
                _lastAppliedProjections.ToArray(),
                _lastSkippedProjections.ToArray(),
                _lastError);
        }
    }
}
