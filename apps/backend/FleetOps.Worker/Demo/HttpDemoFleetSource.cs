using System.Net.Http.Json;
using FleetOps.Core.Modules.Demo;
using Microsoft.Extensions.Options;

namespace FleetOps.Worker.Demo;

public sealed class HttpDemoFleetSource(
    IHttpClientFactory httpClientFactory,
    IOptions<DemoEngineOptions> options) : IDemoFleetSource
{
    public async Task<DemoFleetDefinition> LoadAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.OrganizationSlug))
            throw new InvalidOperationException("DemoEngine:OrganizationSlug is required.");
        if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
            throw new InvalidOperationException("DemoEngine:ApiBaseUrl is required.");

        var client = httpClientFactory.CreateClient(nameof(HttpDemoFleetSource));
        client.BaseAddress = new Uri(settings.ApiBaseUrl, UriKind.Absolute);
        InternalApiHeader.Apply(client, settings.InternalApiKey);
        var slug = Uri.EscapeDataString(settings.OrganizationSlug.Trim());
        var response = await client.GetFromJsonAsync<ScenarioResponse>($"/api/internal/v1/tracking/scenarios/{slug}?maxVehicles=20", cancellationToken)
            ?? throw new InvalidOperationException("Demo fleet discovery returned no content.");
        return new(response.OrganizationId, response.Vehicles.Select(vehicle => new DemoVehicleBinding(vehicle.VehicleId, vehicle.DeviceId)).ToList());
    }

    private sealed record ScenarioResponse(Guid OrganizationId, IReadOnlyList<ScenarioVehicleResponse> Vehicles);
    private sealed record ScenarioVehicleResponse(Guid VehicleId, string DeviceId);
}
