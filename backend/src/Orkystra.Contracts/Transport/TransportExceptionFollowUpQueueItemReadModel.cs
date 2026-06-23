namespace Orkystra.Contracts.Transport;

public sealed record TransportExceptionFollowUpQueueItemReadModel(
    string ExceptionId,
    string Title,
    string Category,
    string Detail,
    Guid? RouteId,
    string? RouteReference,
    string Status,
    string? Note,
    string? FollowUpOwner,
    DateTimeOffset? TargetReturnAtUtc,
    DateTimeOffset UpdatedAtUtc,
    bool IsStillActive,
    bool IsOwnerMissing,
    bool IsOverdue,
    string AlertSeverity,
    string AlertSummary,
    int UpdateCount,
    string? PreviousStatus,
    string RecommendedAction,
    string ActionLabel,
    IReadOnlyCollection<string> Evidence);
