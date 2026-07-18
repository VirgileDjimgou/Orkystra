using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace FleetOps.Simulation;

public sealed record SimulationSession(string Role, string OrganizationName, string AccessToken, Guid? DriverId);

public sealed class FleetOpsSimulationClient(HttpClient http)
{
    public async Task<SimulationSession> LoginAsync(SimulationAccount account, string expectedOrganization, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { account.Email, account.Password },
            cancellationToken);
        var payload = await ReadRequiredAsync(response, HttpStatusCode.OK, "login", cancellationToken);
        var user = payload["user"] ?? throw new InvalidOperationException("Login response has no user.");
        var roles = user["roles"]?.AsArray().Select(x => x?.GetValue<string>()).ToArray() ?? [];
        if (!roles.Contains(account.Role, StringComparer.Ordinal) || user["organizationName"]?.GetValue<string>() != expectedOrganization)
        {
            throw new InvalidOperationException($"{account.Email} did not resolve to {expectedOrganization}/{account.Role}.");
        }

        return new SimulationSession(
            account.Role,
            expectedOrganization,
            payload["accessToken"]?.GetValue<string>() ?? throw new InvalidOperationException("Login token is missing."),
            Guid.TryParse(user["driverId"]?.ToString(), out var driverId) ? driverId : null);
    }

    public Task<JsonNode> GetAsync(SimulationSession session, string path, CancellationToken cancellationToken) =>
        SendAsync(session, HttpMethod.Get, path, null, HttpStatusCode.OK, cancellationToken);

    public Task<JsonNode> PostAsync(SimulationSession session, string path, object? body, CancellationToken cancellationToken) =>
        SendAsync(session, HttpMethod.Post, path, body, HttpStatusCode.OK, cancellationToken);

    public Task<JsonNode> PostCreatedAsync(SimulationSession session, string path, object body, CancellationToken cancellationToken) =>
        SendAsync(session, HttpMethod.Post, path, body, HttpStatusCode.Created, cancellationToken);

    public Task<JsonNode> PutAsync(SimulationSession session, string path, object body, CancellationToken cancellationToken) =>
        SendAsync(session, HttpMethod.Put, path, body, HttpStatusCode.OK, cancellationToken);

    public Task<JsonNode> PutNoContentAsync(SimulationSession session, string path, object body, CancellationToken cancellationToken) =>
        SendAsync(session, HttpMethod.Put, path, body, HttpStatusCode.NoContent, cancellationToken);

    public async Task AssertStatusAsync(
        SimulationSession session,
        HttpMethod method,
        string path,
        HttpStatusCode expectedStatus,
        CancellationToken cancellationToken)
    {
        using var request = BuildRequest(session, method, path, null);
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode != expectedStatus)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Expected {(int)expectedStatus} for {method} {path}, got {(int)response.StatusCode}: {body}");
        }
    }

    public async Task<JsonNode> SendAnonymousAsync(
        HttpMethod method,
        string path,
        object? body,
        HttpStatusCode expectedStatus,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = body is null ? null : JsonContent.Create(body),
        };
        using var response = await http.SendAsync(request, cancellationToken);
        return await ReadRequiredAsync(response, expectedStatus, $"{method} {path}", cancellationToken);
    }

    private async Task<JsonNode> SendAsync(
        SimulationSession session,
        HttpMethod method,
        string path,
        object? body,
        HttpStatusCode expectedStatus,
        CancellationToken cancellationToken)
    {
        using var request = BuildRequest(session, method, path, body);
        using var response = await http.SendAsync(request, cancellationToken);
        return await ReadRequiredAsync(response, expectedStatus, $"{method} {path}", cancellationToken);
    }

    private static HttpRequestMessage BuildRequest(SimulationSession session, HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private static async Task<JsonNode> ReadRequiredAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode != expectedStatus)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"{operation} returned {(int)response.StatusCode}, expected {(int)expectedStatus}: {error}");
        }

        if (response.Content.Headers.ContentLength == 0 || expectedStatus == HttpStatusCode.NoContent)
        {
            return new JsonObject();
        }

        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(text) ? new JsonObject() : JsonNode.Parse(text) ?? new JsonObject();
    }
}
