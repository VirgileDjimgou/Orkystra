namespace Orkystra.Contracts.Transport;

public sealed record TransportSyncImportDetailReadModel(
    long RunId,
    string ProviderId,
    string Source,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ImportedAtUtc,
    int ImportedRouteCount,
    string HealthStatus,
    string Summary,
    IReadOnlyCollection<RouteSummaryReadModel> Routes);
