namespace Orkystra.Integration.Tests;

public sealed class PersistenceApiFlowsTests : IntegrationTestBase
{
    public PersistenceApiFlowsTests(OrkystraWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Get_control_tower_overview_returns_data()
    {
        var response = await Client.GetAsync("/api/control-tower/overview");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var doc = await DeserializeDocumentAsync(response);
        Assert.True(doc.RootElement.TryGetProperty("tenantId", out var tenantId));
        Assert.False(string.IsNullOrEmpty(tenantId.GetString()));
        Assert.True(doc.RootElement.TryGetProperty("warehouses", out var warehouses));
        Assert.True(warehouses.GetArrayLength() >= 1);
        Assert.True(doc.RootElement.TryGetProperty("routes", out var routes));
        Assert.True(routes.GetArrayLength() >= 1);
        Assert.True(doc.RootElement.TryGetProperty("alerts", out _));
        Assert.True(doc.RootElement.TryGetProperty("eventFeed", out _));
    }

    [Fact]
    public async Task Get_warehouses_returns_list()
    {
        var response = await Client.GetAsync("/api/warehouses");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var doc = await DeserializeDocumentAsync(response);
        Assert.True(doc.RootElement.GetArrayLength() >= 1);

        var firstId = doc.RootElement[0].GetProperty("warehouseId").GetGuid();
        var detailResponse = await Client.GetAsync($"/api/warehouses/{firstId:D}");
        Assert.Equal(System.Net.HttpStatusCode.OK, detailResponse.StatusCode);
    }

    [Fact]
    public async Task Get_transport_routes_returns_list()
    {
        var response = await Client.GetAsync("/api/transport/routes");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var doc = await DeserializeDocumentAsync(response);
        Assert.True(doc.RootElement.GetArrayLength() >= 1);

        var firstId = doc.RootElement[0].GetProperty("routeId").GetGuid();
        var detailResponse = await Client.GetAsync($"/api/transport/routes/{firstId:D}");
        Assert.Equal(System.Net.HttpStatusCode.OK, detailResponse.StatusCode);
    }

    [Fact]
    public async Task Get_gps_board_returns_operator_fleet_view()
    {
        var publishResponse = await Client.PostAsync("/api/gps/positions/publish", content: null);
        Assert.Equal(System.Net.HttpStatusCode.Accepted, publishResponse.StatusCode);

        var response = await Client.GetAsync("/api/gps/board");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var doc = await DeserializeDocumentAsync(response);
        Assert.True(doc.RootElement.TryGetProperty("positionCount", out var positionCount));
        Assert.True(positionCount.GetInt32() >= 0);
        Assert.True(doc.RootElement.TryGetProperty("summary", out var summary));
        Assert.False(string.IsNullOrWhiteSpace(summary.GetString()));
        Assert.True(doc.RootElement.TryGetProperty("positions", out var positions));
        Assert.Equal(positionCount.GetInt32(), positions.GetArrayLength());
        Assert.True(doc.RootElement.TryGetProperty("focusSummary", out _));

        if (positions.GetArrayLength() > 0)
        {
            var firstPosition = positions[0];
            Assert.True(firstPosition.TryGetProperty("truckReference", out _));
            Assert.True(firstPosition.TryGetProperty("alertPosture", out _));
            Assert.True(firstPosition.TryGetProperty("freshnessPosture", out _));
        }
    }

    [Fact]
    public async Task Get_provider_catalog_returns_list()
    {
        var response = await Client.GetAsync("/api/providers/catalog");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var doc = await DeserializeDocumentAsync(response);
        Assert.True(doc.RootElement.TryGetProperty("providers", out var providers));
        Assert.True(providers.GetArrayLength() >= 1);
        var transportProvider = providers.EnumerateArray()
            .First(provider => provider.GetProperty("providerId").GetString() == "rest-transport-adapter");
        var configuration = transportProvider.GetProperty("configuration");
        Assert.Equal("dry-run", configuration.GetProperty("writebackMode").GetString());
        Assert.Equal("Auth Required", configuration.GetProperty("writebackReadiness").GetString());
    }
}
