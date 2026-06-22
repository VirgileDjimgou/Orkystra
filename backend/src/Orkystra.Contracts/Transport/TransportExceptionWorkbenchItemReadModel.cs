namespace Orkystra.Contracts.Transport;

public sealed record TransportExceptionWorkbenchItemReadModel(
    string ExceptionId,
    string Severity,
    string Category,
    string Title,
    string Detail,
    Guid? RouteId,
    string? RouteReference,
    string RecommendedAction,
    string ActionLabel,
    string? ResolutionStatus,
    string? ResolutionNote,
    DateTimeOffset? ResolutionUpdatedAtUtc,
    IReadOnlyCollection<string> Evidence);
