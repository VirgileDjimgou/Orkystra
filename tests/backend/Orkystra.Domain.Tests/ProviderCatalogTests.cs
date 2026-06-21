using Orkystra.Api.Connectors;

namespace Orkystra.Domain.Tests;

public sealed class ProviderCatalogTests
{
    [Fact]
    public async Task ProviderCatalogService_builds_catalog_from_registered_connectors()
    {
        var service = new ProviderCatalogService();

        var catalog = await service.BuildCatalogAsync();

        Assert.True(catalog.GeneratedAtUtc <= DateTimeOffset.UtcNow);
        Assert.Equal(3, catalog.Providers.Count);
        Assert.Contains(catalog.Providers, provider => provider.ProviderId == "csv-warehouse-import");
        Assert.Contains(catalog.Providers, provider => provider.Schema.ResourceName == "gps-position-event");
        Assert.Contains(catalog.Providers, provider => provider.SupportedReadModels.Contains("RouteSummaryReadModel"));
    }
}
