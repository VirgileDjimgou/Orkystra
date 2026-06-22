namespace Orkystra.Contracts.Transport;

public sealed record TransportSyncHistoryEntryReadModel(
    long RunId,
    string ProviderId,
    string Source,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ImportedAtUtc,
    int ImportedRouteCount,
    IReadOnlyCollection<string> ImportedRouteReferences,
    string HealthStatus,
    string Summary,
    bool HasComparablePrevious,
    int AddedRouteCount,
    int RemovedRouteCount,
    int ChangedRouteCount);
