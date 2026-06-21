using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json;
using Orkystra.Contracts.Ai;
using Orkystra.Contracts.ControlTower;
using Orkystra.Contracts.Simulation;
using Orkystra.Contracts.Transport;
using Orkystra.Contracts.Warehouse;

namespace Orkystra.Api.AI;

public sealed class AiWorkflowService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiWorkflowService> _logger;

    public AiWorkflowService(HttpClient httpClient, ILogger<AiWorkflowService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<AiRecommendationEnvelope> BuildRecommendationAsync(
        string tenantId,
        AiRecommendationQueryRequest request,
        ControlTowerOverviewResponse overview,
        CancellationToken cancellationToken)
    {
        var aiRequest = new AiServiceRecommendationRequest(
            tenantId,
            request.Question,
            request.ScenarioId,
            BuildProjectionSnapshot(overview));

        try
        {
            using var response = await _httpClient.PostAsJsonAsync("recommendations", aiRequest, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var aiResponse = await response.Content.ReadFromJsonAsync<AiServiceRecommendationResponse>(cancellationToken);
                if (aiResponse is not null)
                {
                    return new AiRecommendationEnvelope(MapRecommendation(aiResponse), "api", null);
                }
            }

            var errorMessage = await ReadErrorMessageAsync(response, cancellationToken);
            _logger.LogWarning(
                "AI service request for tenant {TenantId} returned {StatusCode}: {ErrorMessage}",
                tenantId,
                (int)response.StatusCode,
                errorMessage);

            return new AiRecommendationEnvelope(
                BuildFallbackRecommendation(request, overview),
                "fallback",
                errorMessage);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException or JsonException)
        {
            _logger.LogWarning(
                exception,
                "AI workflow fallback triggered for tenant {TenantId} and question '{Question}'",
                tenantId,
                request.Question);

            return new AiRecommendationEnvelope(
                BuildFallbackRecommendation(request, overview),
                "fallback",
                exception.Message);
        }
    }

    private static AiRecommendationResponse MapRecommendation(AiServiceRecommendationResponse response)
    {
        return new AiRecommendationResponse(
            response.Intent,
            response.DirectAnswer,
            response.Evidence
                .Select(item => new AiEvidenceReadModel(item.Source, item.Detail))
                .ToArray(),
            response.Assumptions.ToArray(),
            response.RecommendedActions
                .Select(action => new AiRecommendedActionReadModel(action.Title, action.Rationale, action.Priority))
                .ToArray(),
            response.ConfidenceLevel,
            response.AlternativeScenarioNote,
            response.MissingData.ToArray(),
            response.SpecialistAgents.ToArray());
    }

    private static AiRecommendationResponse BuildFallbackRecommendation(
        AiRecommendationQueryRequest request,
        ControlTowerOverviewResponse overview)
    {
        var intent = ClassifyIntent(request.Question);
        return intent switch
        {
            "warehouse" => BuildWarehouseFallback(overview),
            "dispatcher" => BuildDispatcherFallback(overview),
            _ => BuildUnknownFallback()
        };
    }

    private static AiRecommendationResponse BuildWarehouseFallback(ControlTowerOverviewResponse overview)
    {
        if (overview.Warehouses.Count == 0)
        {
            return new AiRecommendationResponse(
                "warehouse",
                "I cannot assess warehouse conditions because no warehouse projections were provided.",
                [],
                [],
                [],
                "low",
                null,
                ["warehouse summary projections"],
                ["warehouse-agent"]);
        }

        var busiest = overview.Warehouses.MaxBy(summary => summary.StoredPalletCount / (double)Math.Max(summary.SlotCount, 1))!;
        var utilization = (int)Math.Round((busiest.StoredPalletCount / (double)Math.Max(busiest.SlotCount, 1)) * 100);

        var assumptions = new List<string>();
        if (busiest.OccupiedDockCount == 0)
        {
            assumptions.Add("Dock pressure cannot be estimated reliably because the busiest warehouse reports no occupied docks.");
        }

        return new AiRecommendationResponse(
            "warehouse",
            $"The clearest warehouse pressure point is {busiest.Name}. It is operating at roughly {utilization}% slot utilization, so further inbound waves should be staged carefully.",
            [
                new AiEvidenceReadModel(
                    "warehouse_summary_projection",
                    $"{busiest.Name} is using {busiest.StoredPalletCount} of {busiest.SlotCount} slots ({utilization}%)."),
                new AiEvidenceReadModel(
                    "dock_projection",
                    $"{busiest.Name} currently shows {busiest.OccupiedDockCount} occupied docks.")
            ],
            assumptions,
            [
                new AiRecommendedActionReadModel(
                    $"Rebalance inbound flow at {busiest.Name}",
                    "The busiest warehouse has the tightest remaining slot capacity and should be protected from additional congestion.",
                    utilization >= 80 ? "high" : "medium")
            ],
            utilization >= 70 ? "high" : "medium",
            "Run a what-if scenario that diverts the next inbound wave to the lower-utilization warehouse before changing execution rules.",
            [],
            ["warehouse-agent"]);
    }

    private static AiRecommendationResponse BuildDispatcherFallback(ControlTowerOverviewResponse overview)
    {
        if (overview.Routes.Count == 0)
        {
            return new AiRecommendationResponse(
                "dispatcher",
                "I cannot assess transport conditions because no route projections were provided.",
                [],
                [],
                [],
                "low",
                null,
                ["route summary projections"],
                ["dispatcher-agent"]);
        }

        var delayedRoutes = overview.Routes.Where(route => !string.Equals(route.Status, "On time", StringComparison.OrdinalIgnoreCase)).ToArray();
        var criticalRoute = (delayedRoutes.Length > 0 ? delayedRoutes : overview.Routes)
            .MaxBy(route => route.ShipmentCount)!;

        var assumptions = new List<string>();
        var missingData = new List<string>();
        if (criticalRoute.CompletedDeliveryCount == 0)
        {
            assumptions.Add("No completed deliveries are visible yet, so route recovery confidence is limited.");
        }

        if (delayedRoutes.Length == 0)
        {
            assumptions.Add("No delayed routes were present; the answer is based on the heaviest active route instead of an active exception.");
            missingData.Add("delay-specific telemetry");
        }

        return new AiRecommendationResponse(
            "dispatcher",
            $"The highest-impact transport watch item is {criticalRoute.Reference}. It is currently '{criticalRoute.Status}' and affects {criticalRoute.ShipmentCount} shipments.",
            [
                new AiEvidenceReadModel(
                    "route_summary_projection",
                    $"Route {criticalRoute.Reference} is marked '{criticalRoute.Status}' with {criticalRoute.ShipmentCount} shipments over {criticalRoute.StopCount} stops.")
            ],
            assumptions,
            [
                new AiRecommendedActionReadModel(
                    $"Review recovery plan for {criticalRoute.Reference}",
                    "This route currently carries the largest shipment load among the routes that are not fully on time.",
                    string.Equals(criticalRoute.Status, "Delayed", StringComparison.OrdinalIgnoreCase) ? "critical" : "high")
            ],
            delayedRoutes.Length > 0 ? "high" : "medium",
            "Compare the current route against a scenario with one stop resequenced or one carrier handoff reassigned.",
            missingData,
            ["dispatcher-agent"]);
    }

    private static AiRecommendationResponse BuildUnknownFallback()
    {
        return new AiRecommendationResponse(
            "unknown",
            "I could not classify the request confidently from the current projections. Please ask a warehouse or dispatcher question, or provide more operational context.",
            [],
            ["Intent routing stayed conservative because the request did not clearly match the warehouse or dispatcher tools."],
            [],
            "low",
            null,
            ["clear operational intent"],
            ["supervisor-agent"]);
    }

    private static string ClassifyIntent(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return "unknown";
        }

        var normalized = question.ToLowerInvariant();
        string[] warehouseKeywords = ["warehouse", "dock", "slot", "pallet", "storage", "congestion"];
        string[] dispatcherKeywords = ["route", "truck", "delay", "eta", "dispatch", "carrier", "delivery"];

        if (warehouseKeywords.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal)))
        {
            return "warehouse";
        }

        if (dispatcherKeywords.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal)))
        {
            return "dispatcher";
        }

        return "unknown";
    }

    private static AiServiceProjectionSnapshot BuildProjectionSnapshot(ControlTowerOverviewResponse overview)
    {
        return new AiServiceProjectionSnapshot(
            overview.Warehouses
                .Select(MapWarehouseSummary)
                .ToArray(),
            overview.Routes
                .Select(MapRouteSummary)
                .ToArray(),
            overview.Scenarios
                .Select(MapScenarioSummary)
                .ToArray());
    }

    private static AiServiceWarehouseSummary MapWarehouseSummary(WarehouseSummaryReadModel summary)
    {
        return new AiServiceWarehouseSummary(
            summary.WarehouseId.ToString(),
            summary.Name,
            summary.ZoneCount,
            summary.RackCount,
            summary.SlotCount,
            summary.OccupiedDockCount,
            summary.StoredPalletCount);
    }

    private static AiServiceRouteSummary MapRouteSummary(RouteSummaryReadModel summary)
    {
        return new AiServiceRouteSummary(
            summary.RouteId.ToString(),
            summary.Reference,
            summary.TruckId.ToString(),
            summary.TruckReference,
            summary.Status,
            summary.StopCount,
            summary.ShipmentCount,
            summary.CompletedDeliveryCount);
    }

    private static AiServiceScenarioSummary MapScenarioSummary(ScenarioSummaryReadModel summary)
    {
        return new AiServiceScenarioSummary(
            summary.ScenarioId.ToString(),
            summary.Name,
            summary.Seed,
            summary.Status,
            summary.CurrentTime.ToString("O"),
            summary.InjectedEventCount);
    }

    private static async Task<string?> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(payload) ? response.ReasonPhrase : payload;
    }

    private sealed record AiServiceRecommendationRequest(
        [property: JsonPropertyName("tenant_id")] string TenantId,
        [property: JsonPropertyName("question")] string Question,
        [property: JsonPropertyName("scenario_id")] string? ScenarioId,
        [property: JsonPropertyName("projections")] AiServiceProjectionSnapshot Projections);

    private sealed record AiServiceProjectionSnapshot(
        [property: JsonPropertyName("warehouse_summaries")] IReadOnlyCollection<AiServiceWarehouseSummary> WarehouseSummaries,
        [property: JsonPropertyName("route_summaries")] IReadOnlyCollection<AiServiceRouteSummary> RouteSummaries,
        [property: JsonPropertyName("scenario_summaries")] IReadOnlyCollection<AiServiceScenarioSummary> ScenarioSummaries);

    private sealed record AiServiceWarehouseSummary(
        [property: JsonPropertyName("warehouse_id")] string WarehouseId,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("zone_count")] int ZoneCount,
        [property: JsonPropertyName("rack_count")] int RackCount,
        [property: JsonPropertyName("slot_count")] int SlotCount,
        [property: JsonPropertyName("occupied_dock_count")] int OccupiedDockCount,
        [property: JsonPropertyName("stored_pallet_count")] int StoredPalletCount);

    private sealed record AiServiceRouteSummary(
        [property: JsonPropertyName("route_id")] string RouteId,
        [property: JsonPropertyName("reference")] string Reference,
        [property: JsonPropertyName("truck_id")] string TruckId,
        [property: JsonPropertyName("truck_reference")] string TruckReference,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("stop_count")] int StopCount,
        [property: JsonPropertyName("shipment_count")] int ShipmentCount,
        [property: JsonPropertyName("completed_delivery_count")] int CompletedDeliveryCount);

    private sealed record AiServiceScenarioSummary(
        [property: JsonPropertyName("scenario_id")] string ScenarioId,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("seed")] int Seed,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("current_time")] string CurrentTime,
        [property: JsonPropertyName("injected_event_count")] int InjectedEventCount);

    private sealed record AiServiceRecommendationResponse(
        [property: JsonPropertyName("intent")] string Intent,
        [property: JsonPropertyName("direct_answer")] string DirectAnswer,
        [property: JsonPropertyName("evidence")] IReadOnlyCollection<AiServiceEvidenceItem> Evidence,
        [property: JsonPropertyName("assumptions")] IReadOnlyCollection<string> Assumptions,
        [property: JsonPropertyName("recommended_actions")] IReadOnlyCollection<AiServiceRecommendedAction> RecommendedActions,
        [property: JsonPropertyName("confidence_level")] string ConfidenceLevel,
        [property: JsonPropertyName("alternative_scenario_note")] string? AlternativeScenarioNote,
        [property: JsonPropertyName("missing_data")] IReadOnlyCollection<string> MissingData,
        [property: JsonPropertyName("specialist_agents")] IReadOnlyCollection<string> SpecialistAgents);

    private sealed record AiServiceEvidenceItem(
        [property: JsonPropertyName("source")] string Source,
        [property: JsonPropertyName("detail")] string Detail);

    private sealed record AiServiceRecommendedAction(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("rationale")] string Rationale,
        [property: JsonPropertyName("priority")] string Priority);
}
