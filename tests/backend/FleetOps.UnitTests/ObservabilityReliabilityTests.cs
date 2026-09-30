extern alias worker;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Tracking;
using FleetOps.Core.Modules.Demo;
using FleetOps.Core.Observability;
using FleetOps.UnitTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using DemoEngineOptions = worker::FleetOps.Worker.Demo.DemoEngineOptions;
using DemoScenarioHostedService = worker::FleetOps.Worker.Demo.DemoScenarioHostedService;

namespace FleetOps.UnitTests;

[Trait("Category", "Reliability")]
public sealed class ObservabilityReliabilityTests
{
    [Fact]
    public void MetricsStoreDoesNotLoseConcurrentCounts()
    {
        var store = new TrackingMetricsStore();
        var organizationId = Guid.NewGuid();
        Parallel.For(0, 8, _ =>
        {
            for (var index = 0; index < 1000; index++)
            {
                store.RecordAccepted(organizationId, outOfOrder: false);
            }
        });
        Parallel.For(0, 8, _ =>
        {
            for (var index = 0; index < 100; index++)
            {
                store.RecordDuplicate(organizationId);
                store.RecordRejected(organizationId);
            }
        });

        var snapshot = store.GetSnapshot(organizationId);
        Assert.Equal(8000, snapshot.Accepted);
        Assert.Equal(800, snapshot.Duplicate);
        Assert.Equal(800, snapshot.Rejected);
    }

    [Fact]
    public async Task CanonicalIngestionRecordsOutcomeMetrics()
    {
        await using var factory = new FleetOpsApiFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        using var collector = new MetricCollector();
        var scenario = await LoadScenarioAsync(client, "northwind", 3);
        var vehicle = scenario.Vehicles[0];
        var baseUtc = DateTimeOffset.UtcNow.AddMinutes(-5);

        Assert.Equal(HttpStatusCode.Accepted, (await PostTelemetryAsync(client, scenario, vehicle, "metric-1", baseUtc.AddSeconds(5), 1)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await PostTelemetryAsync(client, scenario, vehicle, "metric-2", baseUtc.AddSeconds(10), 2)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await PostTelemetryAsync(client, scenario, vehicle, "metric-1", baseUtc.AddSeconds(5), 1)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await PostTelemetryAsync(client, scenario, vehicle, "metric-older", baseUtc, 0)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/internal/v1/tracking/events", new IngestTelemetryRequest(
            scenario.OrganizationId,
            vehicle.VehicleId,
            vehicle.DeviceId,
            "metric-implausible",
            baseUtc.AddSeconds(11),
            10.0,
            9.1829,
            42,
            90,
            2,
            5,
            "reliability-test"))).StatusCode);

        Assert.Contains(collector.Records, record => HasTag(record, "result", "accepted"));
        Assert.Contains(collector.Records, record => HasTag(record, "result", "duplicate"));
        Assert.Contains(collector.Records, record => HasTag(record, "result", "out_of_order"));
        Assert.Contains(collector.Records, record => HasTag(record, "result", "rejected"));
        Assert.Contains(collector.Records, record => record.Instrument == "fleetops.tracking.ingest.duration" && record.Value >= 0);
    }

    [Fact]
    public async Task HostedDemoEngineLoopRecordsTickAndEmissionMetrics()
    {
        using var collector = new MetricCollector();
        var organizationId = Guid.NewGuid();
        var bindings = Enumerable.Range(0, 10)
            .Select(index => new DemoVehicleBinding(Guid.NewGuid(), $"DEV-{index:D2}"))
            .ToList();
        var emitter = new RecordingEmitter();
        var engine = new DeterministicDemoScenarioEngine(new DeterministicDemoClock(DateTimeOffset.UtcNow));
        var service = new DemoScenarioHostedService(
            engine,
            new DemoScenarioCatalog(),
            emitter,
            new StubFleetSource(new DemoFleetDefinition(organizationId, bindings)),
            new NullStateStore(),
            Options.Create(new DemoEngineOptions
            {
                Enabled = true,
                RuntimeMode = "Demo",
                ApiBaseUrl = "http://localhost:9",
                TickSeconds = 1,
                Scenario = "NORMAL_SHIFT",
                Seed = 3101,
                StatePath = "unused"
            }),
            NullLogger<DemoScenarioHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
            while (emitter.Events.Count < 10 && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.True(emitter.Events.Count >= 10, "The hosted Demo engine did not emit telemetry.");
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        Assert.Contains(
            collector.Records,
            record => record.Instrument == "fleetops.demo.engine.telemetry.emitted"
                && record.Tags.TryGetValue("scenario", out var scenario)
                && Equals(scenario, "NormalShift")
                && record.Value >= 10);
        Assert.Contains(collector.Records, record => record.Instrument == "fleetops.demo.engine.tick.duration");
        Assert.Contains(collector.Records, record => record.Instrument == "fleetops.demo.engine.state.save.duration");
    }

    private static bool HasTag(MetricRecord record, string key, string value) =>
        record.Tags.TryGetValue(key, out var actual) && string.Equals(actual as string, value, StringComparison.Ordinal);

    private static async Task<TrackingScenarioResponse> LoadScenarioAsync(HttpClient client, string slug, int maxVehicles)
    {
        var reset = await client.PostAsync($"/api/internal/v1/tracking/scenarios/{slug}/reset", null);
        reset.EnsureSuccessStatusCode();
        return (await client.GetFromJsonAsync<TrackingScenarioResponse>($"/api/internal/v1/tracking/scenarios/{slug}?maxVehicles={maxVehicles}"))!;
    }

    private static Task<HttpResponseMessage> PostTelemetryAsync(
        HttpClient client,
        TrackingScenarioResponse scenario,
        TrackingScenarioVehicleResponse vehicle,
        string eventId,
        DateTimeOffset recordedAtUtc,
        long sequence) =>
        client.PostAsJsonAsync("/api/internal/v1/tracking/events", new IngestTelemetryRequest(
            scenario.OrganizationId,
            vehicle.VehicleId,
            vehicle.DeviceId,
            eventId,
            recordedAtUtc,
            48.7758,
            9.1829,
            42,
            90,
            sequence,
            5,
            "reliability-test"));

    private sealed class RecordingEmitter : IDemoTelemetryEmitter
    {
        public ConcurrentQueue<DemoTelemetryEvent> Events { get; } = new();

        public Task EmitAsync(DemoTelemetryEvent telemetry, CancellationToken cancellationToken)
        {
            Events.Enqueue(telemetry);
            return Task.CompletedTask;
        }
    }

    private sealed class StubFleetSource(DemoFleetDefinition fleet) : IDemoFleetSource
    {
        public Task<DemoFleetDefinition> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(fleet);
    }

    private sealed class NullStateStore : IDemoScenarioStateStore
    {
        public Task<DemoScenarioSnapshot?> LoadAsync(CancellationToken cancellationToken) => Task.FromResult<DemoScenarioSnapshot?>(null);
        public Task SaveAsync(DemoScenarioSnapshot snapshot, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

internal sealed record MetricRecord(string Instrument, IReadOnlyDictionary<string, object?> Tags, double Value);

internal sealed class MetricCollector : IDisposable
{
    private readonly MeterListener _listener = new();

    public MetricCollector()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (string.Equals(instrument.Meter.Name, FleetOpsMetrics.MeterName, StringComparison.Ordinal))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.Start();
    }

    public ConcurrentQueue<MetricRecord> Records { get; } = new();

    public void Dispose() => _listener.Dispose();

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            dictionary[tag.Key] = tag.Value;
        }

        Records.Enqueue(new MetricRecord(instrument.Name, dictionary, value));
    }
}
