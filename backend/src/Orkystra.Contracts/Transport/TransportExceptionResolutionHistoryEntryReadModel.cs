namespace Orkystra.Contracts.Transport;

public sealed record TransportExceptionResolutionHistoryEntryReadModel(
    string HistoryEntryId,
    string ExceptionId,
    string Status,
    string? Note,
    string? FollowUpOwner,
    DateTimeOffset? TargetReturnAtUtc,
    DateTimeOffset UpdatedAtUtc);
