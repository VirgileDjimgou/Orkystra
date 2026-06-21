namespace Orkystra.Contracts.Connectors;

public sealed record ProviderSchemaDescription(
    string ProviderId,
    string ResourceName,
    IReadOnlyCollection<ProviderSchemaField> Fields);
