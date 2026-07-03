using Orkystra.Contracts.Ai;
using Orkystra.Contracts.ControlTower;

namespace Orkystra.Api.AI;

public static class AiRecommendationBuilder
{
    public static string ClassifyIntent(string question)
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

    public static AiRecommendationResponse BuildFromProjections(
        AiRecommendationQueryRequest request,
        ControlTowerOverviewResponse overview)
    {
        var intent = ClassifyIntent(request.Question);
        return intent switch
        {
            "warehouse" => BuildWarehouseRecommendation(overview),
            "dispatcher" => BuildDispatcherRecommendation(overview),
            _ => BuildUnknownRecommendation()
        };
    }

    private static AiRecommendationResponse BuildWarehouseRecommendation(ControlTowerOverviewResponse overview)
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

        var evidence = new List<AiEvidenceReadModel>
        {
            new("warehouse_summary_projection", $"{busiest.Name} is using {busiest.StoredPalletCount} of {busiest.SlotCount} slots ({utilization}%).", "projection_data"),
            new("dock_projection", $"{busiest.Name} currently shows {busiest.OccupiedDockCount} occupied docks.", "projection_data")
        };

        var assumptions = new List<string>();
        if (busiest.OccupiedDockCount == 0)
        {
            assumptions.Add("Dock pressure cannot be estimated reliably because the busiest warehouse reports no occupied docks.");
        }

        return new AiRecommendationResponse(
            "warehouse",
            $"The clearest warehouse pressure point is {busiest.Name}. It is operating at roughly {utilization}% slot utilization, so further inbound waves should be staged carefully.",
            evidence,
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

    private static AiRecommendationResponse BuildDispatcherRecommendation(ControlTowerOverviewResponse overview)
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

        var evidence = new List<AiEvidenceReadModel>
        {
            new("route_summary_projection", $"Route {criticalRoute.Reference} is marked '{criticalRoute.Status}' with {criticalRoute.ShipmentCount} shipments over {criticalRoute.StopCount} stops.", "projection_data")
        };

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
            evidence,
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

    private static AiRecommendationResponse BuildUnknownRecommendation()
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
}
