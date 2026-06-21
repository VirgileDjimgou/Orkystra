namespace Orkystra.Contracts.Connectors;

public sealed record ProviderSchemaField(
    string Name,
    string Type,
    bool Required,
    string CanonicalMapping,
    string Description);
