namespace Orkystra.Integration.Tests;

public sealed class HealthEndpointsTests : IntegrationTestBase
{
    public HealthEndpointsTests(OrkystraWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Get_liveness_returns_healthy()
    {
        var response = await Client.GetAsync("/health/live");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var doc = await DeserializeDocumentAsync(response);
        Assert.Equal("orkystra-api", doc.RootElement.GetProperty("service").GetString());
        Assert.Equal("healthy", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Get_readiness_returns_ready()
    {
        var response = await Client.GetAsync("/health/ready");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var doc = await DeserializeDocumentAsync(response);
        Assert.Equal("orkystra-api", doc.RootElement.GetProperty("service").GetString());
        Assert.Equal("ready", doc.RootElement.GetProperty("status").GetString());
        Assert.True(doc.RootElement.TryGetProperty("dependencies", out _));
    }

    [Fact]
    public async Task Get_sanity_returns_all_components()
    {
        var response = await Client.GetAsync("/health/sanity");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var doc = await DeserializeDocumentAsync(response);
        Assert.True(doc.RootElement.TryGetProperty("components", out var components));
        Assert.NotEqual(0, components.GetArrayLength());
        Assert.True(doc.RootElement.TryGetProperty("allHealthy", out _));
    }
}
