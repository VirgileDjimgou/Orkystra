using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json;
using Orkystra.Contracts.Ai;
using Orkystra.Contracts.ControlTower;
using Orkystra.Contracts.Simulation;
using Orkystra.Contracts.Transport;
using Orkystra.Contracts.Warehouse;

namespace Orkystra.Api.AI;

public sealed class HttpAiProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpAiProvider> _logger;

    public HttpAiProvider(HttpClient httpClient, ILogger<HttpAiProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public string Name => "http";

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
                    return new AiRecommendationEnvelope(MapRecommendation(aiResponse), "api", null, Name);
                }
            }

            var errorMessage = await ReadErrorMessageAsync(response, cancellationToken);
            _logger.LogWarning(
                "HTTP AI provider request for tenant {TenantId} returned {StatusCode}: {ErrorMessage}",
                tenantId,
                (int)response.StatusCode,
                errorMessage);

            return new AiRecommendationEnvelope(
                AiRecommendationBuilder.BuildFromProjections(request, overview),
                "fallback",
                $"HTTP AI provider returned {(int)response.StatusCode}: {errorMessage}",
                Name);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException or JsonException)
        {
            _logger.LogWarning(
                exception,
                "HTTP AI provider connection failed for tenant {TenantId} and question '{Question}'",
                tenantId,
                request.Question);

            return new AiRecommendationEnvelope(
                AiRecommendationBuilder.BuildFromProjections(request, overview),
                "fallback",
                exception.Message,
                Name);
        }
    }

    private static AiRecommendationResponse MapRecommendation(AiServiceRecommendationResponse response)
    {
        return new AiRecommendationResponse(
            response.Intent,
            response.DirectAnswer,
            response.Evidence
                .Select(item => new AiEvidenceReadModel(item.Source, item.Detail, item.Grounding ?? "projection_data"))
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
        [property: JsonPropertyName("detail")] string Detail,
        [property: JsonPropertyName("grounding")] string? Grounding);

    private sealed record AiServiceRecommendedAction(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("rationale")] string Rationale,
        [property: JsonPropertyName("priority")] string Priority);
}
