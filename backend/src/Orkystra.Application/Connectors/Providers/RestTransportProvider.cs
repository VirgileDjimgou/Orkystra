using Orkystra.Contracts.Connectors;
using Orkystra.Contracts.Transport;

namespace Orkystra.Application.Connectors.Providers;

public sealed class RestTransportProvider : ITransportProviderAdapter
{
    public string ProviderId => "rest-transport-adapter";

    public string ProviderName => "REST Transport Adapter";

    public ProviderDomain Domain => ProviderDomain.Transport;

    public ProviderKind Kind => ProviderKind.Connector;

    public ValueTask<ProviderHealthReport> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(
            new ProviderHealthReport(
                ProviderId,
                ProviderName,
                ProviderHealthStatus.Degraded,
                DateTimeOffset.UtcNow,
                "REST adapter skeleton is available but not yet configured against a live upstream service.",
                ["endpoint-unconfigured", "schema-ready"]));
    }

    public ValueTask<ProviderCapabilitySet> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(
            new ProviderCapabilitySet(
                CanRead: true,
                CanWrite: true,
                CanStreamEvents: false,
                CanIngestCommands: true,
                CanQueryHistory: true,
                SupportsReadOnlyMode: true,
                CanReplayData: false));
    }

    public ValueTask<ProviderSyncStatus> GetSyncStatusAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(
            new ProviderSyncStatus(
                ProviderId,
                "pull",
                DateTimeOffset.UtcNow.AddMinutes(-9),
                DateTimeOffset.UtcNow,
                "degraded-live-snapshot",
                "Demo transport projection is available while upstream configuration remains partial."));
    }

    public ValueTask<ProviderSchemaDescription> DescribeSchemaAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(
            new ProviderSchemaDescription(
                ProviderId,
                "transport-route-resource",
                [
                    new ProviderSchemaField("route_reference", "string", true, "Route.Reference", "External route reference"),
                    new ProviderSchemaField("truck_reference", "string", true, "Truck.Reference", "Assigned truck"),
                    new ProviderSchemaField("status", "string", true, "Route.Status", "Normalized route status"),
                    new ProviderSchemaField("stop_count", "integer", true, "Route.StopCount", "Number of stops"),
                    new ProviderSchemaField("shipment_count", "integer", true, "Route.ShipmentCount", "Number of shipments"),
                    new ProviderSchemaField("completed_delivery_count", "integer", true, "Route.CompletedDeliveryCount", "Completed deliveries")
                ]));
    }

    public ValueTask<IReadOnlyCollection<RouteSummaryReadModel>> ReadRoutesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<RouteSummaryReadModel> routes =
        [
            new RouteSummaryReadModel(
                Guid.Parse("5024fa82-f658-46c8-88bf-aece07d56f09"),
                "RT-204",
                Guid.Parse("0d91dc2f-3a74-4562-96a6-c8de611f699d"),
                "TRK-11",
                "On time",
                5,
                22,
                2),
            new RouteSummaryReadModel(
                Guid.Parse("528c1588-40fd-451b-8c86-2caa625602de"),
                "RT-318",
                Guid.Parse("2a398a30-61cf-4fc3-a18d-e491530b4f24"),
                "TRK-07",
                "At risk",
                4,
                15,
                1),
            new RouteSummaryReadModel(
                Guid.Parse("9f91e82e-226a-48f7-a94c-907b79431739"),
                "RT-412",
                Guid.Parse("cf7c6cc8-7b55-49d4-94ff-a5ee9e340856"),
                "TRK-19",
                "Delayed",
                6,
                27,
                3)
        ];

        return ValueTask.FromResult(routes);
    }
}
