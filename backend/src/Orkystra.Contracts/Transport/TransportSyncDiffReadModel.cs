namespace Orkystra.Contracts.Transport;

public sealed record TransportSyncDiffReadModel(
    bool HasComparableHistory,
    string Detail,
    DateTimeOffset? LatestImportedAtUtc,
    DateTimeOffset? PreviousImportedAtUtc,
    int LatestRouteCount,
    int PreviousRouteCount,
    int AddedRouteCount,
    int RemovedRouteCount,
    int ChangedRouteCount,
    IReadOnlyCollection<TransportSyncRouteDiffItemReadModel> RouteDiffs);
