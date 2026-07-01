using System.Net.Http.Headers;
using System.Text.Json;

namespace Orkystra.Integration.Tests;

public abstract class IntegrationTestBase : IClassFixture<OrkystraWebApplicationFactory>, IDisposable
{
    protected IntegrationTestBase(OrkystraWebApplicationFactory factory)
    {
        Client = factory.CreateClient();
        Client.DefaultRequestHeaders.Add("X-Api-Key", "integration-test-key");
        Client.DefaultRequestHeaders.Add("X-Tenant-Id", "integration-test-tenant");
        Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    protected HttpClient Client { get; }

    protected static async Task<T> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    protected static async Task<JsonDocument> DeserializeDocumentAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }

    public void Dispose()
    {
        Client.Dispose();
    }
}
