using Orkystra.Contracts.Connectors;

namespace Orkystra.Contracts.Transport;

public sealed record TransportSyncStatusReadModel(
    string ProviderId,
    string Source,
    bool LiveImport,
    bool HasPersistedSnapshot,
    int ImportedRouteCount,
    IReadOnlyCollection<Guid> ImportedRouteIds,
    IReadOnlyCollection<string> ImportedRouteReferences,
    DateTimeOffset? LastImportedAtUtc,
    DateTimeOffset? LastSuccessfulSyncAt,
    DateTimeOffset? LastAttemptedSyncAt,
    string SyncStatus,
    string? SyncDetail,
    ProviderHealthReport Health);
