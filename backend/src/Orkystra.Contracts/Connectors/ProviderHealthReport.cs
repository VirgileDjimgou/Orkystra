namespace Orkystra.Contracts.Connectors;

public sealed record ProviderHealthReport(
    string ProviderId,
    string ProviderName,
    ProviderHealthStatus Status,
    DateTimeOffset CheckedAt,
    string Summary,
    IReadOnlyCollection<string> Signals);
