namespace Orkystra.Contracts.Connectors;

public sealed record UpdateProviderConfigurationRequest(
    bool Enabled,
    string Environment,
    IReadOnlyDictionary<string, string> Settings);
