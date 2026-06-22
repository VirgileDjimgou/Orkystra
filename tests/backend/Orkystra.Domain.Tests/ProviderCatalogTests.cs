using Microsoft.Extensions.Options;
using Orkystra.Api.Connectors;

namespace Orkystra.Domain.Tests;

public sealed class ProviderCatalogTests
{
    [Fact]
    public async Task ProviderCatalogService_builds_catalog_from_registered_connectors()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("orkystra-provider-catalog-tests");

        try
        {
            var store = BuildRuntimeStore(tempDirectory.FullName);
            var service = new ProviderCatalogService(store);

            var catalog = await service.BuildCatalogAsync();

            Assert.True(catalog.GeneratedAtUtc <= DateTimeOffset.UtcNow);
            Assert.Equal(3, catalog.Providers.Count);
            Assert.Contains(catalog.Providers, provider => provider.ProviderId == "csv-warehouse-import");
            Assert.Contains(catalog.Providers, provider => provider.Schema.ResourceName == "gps-position-event");
            Assert.Contains(catalog.Providers, provider => provider.SupportedReadModels.Contains("RouteSummaryReadModel"));
            Assert.Contains(catalog.Providers, provider => provider.SupportedReadModels.Contains("RouteDetailReadModel"));
            Assert.Contains(catalog.Providers, provider => provider.Configuration.Readiness == "Configured" || provider.Configuration.Readiness == "Auth Key Missing");
            Assert.Contains(catalog.Providers, provider => provider.Configuration.ConfiguredFields.Contains("baseUrl"));
            Assert.Contains(catalog.Providers, provider => provider.Configuration.Settings.Any(setting => setting.Key == "sourcePath" && setting.Value.Contains("warehouse-demo")));
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Fact]
    public async Task ProviderCatalogService_exposes_auth_mode_for_rest_transport_provider()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("orkystra-provider-catalog-tests");

        try
        {
            var store = BuildRuntimeStore(tempDirectory.FullName);
            var service = new ProviderCatalogService(store);

            var catalog = await service.BuildCatalogAsync();

            var transportProvider = catalog.Providers
                .Single(provider => provider.ProviderId == "rest-transport-adapter");

            Assert.Equal("api-key", transportProvider.Configuration.AuthMode);
            Assert.False(transportProvider.Configuration.AuthConfigured); // No secret store injected
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Fact]
    public async Task ProviderCatalogService_reports_auth_configured_when_secret_store_has_api_key()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("orkystra-provider-catalog-tests");

        try
        {
            var secretsPath = Path.Combine(tempDirectory.FullName, "appsettings.Secrets.local.json");
            var secretStore = new ProviderSecretStore(secretsPath);
            await secretStore.UpdateSecretAsync("rest-transport-adapter", "apiKey", "catalog-test-api-key");

            var runtimeStore = BuildRuntimeStore(tempDirectory.FullName);
            var registryFactory = new ProviderRegistryFactory();
            var service = new ProviderCatalogService(runtimeStore, secretStore, registryFactory);

            var catalog = await service.BuildCatalogAsync();

            var transportProvider = catalog.Providers
                .Single(provider => provider.ProviderId == "rest-transport-adapter");

            Assert.Equal("api-key", transportProvider.Configuration.AuthMode);
            Assert.True(transportProvider.Configuration.AuthConfigured);
            // All required non-secret fields are present AND the API key is configured → Configured.
            Assert.Equal("Configured", transportProvider.Configuration.Readiness);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Fact]
    public async Task ProviderCatalogService_readiness_is_auth_key_missing_when_key_not_provided()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("orkystra-provider-catalog-tests");

        try
        {
            var secretsPath = Path.Combine(tempDirectory.FullName, "appsettings.Secrets.local.json");
            var secretStore = new ProviderSecretStore(secretsPath); // empty — no key stored
            var runtimeStore = BuildRuntimeStore(tempDirectory.FullName);
            var registryFactory = new ProviderRegistryFactory();
            var service = new ProviderCatalogService(runtimeStore, secretStore, registryFactory);

            var catalog = await service.BuildCatalogAsync();

            var transportProvider = catalog.Providers
                .Single(provider => provider.ProviderId == "rest-transport-adapter");

            // base config is present (baseUrl + authMode) but key is missing → Auth Key Missing
            Assert.Equal("Auth Key Missing", transportProvider.Configuration.Readiness);
            Assert.False(transportProvider.Configuration.AuthConfigured);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    private static ProviderRuntimeStore BuildRuntimeStore(string tempDirectoryPath)
    {
        return new ProviderRuntimeStore(
            Options.Create(new ProviderRuntimeOptions
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
            }),
            Path.Combine(tempDirectoryPath, "appsettings.Local.json"));
    }
}
