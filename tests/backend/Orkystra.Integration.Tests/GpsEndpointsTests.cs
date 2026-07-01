namespace Orkystra.Integration.Tests;

public sealed class GpsEndpointsTests : IntegrationTestBase
{
    public GpsEndpointsTests(OrkystraWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Get_gps_positions_returns_ok()
    {
        var response = await Client.GetAsync("/api/gps/positions");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var doc = await DeserializeDocumentAsync(response);
        Assert.True(doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array);
    }
}
