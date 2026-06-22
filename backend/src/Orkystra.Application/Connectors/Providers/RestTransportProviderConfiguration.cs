namespace Orkystra.Application.Connectors.Providers;

public sealed record RestTransportProviderConfiguration(
    bool Enabled,
    string Environment,
    string? BaseUrl,
    string AuthMode,
    string? ApiKey)
{
    public static RestTransportProviderConfiguration LocalDemo { get; } =
        new(
            true,
            "local-demo",
            null,
            "none",
            null);
}
