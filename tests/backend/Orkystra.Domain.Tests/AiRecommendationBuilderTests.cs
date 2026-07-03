using Orkystra.Api.AI;
using Orkystra.Contracts.Ai;
using Orkystra.Contracts.ControlTower;
using Orkystra.Contracts.Simulation;
using Orkystra.Contracts.Transport;
using Orkystra.Contracts.Warehouse;

namespace Orkystra.Domain.Tests;

public sealed class AiRecommendationBuilderTests
{
    [Fact]
    public void ClassifyIntent_returns_warehouse_for_warehouse_keywords()
    {
        Assert.Equal("warehouse", AiRecommendationBuilder.ClassifyIntent("Which warehouse needs attention?"));
        Assert.Equal("warehouse", AiRecommendationBuilder.ClassifyIntent("Analyze dock congestion at North Hub"));
        Assert.Equal("warehouse", AiRecommendationBuilder.ClassifyIntent("How many pallets are in storage?"));
        Assert.Equal("warehouse", AiRecommendationBuilder.ClassifyIntent("Check slot utilization across all zones"));
    }

    [Fact]
    public void ClassifyIntent_returns_dispatcher_for_transport_keywords()
    {
        Assert.Equal("dispatcher", AiRecommendationBuilder.ClassifyIntent("Which route is delayed?"));
        Assert.Equal("dispatcher", AiRecommendationBuilder.ClassifyIntent("What is the ETA for RT-412?"));
        Assert.Equal("dispatcher", AiRecommendationBuilder.ClassifyIntent("Dispatch the nearest truck to the depot"));
        Assert.Equal("dispatcher", AiRecommendationBuilder.ClassifyIntent("Show delivery exceptions"));
    }

    [Fact]
    public void ClassifyIntent_returns_unknown_for_unclear_question()
    {
        Assert.Equal("unknown", AiRecommendationBuilder.ClassifyIntent("What is the weather like?"));
        Assert.Equal("unknown", AiRecommendationBuilder.ClassifyIntent("How is the business doing today?"));
        Assert.Equal("unknown", AiRecommendationBuilder.ClassifyIntent(""));
        Assert.Equal("unknown", AiRecommendationBuilder.ClassifyIntent("   "));
        Assert.Equal("unknown", AiRecommendationBuilder.ClassifyIntent(null!));
    }

    [Fact]
    public void BuildFromProjections_returns_warehouse_response_with_correct_grounding()
    {
        var request = new AiRecommendationQueryRequest("Which warehouse needs attention right now?", "scenario-1");
        var overview = BuildOverviewWithWarehouses();

        var result = AiRecommendationBuilder.BuildFromProjections(request, overview);

        Assert.Equal("warehouse", result.Intent);
        Assert.NotEmpty(result.Evidence);
        Assert.All(result.Evidence, ev => Assert.Equal("projection_data", ev.Grounding));
        Assert.Contains("North Hub A", result.DirectAnswer, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFromProjections_returns_dispatcher_response_with_correct_grounding()
    {
        var request = new AiRecommendationQueryRequest("Which route is critical right now?", "scenario-1");
        var overview = BuildOverviewWithRoutes();

        var result = AiRecommendationBuilder.BuildFromProjections(request, overview);

        Assert.Equal("dispatcher", result.Intent);
        Assert.NotEmpty(result.Evidence);
        Assert.All(result.Evidence, ev => Assert.Equal("projection_data", ev.Grounding));
        Assert.Contains("RT-412", result.DirectAnswer, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFromProjections_returns_unknown_for_ambiguous_question()
    {
        var request = new AiRecommendationQueryRequest("What do you think about the current setup?", "scenario-1");
        var overview = BuildOverviewWithWarehouses();

        var result = AiRecommendationBuilder.BuildFromProjections(request, overview);

        Assert.Equal("unknown", result.Intent);
        Assert.Empty(result.Evidence);
        Assert.NotEmpty(result.Assumptions);
        Assert.Empty(result.RecommendedActions);
        Assert.Equal("low", result.ConfidenceLevel);
    }

    [Fact]
    public void BuildFromProjections_returns_no_warehouses_message_when_warehouses_empty()
    {
        var request = new AiRecommendationQueryRequest("Which warehouse needs attention right now?", "scenario-1");
        var overview = BuildOverviewWithRoutes();

        var result = AiRecommendationBuilder.BuildFromProjections(request, overview);

        Assert.Equal("warehouse", result.Intent);
        Assert.Empty(result.Evidence);
        Assert.Equal("low", result.ConfidenceLevel);
        Assert.Contains("cannot assess", result.DirectAnswer);
    }

    [Fact]
    public void BuildFromProjections_returns_no_routes_message_when_routes_empty()
    {
        var request = new AiRecommendationQueryRequest("Which route is critical right now?", "scenario-1");
        var overview = BuildOverviewWithWarehouses();

        var result = AiRecommendationBuilder.BuildFromProjections(request, overview);

        Assert.Equal("dispatcher", result.Intent);
        Assert.Empty(result.Evidence);
        Assert.Equal("low", result.ConfidenceLevel);
        Assert.Contains("cannot assess", result.DirectAnswer);
    }

    [Fact]
    public void BuildFromProjections_handles_null_question()
    {
        var request = new AiRecommendationQueryRequest(null!, "scenario-1");
        var overview = BuildOverviewWithWarehouses();

        var result = AiRecommendationBuilder.BuildFromProjections(request, overview);

        Assert.Equal("unknown", result.Intent);
    }

    private static ControlTowerOverviewResponse BuildOverviewWithWarehouses()
    {
        return new ControlTowerOverviewResponse(
            "north-hub-demo",
            DateTimeOffset.Parse("2026-06-20T10:15:00Z"),
            [],
            [
                new WarehouseSummaryReadModel(
                    Guid.Parse("db9a789f-9df8-45ff-a252-96d4319c2f12"),
                    "North Hub A",
                    4, 18, 820, 3, 612)
            ],
            [],
            [], [], []);
    }

    private static ControlTowerOverviewResponse BuildOverviewWithRoutes()
    {
        return new ControlTowerOverviewResponse(
            "north-hub-demo",
            DateTimeOffset.Parse("2026-06-20T10:15:00Z"),
            [],
            [],
            [
                new RouteSummaryReadModel(
                    Guid.Parse("9f91e82e-226a-48f7-a94c-907b79431739"),
                    "RT-412",
                    Guid.Parse("cf7c6cc8-7b55-49d4-94ff-a5ee9e340856"),
                    "TRK-19",
                    "Delayed",
                    6, 27, 3)
            ],
            [], [], []);
    }
}
