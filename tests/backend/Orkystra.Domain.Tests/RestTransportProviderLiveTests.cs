using System.Net;
using System.Text;
using Orkystra.Application.Connectors.Providers;

namespace Orkystra.Domain.Tests;

public sealed class RestTransportProviderLiveTests
{
    [Fact]
    public async Task ReadRoutesAsync_uses_live_upstream_when_runtime_endpoint_is_valid()
    {
        var handler = new StubHttpMessageHandler((request) =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (path.EndsWith("/routes", StringComparison.OrdinalIgnoreCase))
            {
                return JsonResponse("""
                [
                  {
                    "routeId": "e9b0b451-df34-4607-9ee7-e8f593ab6b61",
                    "reference": "LIVE-901",
                    "truckId": "ea018d54-b1d7-44f6-9fe5-64bddd16e414",
                    "truckReference": "TRK-LIVE-1",
                    "status": "On time",
                    "stopCount": 2,
                    "shipmentCount": 5,
                    "completedDeliveryCount": 1
                  }
                ]
                """);
            }

            if (path.EndsWith("/routes/details", StringComparison.OrdinalIgnoreCase))
            {
                return JsonResponse("""
                [
                  {
                    "routeId": "e9b0b451-df34-4607-9ee7-e8f593ab6b61",
                    "reference": "LIVE-901",
                    "truckId": "ea018d54-b1d7-44f6-9fe5-64bddd16e414",
                    "truckReference": "TRK-LIVE-1",
                    "driverName": "Live Driver",
                    "status": "On time",
                    "truckStatus": "In transit",
                    "truckCapacityKilograms": 700,
                    "totalLoadKilograms": 420,
                    "stopCount": 2,
                    "shipmentCount": 5,
                    "completedDeliveryCount": 1,
                    "updatedAtUtc": "2026-06-22T08:00:00Z",
                    "stops": [
                      { "sequence": 1, "name": "Live Hub", "coordinateLabel": "48.8566, 2.3522", "timeWindowLabel": "08:00-09:00" },
                      { "sequence": 2, "name": "Live Store", "coordinateLabel": "48.8800, 2.4000", "timeWindowLabel": "09:15-10:00" }
                    ],
                    "shipments": [
                      { "reference": "LIVE-SHIP-01", "status": "Loaded", "loadWeightKilograms": 120, "orderReference": "LIVE-ORDER-01" }
                    ],
                    "deliveries": [
                      { "reference": "LIVE-DLV-01", "stopSequence": 2, "stopName": "Live Store", "shipmentReference": "LIVE-SHIP-01", "status": "Pending" }
                    ]
                  }
                ]
                """);
            }

            if (path.EndsWith("/health", StringComparison.OrdinalIgnoreCase))
            {
                return JsonResponse("""
                {
                  "status": "healthy",
                  "summary": "Live upstream healthy.",
                  "signals": ["live-endpoint-configured"]
                }
                """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var provider = new RestTransportProvider(
            new HttpClient(handler),
            new RestTransportProviderConfiguration(
                true,
                "live",
                "https://transport.example.com/api",
                "none",
                null));

        var routes = await provider.ReadRoutesAsync();
        var routeDetails = await provider.ReadRouteDetailsAsync();
        var health = await provider.GetHealthAsync();

        Assert.Single(routes);
        Assert.Equal("LIVE-901", routes.Single().Reference);
        Assert.Single(routeDetails);
        Assert.Equal("Live Driver", routeDetails.Single().DriverName);
        Assert.Equal("Live upstream healthy.", health.Summary);
    }

    [Fact]
    public async Task ReadRoutesAsync_falls_back_to_demo_data_when_upstream_endpoint_is_placeholder()
    {
        var provider = new RestTransportProvider(
            httpClient: null,
            new RestTransportProviderConfiguration(
                true,
                "sandbox",
                "https://sandbox.example.invalid/transport",
                "api-key",
                null));

        var routes = await provider.ReadRoutesAsync();
        var health = await provider.GetHealthAsync();

        Assert.Equal(3, routes.Count);
        Assert.Contains(routes, route => route.Reference == "RT-412");
        Assert.Equal(Orkystra.Contracts.Connectors.ProviderHealthStatus.Degraded, health.Status);
        Assert.Contains("demo fallback", health.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetHealthAsync_reports_auth_key_missing_when_api_key_mode_has_no_key()
    {
        // A valid live endpoint with auth mode api-key but no API key supplied.
        var provider = new RestTransportProvider(
            new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
            new RestTransportProviderConfiguration(
                true,
                "live",
                "https://transport.example.com/api",
                "api-key",
                null));  // No API key

        var health = await provider.GetHealthAsync();

        Assert.Equal(Orkystra.Contracts.Connectors.ProviderHealthStatus.Degraded, health.Status);
        Assert.Contains("auth-key-missing", health.Signals);
        Assert.Contains("API key", health.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAsync_forwards_api_key_header_when_configured()
    {
        string? capturedApiKeyHeader = null;

        var handler = new StubHttpMessageHandler(request =>
        {
            capturedApiKeyHeader = request.Headers.TryGetValues("X-Api-Key", out var values)
                ? string.Join(",", values)
                : null;

            if (request.RequestUri?.AbsolutePath.EndsWith("/routes", StringComparison.OrdinalIgnoreCase) == true)
            {
                return JsonResponse("""
                [
                  {
                    "routeId": "e9b0b451-df34-4607-9ee7-e8f593ab6b61",
                    "reference": "KEYED-ROUTE-01",
                    "truckId": "ea018d54-b1d7-44f6-9fe5-64bddd16e414",
                    "truckReference": "TRK-KEYED-1",
                    "status": "On time",
                    "stopCount": 1,
                    "shipmentCount": 2,
                    "completedDeliveryCount": 0
                  }
                ]
                """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var provider = new RestTransportProvider(
            new HttpClient(handler),
            new RestTransportProviderConfiguration(
                true,
                "live",
                "https://transport.example.com/api",
                "api-key",
                "my-secret-api-key"));

        var routes = await provider.ReadRoutesAsync();

        Assert.Single(routes);
        Assert.Equal("KEYED-ROUTE-01", routes.Single().Reference);
        Assert.Equal("my-secret-api-key", capturedApiKeyHeader);
    }

    [Fact]
    public async Task GetSyncStatusAsync_reports_auth_key_missing_when_live_but_no_key()
    {
        var provider = new RestTransportProvider(
            new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
            new RestTransportProviderConfiguration(
                true,
                "live",
                "https://transport.example.com/api",
                "api-key",
                null));

        var syncStatus = await provider.GetSyncStatusAsync();

        Assert.Equal("auth-key-missing", syncStatus.Status);
        Assert.Contains("API key", syncStatus.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpResponseMessage JsonResponse(string payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFactory(request));
        }
    }
}
