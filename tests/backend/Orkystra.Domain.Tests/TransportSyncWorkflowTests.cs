using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Orkystra.Api.ControlTower;
using Orkystra.Api.Persistence;
using Orkystra.Application.Connectors;
using Orkystra.Application.Connectors.Providers;
using Orkystra.Contracts.Transport;

namespace Orkystra.Domain.Tests;

public sealed class TransportSyncWorkflowTests
{
    [Fact]
    public async Task ImportSnapshotAsync_persists_live_transport_snapshot_and_status()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("orkystra-transport-sync-tests");

        try
        {
            var store = new SqliteOperationalPersistenceStore(
                Options.Create(new OperationalPersistenceOptions
                {
                    DatabasePath = Path.Combine("data", "operations.db")
                }),
                tempDirectory.FullName);

            var provider = new RestTransportProvider(
                new HttpClient(new StubHttpMessageHandler(request =>
                {
                    var path = request.RequestUri?.AbsolutePath ?? string.Empty;

                    if (path.EndsWith("/routes/details", StringComparison.OrdinalIgnoreCase))
                    {
                        return JsonResponse("""
                        [
                          {
                            "routeId": "4a63ebfb-c31a-4100-8684-561f427d10c9",
                            "reference": "LIVE-204",
                            "truckId": "01987c75-661d-434d-8e62-9321d0c74bd2",
                            "truckReference": "TRK-LIVE-204",
                            "driverName": "Maya Sync",
                            "status": "On time",
                            "truckStatus": "In transit",
                            "truckCapacityKilograms": 780,
                            "totalLoadKilograms": 520,
                            "stopCount": 2,
                            "shipmentCount": 2,
                            "completedDeliveryCount": 1,
                            "updatedAtUtc": "2026-06-22T12:00:00Z",
                            "stops": [
                              { "sequence": 1, "name": "Import Hub", "coordinateLabel": "48.8566, 2.3522", "timeWindowLabel": "08:00-08:45" },
                              { "sequence": 2, "name": "Import Store", "coordinateLabel": "48.8800, 2.4000", "timeWindowLabel": "09:00-09:30" }
                            ],
                            "shipments": [
                              { "reference": "LIVE-SHIP-204-01", "status": "Loaded", "loadWeightKilograms": 120, "orderReference": "LIVE-ORDER-204-01" },
                              { "reference": "LIVE-SHIP-204-02", "status": "Departed", "loadWeightKilograms": 90, "orderReference": "LIVE-ORDER-204-02" }
                            ],
                            "deliveries": [
                              { "reference": "LIVE-DLV-204-01", "stopSequence": 2, "stopName": "Import Store", "shipmentReference": "LIVE-SHIP-204-02", "status": "Pending" }
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
                          "summary": "Live transport import healthy.",
                          "signals": ["live-endpoint-configured", "auth-key-configured"]
                        }
                        """);
                    }

                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                })),
                new RestTransportProviderConfiguration(
                    true,
                    "live",
                    "https://transport.example.com/api",
                    "api-key",
                    "secret-key"));

            var service = new TransportSyncWorkflowService(provider, store);

            var result = await service.ImportSnapshotAsync("tenant-a");
            var persistedSummary = await store.ReadProjectionSnapshotAsync("tenant-a", "route-summaries", "all");
            var persistedStatus = await service.GetLatestStatusAsync("tenant-a");

            Assert.Equal("rest-transport-adapter", result.ProviderId);
            Assert.Equal("live", result.Source);
            Assert.True(result.LiveImport);
            Assert.True(result.HasPersistedSnapshot);
            Assert.Equal(1, result.ImportedRouteCount);
            Assert.Contains("LIVE-204", result.ImportedRouteReferences);
            Assert.NotNull(result.LastImportedAtUtc);
            Assert.NotNull(persistedSummary);
            Assert.Equal("live", persistedStatus.Source);
            Assert.Equal(1, persistedStatus.ImportedRouteCount);
            Assert.Equal("Live transport import healthy.", persistedStatus.Health.Summary);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Fact]
    public async Task GetLatestStatusAsync_returns_runtime_status_when_no_snapshot_has_been_imported_yet()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("orkystra-transport-sync-tests");

        try
        {
            var store = new SqliteOperationalPersistenceStore(
                Options.Create(new OperationalPersistenceOptions
                {
                    DatabasePath = Path.Combine("data", "operations.db")
                }),
                tempDirectory.FullName);

            var provider = new RestTransportProvider(
                httpClient: null,
                new RestTransportProviderConfiguration(
                    true,
                    "sandbox",
                    "https://sandbox.example.invalid/transport",
                    "api-key",
                    null));

            var service = new TransportSyncWorkflowService(provider, store);

            var result = await service.GetLatestStatusAsync("tenant-a");

            Assert.False(result.HasPersistedSnapshot);
            Assert.Equal("demo-fallback", result.Source);
            Assert.Equal(0, result.ImportedRouteCount);
            Assert.Null(result.LastImportedAtUtc);
            Assert.Equal("degraded-live-snapshot", result.SyncStatus);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
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
