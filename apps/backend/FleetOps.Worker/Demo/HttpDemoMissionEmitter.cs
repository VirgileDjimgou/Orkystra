using System.Net.Http.Headers;
using System.Net.Http.Json;
using FleetOps.Core.Modules.Demo;
using FleetOps.Core.Modules.Dispatch;
using Microsoft.Extensions.Options;

namespace FleetOps.Worker.Demo;

public sealed class HttpDemoMissionEmitter(
    IHttpClientFactory httpClientFactory,
    IOptions<DemoEngineOptions> options) : IDemoMissionEmitter
{
    public async Task<DemoMissionActionResult> TransitionAsync(DemoMissionAction action, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl) || string.IsNullOrWhiteSpace(settings.ApiAccessToken))
            throw new InvalidOperationException("Demo mission actions require an API URL and a short-lived access token.");
        var client = httpClientFactory.CreateClient(nameof(HttpDemoMissionEmitter));
        client.BaseAddress = new Uri(settings.ApiBaseUrl, UriKind.Absolute);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiAccessToken);
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/dispatch/missions/{action.MissionId}/status",
            new TransitionRequest(action.TargetStatus, action.RowVersion),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<TransitionResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Mission transition returned no content.");
        return new(result.Id, result.Status, result.RowVersion);
    }

    private sealed record TransitionRequest(MissionStatus Status, long RowVersion);
    private sealed record TransitionResponse(Guid Id, MissionStatus Status, long RowVersion);
}
