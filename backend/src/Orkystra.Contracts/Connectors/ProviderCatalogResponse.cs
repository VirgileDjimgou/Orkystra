namespace Orkystra.Contracts.Connectors;

public sealed record ProviderCatalogResponse(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyCollection<ProviderCatalogItemReadModel> Providers);
