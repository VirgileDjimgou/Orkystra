namespace Orkystra.Api.Connectors;

public sealed class ProviderRuntimeOptions
{
    public const string SectionName = "ProviderRuntime";

    public List<ProviderRuntimeSettings> Providers { get; init; } = [];
}

public sealed class ProviderRuntimeSettings
{
    public string ProviderId { get; init; } = string.Empty;

    public bool Enabled { get; init; } = true;

    public string Environment { get; init; } = "local-demo";

    public Dictionary<string, string> Settings { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
