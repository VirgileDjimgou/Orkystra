namespace Orkystra.Contracts.Transport;

public sealed record TransportExceptionResolutionEntryReadModel(
    string ExceptionId,
    string Status,
    string? Note,
    DateTimeOffset UpdatedAtUtc);
