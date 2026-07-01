using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Orkystra.Contracts.Bootstrap;

namespace Orkystra.Integration.Tests;

public sealed class BootstrapIntegrationTests : IntegrationTestBase
{
    public BootstrapIntegrationTests(OrkystraWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Post_bootstrap_demo_creates_scenario()
    {
        var requestContent = new StringContent(
            JsonSerializer.Serialize(BootstrapDemoRequest.Default),
            Encoding.UTF8,
            "application/json");

        var response = await Client.PostAsync("/api/bootstrap/demo", requestContent);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var doc = await DeserializeDocumentAsync(response);
        Assert.True(doc.RootElement.TryGetProperty("scenario", out var scenario));
        Assert.True(scenario.TryGetProperty("scenarioId", out var scenarioId));
        Assert.NotEqual(Guid.Empty, scenarioId.GetGuid());
        Assert.True(doc.RootElement.TryGetProperty("bootstrappedAtUtc", out _));
    }
}
