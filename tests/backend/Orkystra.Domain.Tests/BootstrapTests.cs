using Orkystra.Api.Bootstrap;
using Orkystra.Api.Eventing;
using Orkystra.Api.Persistence;
using Orkystra.Contracts.Bootstrap;
using Microsoft.Extensions.Configuration;

namespace Orkystra.Domain.Tests;

public sealed class BootstrapTests
{
    [Fact]
    public void BootstrapDemoRequest_Default_has_expected_values()
    {
        var request = BootstrapDemoRequest.Default;

        Assert.Equal("Seeded Demo", request.ScenarioName);
        Assert.Equal(42, request.Seed);
        Assert.Equal(15, request.AdvanceMinutes);
        Assert.True(request.IncludeDisruption);
    }

    [Fact]
    public void SanityCheckResponse_all_healthy_when_all_components_healthy()
    {
        var components = new SanityComponentStatus[]
        {
            new("api", true, "API is responding", 1),
            new("mqtt", true, "MQTT connected", 5),
            new("persistence", true, "Persistence store is responding", 3),
        };

        var response = new SanityCheckResponse("1.0.0", components.All(c => c.Healthy), components, DateTimeOffset.UtcNow);

        Assert.True(response.AllHealthy);
        Assert.Equal(3, response.Components.Count);
    }

    [Fact]
    public void SanityCheckResponse_not_healthy_when_one_component_fails()
    {
        var components = new SanityComponentStatus[]
        {
            new("api", true, "API is responding", 1),
            new("mqtt", false, "Cannot reach broker", 5000),
            new("persistence", true, "Persistence store is responding", 3),
        };

        var response = new SanityCheckResponse("1.0.0", components.All(c => c.Healthy), components, DateTimeOffset.UtcNow);

        Assert.False(response.AllHealthy);
    }

    [Fact]
    public async Task SanityCheckService_api_check_always_returns_healthy()
    {
        var eventBackboneOptions = new EventBackboneOptions();
        var persistenceOptions = new OperationalPersistenceOptions();
        var persistenceStore = new SqliteOperationalPersistenceStore(
            Microsoft.Extensions.Options.Options.Create(persistenceOptions),
            ".");
        var config = new ConfigurationBuilder().Build();

        var service = new SanityCheckService(eventBackboneOptions, persistenceOptions, persistenceStore, config);
        var result = await service.RunAsync();

        var apiStatus = result.Components.First(c => c.Component == "api");
        Assert.True(apiStatus.Healthy);
        Assert.Equal("API is responding", apiStatus.Message);
    }
}
