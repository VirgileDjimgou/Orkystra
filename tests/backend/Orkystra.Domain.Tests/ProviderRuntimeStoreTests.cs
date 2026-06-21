using Microsoft.Extensions.Options;
using Orkystra.Api.Connectors;
using Orkystra.Contracts.Connectors;

namespace Orkystra.Domain.Tests;

public sealed class ProviderRuntimeStoreTests
{
    [Fact]
    public async Task UpdateAsync_persists_local_configuration_for_known_provider()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("orkystra-provider-runtime-tests");

        try
        {
            var localConfigurationPath = Path.Combine(tempDirectory.FullName, "appsettings.Local.json");
            var store = new ProviderRuntimeStore(
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
                        }
                    ]
                }),
                localConfigurationPath);

            var updated = await store.UpdateAsync(
                "csv-warehouse-import",
                new UpdateProviderConfigurationRequest(
                    false,
                    "sandbox",
                    new Dictionary<string, string>
                    {
                        ["sourcePath"] = "data/imports/warehouse-refresh.csv",
                        ["importSchedule"] = "hourly"
                    }));

            Assert.False(updated.Enabled);
            Assert.Equal("sandbox", updated.Environment);
            Assert.Equal("hourly", updated.Settings["importSchedule"]);
            Assert.True(File.Exists(localConfigurationPath));

            var persisted = await File.ReadAllTextAsync(localConfigurationPath);
            Assert.Contains("\"ProviderRuntime\"", persisted);
            Assert.Contains("warehouse-refresh.csv", persisted);
            Assert.Contains("\"Enabled\": false", persisted);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Fact]
    public async Task UpdateAsync_rejects_unknown_fields()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("orkystra-provider-runtime-tests");

        try
        {
            var store = new ProviderRuntimeStore(
                Options.Create(new ProviderRuntimeOptions()),
                Path.Combine(tempDirectory.FullName, "appsettings.Local.json"));

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => store.UpdateAsync(
                "rest-transport-adapter",
                new UpdateProviderConfigurationRequest(
                    true,
                    "sandbox",
                    new Dictionary<string, string>
                    {
                        ["baseUrl"] = "https://sandbox.example.invalid/transport",
                        ["password"] = "hidden"
                    })));

            Assert.Contains("Unsupported configuration fields", exception.Message);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }
}
