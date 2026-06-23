namespace Orkystra.Contracts.Transport;

public sealed record TransportExceptionFollowUpHandoffItemReadModel(
    string ExceptionId,
    string Title,
    string? RouteReference,
    string? FollowUpOwner,
    DateTimeOffset? TargetReturnAtUtc,
    string SlaPosture,
    int? HoursUntilTarget,
    string? Note,
    string HandoffSummary,
    string ReadinessPosture,
    string ReadinessSummary,
    string AcknowledgementStatus,
    string AcknowledgementSummary,
    string RecommendedAction,
    string ActionLabel);
