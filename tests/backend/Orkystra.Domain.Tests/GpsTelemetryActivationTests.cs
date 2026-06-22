using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orkystra.Api.Connectors;
using Orkystra.Api.Eventing;
using Orkystra.Api.Gps;
using Orkystra.Application.Eventing;
using Orkystra.Contracts.Connectors;
using Orkystra.Contracts.Eventing;

namespace Orkystra.Domain.Tests;

public sealed class GpsTelemetryActivationTests
{
    [Fact]
    public void MqttEnvelopeSerializer_roundtrips_gps_position_event()
    {
        var serializer = new MqttEnvelopeSerializer();
        var position = new GpsPositionSnapshot(
            Guid.NewGuid(),
            "TRK-GPS-1",
            48.8566m,
            2.3522m,
            52.4m,
            DateTimeOffset.UtcNow);
        var envelope = IntegrationEventEnvelopeFactory.Create(
            position,
            boundedContext: "Gps",
            aggregateType: "Truck",
            aggregateId: position.TruckId,
            eventType: GpsPositionProjection.GpsPositionReportedEventType,
            topicOverride: "fleet/gps/demo",
            headers: new Dictionary<string, string> { ["providerId"] = "gps-telematics-adapter" });

        var serialized = serializer.Serialize(envelope);
        var roundTripped = serializer.Deserialize(serialized);

        Assert.Equal("fleet/gps/demo", roundTripped.Topic);
        Assert.Equal(GpsPositionProjection.GpsPositionReportedEventType, roundTripped.EventType);
        Assert.IsType<GpsPositionSnapshot>(roundTripped.Payload);
        Assert.Equal("TRK-GPS-1", ((GpsPositionSnapshot)roundTripped.Payload).TruckReference);
    }

    [Fact]
    public async Task EventBackboneMessageDispatcher_projects_latest_gps_position_snapshot()
    {
        var serializer = new MqttEnvelopeSerializer();
        var projection = new GpsPositionProjection();
        var runner = new IdempotentProjectionRunner([projection], new InMemoryInboxStateStore());
        var telemetryStore = new EventBackboneTelemetryStore(Options.Create(new EventBackboneOptions()));
        var dispatcher = new EventBackboneMessageDispatcher(
            serializer,
            runner,
            telemetryStore,
            NullLogger<EventBackboneMessageDispatcher>.Instance);
        var truckId = Guid.NewGuid();

        var firstEnvelope = IntegrationEventEnvelopeFactory.Create(
            new GpsPositionSnapshot(truckId, "TRK-GPS-1", 48.8566m, 2.3522m, 45.1m, DateTimeOffset.UtcNow.AddMinutes(-2)),
            "Gps",
            "Truck",
            truckId,
            GpsPositionProjection.GpsPositionReportedEventType,
            topicOverride: "fleet/gps/demo");
        var secondEnvelope = IntegrationEventEnvelopeFactory.Create(
            new GpsPositionSnapshot(truckId, "TRK-GPS-1", 48.9000m, 2.4000m, 48.5m, DateTimeOffset.UtcNow),
            "Gps",
            "Truck",
            truckId,
            GpsPositionProjection.GpsPositionReportedEventType,
            topicOverride: "fleet/gps/demo");

        await dispatcher.DispatchAsync(firstEnvelope.Topic, System.Text.Encoding.UTF8.GetBytes(serializer.Serialize(firstEnvelope)));
        await dispatcher.DispatchAsync(secondEnvelope.Topic, System.Text.Encoding.UTF8.GetBytes(serializer.Serialize(secondEnvelope)));

        var positions = projection.ListAll();
        Assert.Single(positions);
        Assert.Equal(48.9000m, positions.Single().Latitude);
        Assert.Equal("fleet/gps/demo", telemetryStore.Snapshot().LastTopic);
    }

    [Fact]
    public async Task GpsTelemetryWorkflowService_publishes_provider_positions_to_runtime_stream_topic()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("orkystra-gps-workflow-tests");

        try
        {
            var runtimeStore = new ProviderRuntimeStore(
                Options.Create(new ProviderRuntimeOptions
                {
                    Providers =
                    [
                        new ProviderRuntimeSettings
                        {
                            ProviderId = "csv-warehouse-import",
                            Enabled = true,
                            Environment = "local-demo",
                            Settings = new Dictionary<string, string>
                            {
                                ["sourcePath"] = "data/imports/warehouse-demo.csv",
                                ["importSchedule"] = "manual"
                            }
                        },
                        new ProviderRuntimeSettings
                        {
                            ProviderId = "rest-transport-adapter",
                            Enabled = true,
                            Environment = "sandbox",
                            Settings = new Dictionary<string, string>
                            {
                                ["baseUrl"] = "https://sandbox.example.invalid/transport",
                                ["authMode"] = "api-key"
                            }
                        },
                        new ProviderRuntimeSettings
                        {
                            ProviderId = "gps-telematics-adapter",
                            Enabled = true,
                            Environment = "local-demo",
                            Settings = new Dictionary<string, string>
                            {
                                ["streamTopic"] = "fleet/gps/demo",
                                ["snapshotIntervalSeconds"] = "15"
                            }
                        }
                    ]
                }),
                Path.Combine(tempDirectory.FullName, "appsettings.Local.json"));

            var registryFactory = new ProviderRegistryFactory();
            var publisher = new RecordingEventBackbonePublisher();
            var service = new GpsTelemetryWorkflowService(registryFactory, runtimeStore, publisher);

            var result = await service.PublishLatestPositionsAsync("tenant-alpha");

            Assert.Equal("fleet/gps/demo", result.Topic);
            Assert.Equal(1, result.PublishedCount);
            Assert.Single(publisher.Envelopes);
            Assert.Equal("gps-telematics-adapter", publisher.Envelopes.Single().Headers["providerId"]);
            Assert.Equal("tenant-alpha", publisher.Envelopes.Single().Headers["tenant-key"]);
            Assert.Equal(GpsPositionProjection.GpsPositionReportedEventType, publisher.Envelopes.Single().EventType);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    private sealed class RecordingEventBackbonePublisher : IEventBackbonePublisher
    {
        public List<IEventEnvelope> Envelopes { get; } = [];

        public ValueTask PublishAsync(IEventEnvelope envelope, CancellationToken cancellationToken = default)
        {
            Envelopes.Add(envelope);
            return ValueTask.CompletedTask;
        }
    }
}
