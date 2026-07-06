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

    [Fact]
    public async Task Get_persistence_provider_returns_sqlite_diagnostics()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/observability/persistence/provider");
        request.Headers.Add("X-Api-Key", "integration-test-key");

        var response = await Client.SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var doc = await DeserializeDocumentAsync(response);
        Assert.Equal("sqlite", doc.RootElement.GetProperty("provider").GetString());
        Assert.Equal("local-default", doc.RootElement.GetProperty("posture").GetString());
        Assert.True(doc.RootElement.GetProperty("healthy").GetBoolean());
        Assert.True(doc.RootElement.TryGetProperty("connectionTarget", out _));
    }

    [Fact]
    public async Task Get_support_bundle_returns_aggregated_observability_evidence()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/observability/support-bundle?count=4");
        request.Headers.Add("X-Api-Key", "integration-test-key");
        request.Headers.Add("X-Tenant-Id", "local-demo-tenant");

        var response = await Client.SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var doc = await DeserializeDocumentAsync(response);
        Assert.Equal("local-demo-tenant", doc.RootElement.GetProperty("tenantId").GetString());
        Assert.True(doc.RootElement.TryGetProperty("summary", out var summary));
        Assert.True(summary.TryGetProperty("posture", out _));
        Assert.True(summary.TryGetProperty("escalationTarget", out _));
        Assert.True(summary.TryGetProperty("signals", out _));
        Assert.True(summary.TryGetProperty("artifactChecklist", out _));
        Assert.True(doc.RootElement.TryGetProperty("collections", out var collections));
        Assert.True(collections.TryGetProperty("projections", out _));
        Assert.True(collections.TryGetProperty("workflows", out _));
        Assert.True(collections.TryGetProperty("audits", out _));
    }
}
