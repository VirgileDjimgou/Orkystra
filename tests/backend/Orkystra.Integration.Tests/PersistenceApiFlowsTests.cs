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
    public async Task Get_provider_catalog_returns_list()
    {
        var response = await Client.GetAsync("/api/providers/catalog");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var doc = await DeserializeDocumentAsync(response);
        Assert.True(doc.RootElement.TryGetProperty("providers", out var providers));
        Assert.True(providers.GetArrayLength() >= 1);
    }
}
