using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Orkystra.Api.AI;
using Orkystra.Contracts.Ai;
using Orkystra.Contracts.ControlTower;
using Orkystra.Contracts.Simulation;
using Orkystra.Contracts.Transport;
using Orkystra.Contracts.Warehouse;

namespace Orkystra.Domain.Tests;

public sealed class AiWorkflowTests
{
    [Fact]
    public async Task BuildRecommendationAsync_uses_http_provider_when_available()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "intent": "warehouse",
              "direct_answer": "The clearest warehouse pressure point is North Hub A.",
              "evidence": [
                {
                  "source": "warehouse_summary_projection",
                  "detail": "North Hub A is using 612 of 820 slots (75%)."
                }
              ],
              "assumptions": [],
              "recommended_actions": [
                {
                  "title": "Rebalance inbound flow at North Hub A",
                  "rationale": "The busiest warehouse has the tightest remaining slot capacity.",
                  "priority": "high"
                }
              ],
              "confidence_level": "high",
              "alternative_scenario_note": "Run a what-if scenario that diverts the next inbound wave.",
              "missing_data": [],
              "specialist_agents": [
                "warehouse-agent"
              ]
            }
            """, Encoding.UTF8, "application/json")
        });

        var httpProvider = new HttpAiProvider(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:8001")
        }, NullLogger<HttpAiProvider>.Instance);

        var service = new AiWorkflowService(httpProvider, NullLogger<AiWorkflowService>.Instance);

        var result = await service.BuildRecommendationAsync(
            "north-hub-demo",
            new AiRecommendationQueryRequest("Which warehouse needs attention right now?", "scenario-1"),
            BuildOverview(),
            CancellationToken.None);

        Assert.Equal("api", result.Source);
        Assert.Equal("http", result.ProviderName);
        Assert.Equal("warehouse", result.Recommendation.Intent);
        Assert.Contains("North Hub A", result.Recommendation.DirectAnswer);
        Assert.Contains("\"tenant_id\":\"north-hub-demo\"", handler.LastRequestBody);
        Assert.Contains("\"warehouse_summaries\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task HttpAiProvider_falls_back_when_service_is_unavailable()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            ReasonPhrase = "Service unavailable"
        });

        var httpProvider = new HttpAiProvider(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:8001")
        }, NullLogger<HttpAiProvider>.Instance);

        var service = new AiWorkflowService(httpProvider, NullLogger<AiWorkflowService>.Instance);

        var result = await service.BuildRecommendationAsync(
            "north-hub-demo",
            new AiRecommendationQueryRequest("Which route should the dispatcher review first?", "scenario-1"),
            BuildOverview(),
            CancellationToken.None);

        Assert.Equal("fallback", result.Source);
        Assert.Equal("http", result.ProviderName);
        Assert.Equal("dispatcher", result.Recommendation.Intent);
        Assert.Contains("RT-412", result.Recommendation.DirectAnswer);
    }

    [Fact]
    public async Task LocalAiProvider_returns_deterministic_recommendation()
    {
        var localProvider = new LocalAiProvider(NullLogger<LocalAiProvider>.Instance);
        var service = new AiWorkflowService(localProvider, NullLogger<AiWorkflowService>.Instance);

        var result = await service.BuildRecommendationAsync(
            "north-hub-demo",
            new AiRecommendationQueryRequest("Which warehouse needs attention right now?", "scenario-1"),
            BuildOverview(),
            CancellationToken.None);

        Assert.Equal("api", result.Source);
        Assert.Equal("local", result.ProviderName);
        Assert.Equal("warehouse", result.Recommendation.Intent);
        Assert.Contains("North Hub A", result.Recommendation.DirectAnswer);
        Assert.NotEmpty(result.Recommendation.Evidence);
        Assert.Equal("projection_data", result.Recommendation.Evidence.First().Grounding);
    }

    [Fact]
    public async Task DisabledAiProvider_returns_disabled_message()
    {
        var disabledProvider = new DisabledAiProvider();
        var service = new AiWorkflowService(disabledProvider, NullLogger<AiWorkflowService>.Instance);

        var result = await service.BuildRecommendationAsync(
            "north-hub-demo",
            new AiRecommendationQueryRequest("Which warehouse needs attention right now?", "scenario-1"),
            BuildOverview(),
            CancellationToken.None);

        Assert.Equal("api", result.Source);
        Assert.Equal("disabled", result.ProviderName);
        Assert.Equal("unknown", result.Recommendation.Intent);
        Assert.Contains("disabled", result.Recommendation.DirectAnswer, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("low", result.Recommendation.ConfidenceLevel);
        Assert.Contains("AiService__Provider", result.Recommendation.DirectAnswer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("supervisor-agent", result.Recommendation.SpecialistAgents);
    }

    [Fact]
    public async Task HttpAiProvider_returns_fallback_on_null_response_body()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json")
        });

        var httpProvider = new HttpAiProvider(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:8001")
        }, NullLogger<HttpAiProvider>.Instance);

        var service = new AiWorkflowService(httpProvider, NullLogger<AiWorkflowService>.Instance);

        var result = await service.BuildRecommendationAsync(
            "north-hub-demo",
            new AiRecommendationQueryRequest("Which warehouse needs attention right now?", "scenario-1"),
            BuildOverview(),
            CancellationToken.None);

        Assert.Equal("fallback", result.Source);
        Assert.Equal("http", result.ProviderName);
        Assert.NotEmpty(result.Recommendation.Evidence);
    }

    [Fact]
    public async Task HttpAiProvider_returns_fallback_on_malformed_json()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ this is not valid json }", Encoding.UTF8, "application/json")
        });

        var httpProvider = new HttpAiProvider(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:8001")
        }, NullLogger<HttpAiProvider>.Instance);

        var service = new AiWorkflowService(httpProvider, NullLogger<AiWorkflowService>.Instance);

        var result = await service.BuildRecommendationAsync(
            "north-hub-demo",
            new AiRecommendationQueryRequest("Which warehouse needs attention right now?", "scenario-1"),
            BuildOverview(),
            CancellationToken.None);

        Assert.Equal("fallback", result.Source);
        Assert.Equal("http", result.ProviderName);
        Assert.NotEmpty(result.Recommendation.Evidence);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task LocalAiProvider_handles_empty_overview_gracefully()
    {
        var localProvider = new LocalAiProvider(NullLogger<LocalAiProvider>.Instance);
        var service = new AiWorkflowService(localProvider, NullLogger<AiWorkflowService>.Instance);

        var result = await service.BuildRecommendationAsync(
            "north-hub-demo",
            new AiRecommendationQueryRequest("Which warehouse needs attention right now?", "scenario-1"),
            BuildEmptyOverview(),
            CancellationToken.None);

        Assert.Equal("api", result.Source);
        Assert.Equal("local", result.ProviderName);
        Assert.Equal("warehouse", result.Recommendation.Intent);
        Assert.Contains("cannot assess", result.Recommendation.DirectAnswer);
        Assert.Empty(result.Recommendation.Evidence);
    }

    [Fact]
    public async Task LocalAiProvider_returns_unknown_for_ambiguous_question()
    {
        var localProvider = new LocalAiProvider(NullLogger<LocalAiProvider>.Instance);
        var service = new AiWorkflowService(localProvider, NullLogger<AiWorkflowService>.Instance);

        var result = await service.BuildRecommendationAsync(
            "north-hub-demo",
            new AiRecommendationQueryRequest("How is the overall business doing?", "scenario-1"),
            BuildOverview(),
            CancellationToken.None);

        Assert.Equal("api", result.Source);
        Assert.Equal("local", result.ProviderName);
        Assert.Equal("unknown", result.Recommendation.Intent);
        Assert.Empty(result.Recommendation.Evidence);
    }

    private static ControlTowerOverviewResponse BuildOverview()
    {
        return new ControlTowerOverviewResponse(
            "north-hub-demo",
            DateTimeOffset.Parse("2026-06-20T10:15:00Z"),
            [
                new ScenarioSummaryReadModel(
                    Guid.Parse("9d4e8f09-cf15-48d8-90a6-e96c833fd741"),
                    "Baseline day shift",
                    42,
                    "Running",
                    DateTimeOffset.Parse("2026-06-20T10:15:00Z"),
                    2)
            ],
            [
                new WarehouseSummaryReadModel(
                    Guid.Parse("db9a789f-9df8-45ff-a252-96d4319c2f12"),
                    "North Hub A",
                    4,
                    18,
                    820,
                    3,
                    612)
            ],
            [
                new RouteSummaryReadModel(
                    Guid.Parse("9f91e82e-226a-48f7-a94c-907b79431739"),
                    "RT-412",
                    Guid.Parse("cf7c6cc8-7b55-49d4-94ff-a5ee9e340856"),
                    "TRK-19",
                    "Delayed",
                    6,
                    27,
                    3)
            ],
            [],
            [],
            []);
    }

    private static ControlTowerOverviewResponse BuildEmptyOverview()
    {
        return new ControlTowerOverviewResponse("north-hub-demo", DateTimeOffset.UtcNow, [], [], [], [], [], []);
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
