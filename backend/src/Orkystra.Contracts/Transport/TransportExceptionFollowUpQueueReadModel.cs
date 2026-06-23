namespace Orkystra.Contracts.Transport;

public sealed record TransportExceptionFollowUpQueueReadModel(
    DateTimeOffset GeneratedAtUtc,
    int FollowUpCount,
    int ActiveDeferredCount,
    int RetiredFollowUpCount,
    int OwnerlessCount,
    int AtRiskCount,
    int OverdueCount,
    int HealthyCommitmentCount,
    string? FocusExceptionId,
    string? FocusTitle,
    string FocusSummary,
    string AlertSummary,
    string Summary,
    TransportExceptionFollowUpEscalationDigestReadModel EscalationDigest,
    TransportExceptionFollowUpHandoffPackReadModel HandoffPack,
    IReadOnlyCollection<TransportExceptionFollowUpOwnerSummaryReadModel> OwnerSummaries,
    IReadOnlyCollection<TransportExceptionFollowUpQueueItemReadModel> Items);
