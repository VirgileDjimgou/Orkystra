using System.Net.Http.Json;
using System.Text.Json;
using Orkystra.Contracts.Connectors;
using Orkystra.Contracts.Transport;

namespace Orkystra.Application.Connectors.Providers;

public sealed class RestTransportProvider : ITransportProjectionProviderAdapter
{
    private readonly HttpClient? _httpClient;
    private readonly RestTransportProviderConfiguration _configuration;

    public RestTransportProvider()
        : this(httpClient: null, RestTransportProviderConfiguration.LocalDemo)
    {
    }

    public RestTransportProvider(HttpClient? httpClient, RestTransportProviderConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public string ProviderId => "rest-transport-adapter";

    public string ProviderName => "REST Transport Adapter";

    public ProviderDomain Domain => ProviderDomain.Transport;

    public ProviderKind Kind => ProviderKind.Connector;

    public async ValueTask<ProviderHealthReport> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        if (!_configuration.Enabled)
        {
            return BuildHealthReport(
                ProviderHealthStatus.Degraded,
                "REST transport provider is disabled in the current runtime configuration.",
                ["provider-disabled"]);
        }

        if (!IsLiveModeConfigured())
        {
            return BuildHealthReport(
                ProviderHealthStatus.Degraded,
                "REST adapter is available in demo fallback mode because no valid live upstream endpoint is configured yet.",
                ["endpoint-unconfigured", "schema-ready", "demo-fallback"]);
        }

        try
        {
            var healthResponse = await SendAsync("health", cancellationToken);
            if (healthResponse?.IsSuccessStatusCode == true)
            {
                var payload = await healthResponse.Content.ReadFromJsonAsync<LiveTransportHealthPayload>(cancellationToken);
                return BuildHealthReport(
                    MapHealthStatus(payload?.Status),
                    payload?.Summary ?? $"Live transport endpoint {_configuration.BaseUrl} responded successfully.",
                    payload?.Signals?.Count > 0
                        ? payload.Signals
                        : ["live-endpoint-configured", "schema-ready"]);
            }

            return BuildHealthReport(
                ProviderHealthStatus.Degraded,
                $"Live transport endpoint {_configuration.BaseUrl} is configured, but its health probe is unavailable.",
                ["health-probe-unavailable", "live-endpoint-configured"]);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return BuildHealthReport(
                ProviderHealthStatus.Degraded,
                $"Live transport endpoint {_configuration.BaseUrl} could not be reached. Demo fallback remains active. {exception.Message}",
                ["live-endpoint-unreachable", "demo-fallback"]);
        }
    }

    public ValueTask<ProviderCapabilitySet> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(
            new ProviderCapabilitySet(
                CanRead: true,
                CanWrite: true,
                CanStreamEvents: false,
                CanIngestCommands: true,
                CanQueryHistory: true,
                SupportsReadOnlyMode: true,
                CanReplayData: false));
    }

    public ValueTask<ProviderSyncStatus> GetSyncStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!_configuration.Enabled)
        {
            return ValueTask.FromResult(
                new ProviderSyncStatus(
                    ProviderId,
                    "pull",
                    null,
                    DateTimeOffset.UtcNow,
                    "disabled",
                    "Provider runtime configuration is currently disabled."));
        }

        if (IsLiveModeConfigured())
        {
            return ValueTask.FromResult(
                new ProviderSyncStatus(
                    ProviderId,
                    "pull",
                    null,
                    DateTimeOffset.UtcNow,
                    "live-configured",
                    $"Configured to pull transport projections from {_configuration.BaseUrl}."));
        }

        return ValueTask.FromResult(
            new ProviderSyncStatus(
                ProviderId,
                "pull",
                DateTimeOffset.UtcNow.AddMinutes(-9),
                DateTimeOffset.UtcNow,
                "degraded-live-snapshot",
                "Demo transport projection is available while upstream configuration remains partial."));
    }

    public ValueTask<ProviderSchemaDescription> DescribeSchemaAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(
            new ProviderSchemaDescription(
                ProviderId,
                "transport-route-resource",
                [
                    new ProviderSchemaField("route_reference", "string", true, "Route.Reference", "External route reference"),
                    new ProviderSchemaField("truck_reference", "string", true, "Truck.Reference", "Assigned truck"),
                    new ProviderSchemaField("status", "string", true, "Route.Status", "Normalized route status"),
                    new ProviderSchemaField("stop_count", "integer", true, "Route.StopCount", "Number of stops"),
                    new ProviderSchemaField("shipment_count", "integer", true, "Route.ShipmentCount", "Number of shipments"),
                    new ProviderSchemaField("completed_delivery_count", "integer", true, "Route.CompletedDeliveryCount", "Completed deliveries")
                ]));
    }

    public async ValueTask<IReadOnlyCollection<RouteSummaryReadModel>> ReadRoutesAsync(CancellationToken cancellationToken = default)
    {
        var liveRoutes = await TryReadLiveAsync<IReadOnlyCollection<RouteSummaryReadModel>>("routes", cancellationToken);
        if (liveRoutes is not null && liveRoutes.Count > 0)
        {
            return liveRoutes;
        }

        IReadOnlyCollection<RouteSummaryReadModel> routes = BuildRouteDetails()
            .Select(route => new RouteSummaryReadModel(
                route.RouteId,
                route.Reference,
                route.TruckId,
                route.TruckReference,
                route.Status,
                route.StopCount,
                route.ShipmentCount,
                route.CompletedDeliveryCount))
            .ToArray();

        return routes;
    }

    public async ValueTask<IReadOnlyCollection<RouteDetailReadModel>> ReadRouteDetailsAsync(CancellationToken cancellationToken = default)
    {
        var liveRoutes = await TryReadLiveAsync<IReadOnlyCollection<RouteDetailReadModel>>("routes/details", cancellationToken);
        return liveRoutes is { Count: > 0 } ? liveRoutes : BuildRouteDetails();
    }

    private bool IsLiveModeConfigured()
    {
        if (string.IsNullOrWhiteSpace(_configuration.BaseUrl))
        {
            return false;
        }

        if (!Uri.TryCreate(_configuration.BaseUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !uri.Host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<T?> TryReadLiveAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!IsLiveModeConfigured())
        {
            return default;
        }

        try
        {
            var response = await SendAsync(path, cancellationToken);
            if (response?.IsSuccessStatusCode != true)
            {
                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return default;
        }
    }

    private async Task<HttpResponseMessage?> SendAsync(string path, CancellationToken cancellationToken)
    {
        if (_httpClient is null || !IsLiveModeConfigured())
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, BuildRequestUri(path));
        if (string.Equals(_configuration.AuthMode, "api-key", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(_configuration.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("X-Api-Key", _configuration.ApiKey);
        }

        return await _httpClient.SendAsync(request, cancellationToken);
    }

    private string BuildRequestUri(string path)
    {
        var baseUrl = _configuration.BaseUrl!.TrimEnd('/');
        return $"{baseUrl}/{path.TrimStart('/')}";
    }

    private ProviderHealthReport BuildHealthReport(
        ProviderHealthStatus status,
        string summary,
        IReadOnlyCollection<string> signals)
    {
        return new ProviderHealthReport(
            ProviderId,
            ProviderName,
            status,
            DateTimeOffset.UtcNow,
            summary,
            signals);
    }

    private static ProviderHealthStatus MapHealthStatus(string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "healthy" => ProviderHealthStatus.Healthy,
            "warning" => ProviderHealthStatus.Degraded,
            "degraded" => ProviderHealthStatus.Degraded,
            "critical" => ProviderHealthStatus.Unhealthy,
            "unhealthy" => ProviderHealthStatus.Unhealthy,
            _ => ProviderHealthStatus.Healthy
        };
    }

    private sealed record LiveTransportHealthPayload(
        string? Status,
        string? Summary,
        IReadOnlyCollection<string>? Signals);

    private static IReadOnlyCollection<RouteDetailReadModel> BuildRouteDetails()
    {
        return
        [
            new RouteDetailReadModel(
                Guid.Parse("5024fa82-f658-46c8-88bf-aece07d56f09"),
                "RT-204",
                Guid.Parse("0d91dc2f-3a74-4562-96a6-c8de611f699d"),
                "TRK-11",
                "Alex Driver",
                "On time",
                "In transit",
                500m,
                440m,
                5,
                22,
                2,
                DateTimeOffset.Parse("2026-06-20T10:42:00Z"),
                [
                    new TransportRouteStopReadModel(1, "North Hub A", "48.8566, 2.3522", "08:00-09:30"),
                    new TransportRouteStopReadModel(2, "City Cross-Dock", "48.8809, 2.3743", "09:45-10:15"),
                    new TransportRouteStopReadModel(3, "Retail Depot 14", "48.9050, 2.4130", "10:30-11:15"),
                    new TransportRouteStopReadModel(4, "Retail Depot 19", "48.9178, 2.4571", "11:20-12:10"),
                    new TransportRouteStopReadModel(5, "West Flow Center", "48.9352, 2.4912", "12:20-13:00")
                ],
                [
                    new TransportRouteShipmentReadModel("SHIP-204-01", "Loaded", 120m, "ORD-2041"),
                    new TransportRouteShipmentReadModel("SHIP-204-02", "Departed", 100m, "ORD-2042"),
                    new TransportRouteShipmentReadModel("SHIP-204-03", "Arrived", 80m, "ORD-2043"),
                    new TransportRouteShipmentReadModel("SHIP-204-04", "Completed", 140m, "ORD-2044")
                ],
                [
                    new TransportRouteDeliveryReadModel("DLV-204-01", 1, "North Hub A", "SHIP-204-01", "Completed"),
                    new TransportRouteDeliveryReadModel("DLV-204-02", 2, "City Cross-Dock", "SHIP-204-02", "Completed"),
                    new TransportRouteDeliveryReadModel("DLV-204-03", 3, "Retail Depot 14", "SHIP-204-03", "Pending"),
                    new TransportRouteDeliveryReadModel("DLV-204-04", 4, "Retail Depot 19", "SHIP-204-04", "Pending"),
                    new TransportRouteDeliveryReadModel("DLV-204-05", 5, "West Flow Center", "SHIP-204-04", "Pending")
                ]),
            new RouteDetailReadModel(
                Guid.Parse("528c1588-40fd-451b-8c86-2caa625602de"),
                "RT-318",
                Guid.Parse("2a398a30-61cf-4fc3-a18d-e491530b4f24"),
                "TRK-07",
                "Mina Lopez",
                "At risk",
                "Delayed",
                460m,
                310m,
                4,
                15,
                1,
                DateTimeOffset.Parse("2026-06-20T11:05:00Z"),
                [
                    new TransportRouteStopReadModel(1, "West Flow Center", "48.9352, 2.4912", "09:10-09:40"),
                    new TransportRouteStopReadModel(2, "Retail Depot 21", "48.9642, 2.5339", "10:00-10:30"),
                    new TransportRouteStopReadModel(3, "Regional Store 07", "48.9772, 2.5751", "10:45-11:15"),
                    new TransportRouteStopReadModel(4, "Regional Store 11", "48.9918, 2.6182", "11:30-12:20")
                ],
                [
                    new TransportRouteShipmentReadModel("SHIP-318-01", "Loaded", 90m, "ORD-3181"),
                    new TransportRouteShipmentReadModel("SHIP-318-02", "Loaded", 75m, "ORD-3182"),
                    new TransportRouteShipmentReadModel("SHIP-318-03", "Departed", 80m, "ORD-3183"),
                    new TransportRouteShipmentReadModel("SHIP-318-04", "Arrived", 65m, "ORD-3184")
                ],
                [
                    new TransportRouteDeliveryReadModel("DLV-318-01", 1, "West Flow Center", "SHIP-318-01", "Completed"),
                    new TransportRouteDeliveryReadModel("DLV-318-02", 2, "Retail Depot 21", "SHIP-318-02", "Pending"),
                    new TransportRouteDeliveryReadModel("DLV-318-03", 3, "Regional Store 07", "SHIP-318-03", "Pending"),
                    new TransportRouteDeliveryReadModel("DLV-318-04", 4, "Regional Store 11", "SHIP-318-04", "Pending")
                ]),
            new RouteDetailReadModel(
                Guid.Parse("9f91e82e-226a-48f7-a94c-907b79431739"),
                "RT-412",
                Guid.Parse("cf7c6cc8-7b55-49d4-94ff-a5ee9e340856"),
                "TRK-19",
                "Noah Karim",
                "Delayed",
                "Delayed",
                520m,
                470m,
                6,
                27,
                3,
                DateTimeOffset.Parse("2026-06-20T11:37:00Z"),
                [
                    new TransportRouteStopReadModel(1, "North Hub A", "48.8566, 2.3522", "09:20-09:50"),
                    new TransportRouteStopReadModel(2, "Urban Relay 04", "48.8893, 2.3784", "10:15-10:45"),
                    new TransportRouteStopReadModel(3, "Regional Store 16", "48.9251, 2.4220", "11:00-11:30"),
                    new TransportRouteStopReadModel(4, "Regional Store 22", "48.9517, 2.4613", "11:40-12:05"),
                    new TransportRouteStopReadModel(5, "Cross-Dock Bravo", "48.9784, 2.5085", "12:20-12:50"),
                    new TransportRouteStopReadModel(6, "West Flow Center", "48.9952, 2.5526", "13:00-13:40")
                ],
                [
                    new TransportRouteShipmentReadModel("SHIP-412-01", "Loaded", 80m, "ORD-4121"),
                    new TransportRouteShipmentReadModel("SHIP-412-02", "Loaded", 90m, "ORD-4122"),
                    new TransportRouteShipmentReadModel("SHIP-412-03", "Departed", 75m, "ORD-4123"),
                    new TransportRouteShipmentReadModel("SHIP-412-04", "Departed", 95m, "ORD-4124"),
                    new TransportRouteShipmentReadModel("SHIP-412-05", "Arrived", 65m, "ORD-4125"),
                    new TransportRouteShipmentReadModel("SHIP-412-06", "Completed", 65m, "ORD-4126")
                ],
                [
                    new TransportRouteDeliveryReadModel("DLV-412-01", 1, "North Hub A", "SHIP-412-01", "Completed"),
                    new TransportRouteDeliveryReadModel("DLV-412-02", 2, "Urban Relay 04", "SHIP-412-02", "Completed"),
                    new TransportRouteDeliveryReadModel("DLV-412-03", 3, "Regional Store 16", "SHIP-412-03", "Pending"),
                    new TransportRouteDeliveryReadModel("DLV-412-04", 4, "Regional Store 22", "SHIP-412-04", "Pending"),
                    new TransportRouteDeliveryReadModel("DLV-412-05", 5, "Cross-Dock Bravo", "SHIP-412-05", "Pending"),
                    new TransportRouteDeliveryReadModel("DLV-412-06", 6, "West Flow Center", "SHIP-412-06", "Pending")
                ])
        ];
    }
}
