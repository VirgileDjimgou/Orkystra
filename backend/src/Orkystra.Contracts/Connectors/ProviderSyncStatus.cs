namespace Orkystra.Contracts.Connectors;

public sealed record ProviderSyncStatus(
    string ProviderId,
    string Mode,
    DateTimeOffset? LastSuccessfulSyncAt,
    DateTimeOffset? LastAttemptedSyncAt,
    string Status,
    string? Detail);
