namespace Orkystra.Contracts.Connectors;

public sealed record ProviderConfigurationSummaryReadModel(
    bool Enabled,
    string Environment,
    string Readiness,
    IReadOnlyCollection<string> ConfiguredFields,
    IReadOnlyCollection<string> MissingFields);
