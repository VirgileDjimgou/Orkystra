using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Orkystra.Api.Optimization;
using Orkystra.Contracts.Optimization;
using Orkystra.Contracts.Transport;

namespace Orkystra.Domain.Tests;

public sealed class RouteOptimizationWorkflowTests
{
    [Fact]
    public async Task BuildOptimizationAsync_uses_optimization_service_response_when_available()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "status": "optimized",
              "objective_score": 88.4,
              "ordered_stop_references": ["Regional Store 16", "Regional Store 22", "Cross-Dock Bravo", "West Flow Center"],
              "eta_minutes": {
                "Regional Store 16": 660,
                "Regional Store 22": 700
              },
              "load_distribution": {
                "Regional Store 16": 8,
                "Regional Store 22": 10
              },
              "constraint_violations": [],
              "explanation": {
                "selected_vehicle_reason": "Vehicle TRK-19 remains feasible.",
                "prioritization_reason": "Higher-priority and tighter-window stops were favored first.",
                "tight_constraints": ["route duration"],
                "infeasibility_reason": null,
                "trade_offs": ["The solver preserved an alternative for dispatcher review."]
              },
              "alternatives": [
                {
                  "label": "priority-plan",
                  "ordered_stop_references": ["Regional Store 22", "Regional Store 16", "Cross-Dock Bravo", "West Flow Center"],
                  "objective_score": 93.2,
                  "summary": "Higher-priority stops moved earlier."
                }
              ],
              "solver_backend": "deterministic-fallback"
            }
            """, Encoding.UTF8, "application/json")
        });

        var service = new RouteOptimizationWorkflowService(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:8002")
        }, NullLogger<RouteOptimizationWorkflowService>.Instance);

        var result = await service.BuildOptimizationAsync(
            "north-hub-demo",
            BuildRoute(),
            new RouteOptimizationRunRequest("scenario-1"),
            CancellationToken.None);

        Assert.Equal("api", result.Source);
        Assert.Equal("optimized", result.Optimization.Status);
        Assert.Equal("RT-412", result.Optimization.RouteReference);
        Assert.Equal("deterministic-fallback", result.Optimization.SolverBackend);
        Assert.Contains("\"tenant_id\":\"north-hub-demo\"", handler.LastRequestBody);
        Assert.Contains("\"scenario_id\":\"scenario-1\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task BuildOptimizationAsync_falls_back_when_service_is_unavailable()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            ReasonPhrase = "Service unavailable"
        });

        var service = new RouteOptimizationWorkflowService(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:8002")
        }, NullLogger<RouteOptimizationWorkflowService>.Instance);

        var result = await service.BuildOptimizationAsync(
            "north-hub-demo",
            BuildRoute(),
            new RouteOptimizationRunRequest(null),
            CancellationToken.None);

        Assert.Equal("fallback", result.Source);
        Assert.Equal("optimized", result.Optimization.Status);
        Assert.Equal("api-local-fallback", result.Optimization.SolverBackend);
        Assert.NotEmpty(result.Optimization.OrderedStopReferences);
    }

    private static RouteDetailReadModel BuildRoute()
    {
        return new RouteDetailReadModel(
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
            ]);
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return _responseFactory(request);
        }
    }
}
