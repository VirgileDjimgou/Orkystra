namespace Orkystra.Api.Eventing;

public sealed record EventBackboneTelemetrySnapshot(
    bool Enabled,
    string BrokerUrl,
    string SimulationTopicFilter,
    int PublishedCount,
    int ConsumedCount,
    DateTimeOffset? LastPublishedAtUtc,
    DateTimeOffset? LastConsumedAtUtc,
    string? LastTopic,
    string? LastEventType,
    IReadOnlyCollection<string> LastAppliedProjections,
    IReadOnlyCollection<string> LastSkippedProjections,
    string? LastError);
