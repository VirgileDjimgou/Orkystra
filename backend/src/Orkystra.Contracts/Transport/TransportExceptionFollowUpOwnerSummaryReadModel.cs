namespace Orkystra.Contracts.Transport;

public sealed record TransportExceptionFollowUpOwnerSummaryReadModel(
    string Owner,
    bool IsUnassigned,
    int FollowUpCount,
    int ActiveCount,
    int OverdueCount,
    int AtRiskCount,
    int RetiredCount);
