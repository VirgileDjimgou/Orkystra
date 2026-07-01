namespace Orkystra.Integration.Tests;

public sealed class TransportSyncTests : IntegrationTestBase
{
    public TransportSyncTests(OrkystraWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Get_sync_status_before_import_returns_ok()
    {
        var response = await Client.GetAsync("/api/transport/sync-status");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var doc = await DeserializeDocumentAsync(response);
        Assert.True(doc.RootElement.TryGetProperty("providerId", out var providerId));
        Assert.NotNull(providerId.GetString());
        Assert.NotEmpty(providerId.GetString()!);
    }

    [Fact]
    public async Task Post_transport_sync_imports_routes_and_returns_status()
    {
        var response = await Client.PostAsync("/api/transport/sync", null);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var doc = await DeserializeDocumentAsync(response);
        Assert.True(doc.RootElement.TryGetProperty("syncStatus", out var syncStatus));
        Assert.False(string.IsNullOrEmpty(syncStatus.GetString()));
    }

    [Fact]
    public async Task Transport_sync_persists_workflow_run()
    {
        await Client.PostAsync("/api/transport/sync", null);

        var workflowsResponse = await Client.GetAsync("/observability/persistence/workflows?workflowKind=transport-sync-import");
        Assert.Equal(System.Net.HttpStatusCode.OK, workflowsResponse.StatusCode);
        var doc = await DeserializeDocumentAsync(workflowsResponse);
        Assert.True(doc.RootElement.TryGetProperty("entries", out var entries));
        Assert.True(entries.GetArrayLength() >= 1);
    }
}
