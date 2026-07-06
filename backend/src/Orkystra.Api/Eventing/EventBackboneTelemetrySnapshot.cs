namespace Orkystra.Api.Eventing;

public sealed record EventBackboneTelemetrySnapshot(
    bool Enabled,
    string BrokerUrl,
    string SimulationTopicFilter,
    int PublishedCount,
    int ConsumedCount,
    int RecoveryRunCount,
    int RecoveryReplayedCount,
    int RecoveryFailedCount,
    DateTimeOffset? LastPublishedAtUtc,
    DateTimeOffset? LastConsumedAtUtc,
    DateTimeOffset? LastRecoveryAtUtc,
    string? LastTopic,
    string? LastEventType,
    IReadOnlyCollection<string> LastAppliedProjections,
    IReadOnlyCollection<string> LastSkippedProjections,
    string? LastError);
