namespace Orkystra.Contracts.Transport;

public sealed record TransportExceptionFollowUpQueueReadModel(
    DateTimeOffset GeneratedAtUtc,
    int FollowUpCount,
    int ActiveDeferredCount,
    int WatchlistCount,
    int OwnerlessCount,
    int OverdueCount,
    int HealthyCommitmentCount,
    string AlertSummary,
    string Summary,
    IReadOnlyCollection<TransportExceptionFollowUpQueueItemReadModel> Items);
