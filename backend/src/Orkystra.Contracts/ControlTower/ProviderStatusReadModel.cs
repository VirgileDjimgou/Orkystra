namespace Orkystra.Contracts.ControlTower;

public sealed record ProviderStatusReadModel(
    string ProviderId,
    string ProviderName,
    string Domain,
    string HealthStatus,
    string SyncStatus,
    DateTimeOffset? LastSuccessfulSyncAt,
    DateTimeOffset? LastAttemptedSyncAt,
    string Summary);
