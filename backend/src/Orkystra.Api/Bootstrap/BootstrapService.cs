using MQTTnet;
using MQTTnet.Client;
using Orkystra.Api.Eventing;
using Orkystra.Api.Persistence;
using Orkystra.Api.Simulation;
using Orkystra.Contracts.Bootstrap;
using Orkystra.Contracts.Simulation;

namespace Orkystra.Api.Bootstrap;

public sealed class BootstrapService
{
    private readonly ScenarioEventWorkflowService _scenarioEventWorkflowService;
    private readonly IOperationalPersistenceStore _persistenceStore;
    private readonly EventBackboneOptions _eventBackboneOptions;

    public BootstrapService(
        ScenarioEventWorkflowService scenarioEventWorkflowService,
        IOperationalPersistenceStore persistenceStore,
        EventBackboneOptions eventBackboneOptions)
    {
        _scenarioEventWorkflowService = scenarioEventWorkflowService;
        _persistenceStore = persistenceStore;
        _eventBackboneOptions = eventBackboneOptions;
    }

    public async ValueTask<BootstrapDemoResponse> BootstrapAsync(
        string tenantId,
        BootstrapDemoRequest request,
        CancellationToken cancellationToken = default)
    {
        var scenarioRequest = new PublishScenarioEventsRequest(
            request.ScenarioName,
            request.Seed,
            request.AdvanceMinutes,
            request.IncludeDisruption,
            true);

        var scenarioResult = await _scenarioEventWorkflowService.PublishDemoScenarioAsync(
            tenantId, scenarioRequest, cancellationToken);

        await _persistenceStore.AppendWorkflowRunAsync(
            tenantId,
            "bootstrap-demo",
            "demo",
            scenarioResult.ScenarioId.ToString("D"),
            "api",
            "completed",
            scenarioResult,
            cancellationToken);

        return new BootstrapDemoResponse(
            scenarioResult,
            WarehouseCount: 2,
            RouteCount: 3,
            GpsPositionCount: 5,
            DateTimeOffset.UtcNow);
    }
}

public sealed class SanityCheckService
{
    private readonly EventBackboneOptions _eventBackboneOptions;
    private readonly OperationalPersistenceOptions _persistenceOptions;
    private readonly IOperationalPersistenceStore _persistenceStore;
    private readonly IConfiguration _configuration;

    public SanityCheckService(
        EventBackboneOptions eventBackboneOptions,
        OperationalPersistenceOptions persistenceOptions,
        IOperationalPersistenceStore persistenceStore,
        IConfiguration configuration)
    {
        _eventBackboneOptions = eventBackboneOptions;
        _persistenceOptions = persistenceOptions;
        _persistenceStore = persistenceStore;
        _configuration = configuration;
    }

    public async ValueTask<SanityCheckResponse> RunAsync(CancellationToken cancellationToken = default)
    {
        var components = new List<SanityComponentStatus>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        components.Add(CheckApi(sw));
        components.Add(await CheckMqttAsync(sw, cancellationToken));
        components.Add(await CheckPersistenceAsync(sw, cancellationToken));
        components.Add(await CheckFrontendAsync(sw, cancellationToken));

        sw.Stop();

        return new SanityCheckResponse(
            "1.0.0",
            components.All(c => c.Healthy),
            components,
            DateTimeOffset.UtcNow);
    }

    private static SanityComponentStatus CheckApi(System.Diagnostics.Stopwatch sw)
    {
        sw.Restart();
        var status = new SanityComponentStatus("api", true, "API is responding", sw.ElapsedMilliseconds);
        sw.Start();
        return status;
    }

    private async ValueTask<SanityComponentStatus> CheckMqttAsync(System.Diagnostics.Stopwatch sw, CancellationToken cancellationToken)
    {
        sw.Restart();
        try
        {
            if (!_eventBackboneOptions.Enabled)
            {
                sw.Stop();
                return new SanityComponentStatus("mqtt", true, "MQTT backbone is disabled", sw.ElapsedMilliseconds);
            }

            var factory = new MqttFactory();
            using var client = factory.CreateMqttClient();
            var clientOptions = MqttConnectionSettings.BuildClientOptions(
                _eventBackboneOptions.BrokerUrl, "orkystra-sanity");

            await client.ConnectAsync(clientOptions, cancellationToken);
            await client.DisconnectAsync(cancellationToken: cancellationToken);

            sw.Stop();
            return new SanityComponentStatus("mqtt", true, $"Connected to broker at {_eventBackboneOptions.BrokerUrl}", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new SanityComponentStatus("mqtt", false, $"Cannot reach broker at {_eventBackboneOptions.BrokerUrl}: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    private async ValueTask<SanityComponentStatus> CheckPersistenceAsync(System.Diagnostics.Stopwatch sw, CancellationToken cancellationToken)
    {
        sw.Restart();
        try
        {
            await _persistenceStore.ReadProjectionSnapshotsAsync("sanity-check", null, 1, cancellationToken);
            sw.Stop();
            var providerLabel = string.Equals(_persistenceOptions.Provider, "postgres", StringComparison.OrdinalIgnoreCase)
                ? "PostgreSQL"
                : "SQLite";
            return new SanityComponentStatus("persistence", true, $"{providerLabel} store is responding", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new SanityComponentStatus("persistence", false, $"Persistence store failed: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    private async ValueTask<SanityComponentStatus> CheckFrontendAsync(System.Diagnostics.Stopwatch sw, CancellationToken cancellationToken)
    {
        sw.Restart();
        try
        {
            var frontendUrl = _configuration["Sanity:FrontendUrl"] ?? "http://127.0.0.1:5173";
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await httpClient.GetAsync(frontendUrl, cancellationToken);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                return new SanityComponentStatus("frontend", true, $"Frontend at {frontendUrl} is responding (HTTP {(int)response.StatusCode})", sw.ElapsedMilliseconds);
            }

            return new SanityComponentStatus("frontend", false, $"Frontend at {frontendUrl} returned HTTP {(int)response.StatusCode}", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new SanityComponentStatus("frontend", false, $"Cannot reach frontend: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }
}
