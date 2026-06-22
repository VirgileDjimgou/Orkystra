using Orkystra.Api.Connectors;
using Orkystra.Api.Eventing;
using Orkystra.Application.Connectors;
using Orkystra.Application.Eventing;
using Orkystra.Contracts.Connectors;
using Orkystra.Contracts.Eventing;

namespace Orkystra.Api.Gps;

public sealed class GpsTelemetryWorkflowService
{
    private readonly ProviderRegistryFactory _providerRegistryFactory;
    private readonly ProviderRuntimeStore _runtimeStore;
    private readonly IEventBackbonePublisher _eventPublisher;

    public GpsTelemetryWorkflowService(
        ProviderRegistryFactory providerRegistryFactory,
        ProviderRuntimeStore runtimeStore,
        IEventBackbonePublisher eventPublisher)
    {
        _providerRegistryFactory = providerRegistryFactory;
        _runtimeStore = runtimeStore;
        _eventPublisher = eventPublisher;
    }

    public async ValueTask<PublishGpsTelemetryResponse> PublishLatestPositionsAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var gpsProvider = _providerRegistryFactory.CreateRegistry()
            .ListByDomain(ProviderDomain.Gps)
            .OfType<IGpsProviderAdapter>()
            .First();

        var positions = await gpsProvider.ReadPositionsAsync(cancellationToken);
        var runtime = _runtimeStore.GetProvider("gps-telematics-adapter");
        var topic = runtime is not null &&
                    runtime.Settings.TryGetValue("streamTopic", out var configuredTopic) &&
                    !string.IsNullOrWhiteSpace(configuredTopic)
            ? configuredTopic
            : "fleet/gps/demo";
        var correlationId = Guid.NewGuid();
        var envelopes = new List<IEventEnvelope>(positions.Count);

        foreach (var position in positions)
        {
            var headers = new Dictionary<string, string>
            {
                ["source"] = "gps-provider",
                ["providerId"] = gpsProvider.ProviderId,
                ["tenant-key"] = tenantId,
                ["truckReference"] = position.TruckReference
            };

            var envelope = IntegrationEventEnvelopeFactory.Create(
                position,
                boundedContext: "Gps",
                aggregateType: "Truck",
                aggregateId: position.TruckId,
                eventType: GpsPositionProjection.GpsPositionReportedEventType,
                topicOverride: topic,
                correlationId: correlationId,
                headers: headers);

            envelopes.Add(envelope);
            await _eventPublisher.PublishAsync(envelope, cancellationToken);
        }

        return new PublishGpsTelemetryResponse(
            topic,
            envelopes.Count,
            positions,
            DateTimeOffset.UtcNow);
    }
}
