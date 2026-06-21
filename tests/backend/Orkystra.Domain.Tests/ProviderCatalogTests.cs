using Microsoft.Extensions.Options;
using Orkystra.Api.Connectors;

namespace Orkystra.Domain.Tests;

public sealed class ProviderCatalogTests
{
    [Fact]
    public async Task ProviderCatalogService_builds_catalog_from_registered_connectors()
    {
        var service = new ProviderCatalogService(Options.Create(new ProviderRuntimeOptions
        {
            Providers =
            [
                new ProviderRuntimeSettings
                {
                    ProviderId = "csv-warehouse-import",
                    Enabled = true,
                    Environment = "local-demo",
                    Settings = new Dictionary<string, string>
                    {
                        ["sourcePath"] = "data/imports/warehouse-demo.csv",
                        ["importSchedule"] = "manual"
                    }
                },
                new ProviderRuntimeSettings
                {
                    ProviderId = "rest-transport-adapter",
                    Enabled = true,
                    Environment = "sandbox",
                    Settings = new Dictionary<string, string>
                    {
                        ["baseUrl"] = "https://sandbox.example.invalid/transport",
                        ["authMode"] = "api-key"
                    }
                },
                new ProviderRuntimeSettings
                {
                    ProviderId = "gps-telematics-adapter",
                    Enabled = true,
                    Environment = "local-demo",
                    Settings = new Dictionary<string, string>
                    {
                        ["streamTopic"] = "fleet/gps/demo",
                        ["snapshotIntervalSeconds"] = "15"
                    }
                }
            ]
        }));

        var catalog = await service.BuildCatalogAsync();

        Assert.True(catalog.GeneratedAtUtc <= DateTimeOffset.UtcNow);
        Assert.Equal(3, catalog.Providers.Count);
        Assert.Contains(catalog.Providers, provider => provider.ProviderId == "csv-warehouse-import");
        Assert.Contains(catalog.Providers, provider => provider.Schema.ResourceName == "gps-position-event");
        Assert.Contains(catalog.Providers, provider => provider.SupportedReadModels.Contains("RouteSummaryReadModel"));
        Assert.Contains(catalog.Providers, provider => provider.Configuration.Readiness == "Configured");
        Assert.Contains(catalog.Providers, provider => provider.Configuration.ConfiguredFields.Contains("baseUrl"));
    }
}
