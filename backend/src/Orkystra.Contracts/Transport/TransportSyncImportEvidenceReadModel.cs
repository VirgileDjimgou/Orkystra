namespace Orkystra.Contracts.Transport;

public sealed record TransportSyncImportEvidenceReadModel(
    TransportSyncStatusReadModel SyncStatus,
    IReadOnlyCollection<RouteSummaryReadModel> Routes);
