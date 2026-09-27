using System.Net.Http.Headers;
using System.Net.Http.Json;
using FleetOps.Core.Modules.Demo;
using Microsoft.Extensions.Options;

namespace FleetOps.Worker.Demo;

public sealed class HttpAgentActivitySink(IHttpClientFactory clients, IOptions<DemoEngineOptions> options) : IAgentActivitySink
{
    public async Task RecordAsync(AgentActivity activity, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl) || string.IsNullOrWhiteSpace(settings.ApiAccessToken))
            throw new InvalidOperationException("Agent activity requires DemoEngine API URL and operator access token.");
        var client = clients.CreateClient(nameof(HttpAgentActivitySink));
        client.BaseAddress = new Uri(settings.ApiBaseUrl, UriKind.Absolute);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiAccessToken);
        using var response = await client.PostAsJsonAsync("/api/v1/demo/agent-activities", new
        {
            activity.AgentId,
            activity.DriverId,
            activity.VehicleId,
            activity.MissionId,
            activity.StopId,
            activity.Sequence,
            activity.ObservedState,
            activity.Policy,
            activity.Action,
            activity.ResultCode,
            activity.ResultMessage,
            activity.OccurredAtUtc
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
