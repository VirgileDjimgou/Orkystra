namespace Orkystra.Api.Eventing;

public sealed record OutboxRecoveryResult(
    int Replayed,
    int Failed,
    int Total,
    bool TriggeredAutomatically,
    DateTimeOffset ProcessedAtUtc);
