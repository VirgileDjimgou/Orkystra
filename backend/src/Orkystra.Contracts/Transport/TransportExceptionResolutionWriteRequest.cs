namespace Orkystra.Contracts.Transport;

public sealed record TransportExceptionResolutionWriteRequest(
    string ExceptionId,
    string Status,
    string? Note,
    string? FollowUpOwner,
    DateTimeOffset? TargetReturnAtUtc);
