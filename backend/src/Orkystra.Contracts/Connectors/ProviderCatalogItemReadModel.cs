namespace Orkystra.Contracts.Connectors;

public sealed record ProviderCatalogItemReadModel(
    string ProviderId,
    string ProviderName,
    string Domain,
    string Kind,
    ProviderHealthReport Health,
    ProviderSyncStatus SyncStatus,
    ProviderCapabilitySet Capabilities,
    ProviderSchemaDescription Schema,
    IReadOnlyCollection<string> SupportedReadModels);
