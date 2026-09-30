using System.Net.Http.Json;
using FleetOps.Core.Modules.Demo;
using Microsoft.Extensions.Options;

namespace FleetOps.Worker.Demo;

public sealed class HttpDemoTelemetryEmitter(
    IHttpClientFactory httpClientFactory,
    IOptions<DemoEngineOptions> options) : IDemoTelemetryEmitter
{
    public async Task EmitAsync(DemoTelemetryEvent telemetry, CancellationToken cancellationToken)
    {
        var baseUrl = options.Value.ApiBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("DemoEngine:ApiBaseUrl is required when the Demo engine is enabled.");
        }

        var client = httpClientFactory.CreateClient(nameof(HttpDemoTelemetryEmitter));
        client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        InternalApiHeader.Apply(client, options.Value.InternalApiKey);
        using var response = await client.PostAsJsonAsync("/api/internal/v1/tracking/events", telemetry, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
