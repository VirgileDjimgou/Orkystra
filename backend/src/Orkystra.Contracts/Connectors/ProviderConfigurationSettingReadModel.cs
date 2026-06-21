namespace Orkystra.Contracts.Connectors;

public sealed record ProviderConfigurationSettingReadModel(
    string Key,
    string Value,
    bool Required);
