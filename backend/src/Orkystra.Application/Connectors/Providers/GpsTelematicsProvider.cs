using Orkystra.Contracts.Connectors;

namespace Orkystra.Application.Connectors.Providers;

public sealed class GpsTelematicsProvider : IGpsProviderAdapter
{
    public string ProviderId => "gps-telematics-adapter";

    public string ProviderName => "GPS Telematics Adapter";

    public ProviderDomain Domain => ProviderDomain.Gps;

    public ProviderKind Kind => ProviderKind.Connector;

    public ValueTask<ProviderHealthReport> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(
            new ProviderHealthReport(
                ProviderId,
                ProviderName,
                ProviderHealthStatus.Healthy,
                DateTimeOffset.UtcNow,
                "GPS adapter skeleton can expose canonical truck-position snapshots.",
                ["position-snapshot", "event-stream-ready"]));
    }

    public ValueTask<ProviderCapabilitySet> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(
            new ProviderCapabilitySet(
                CanRead: true,
                CanWrite: false,
                CanStreamEvents: true,
                CanIngestCommands: false,
                CanQueryHistory: true,
                SupportsReadOnlyMode: true,
                CanReplayData: true));
    }

    public ValueTask<ProviderSyncStatus> GetSyncStatusAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(
            new ProviderSyncStatus(
                ProviderId,
                "stream",
                DateTimeOffset.UtcNow.AddMinutes(-2),
                DateTimeOffset.UtcNow,
                "connected",
                "Streaming adapter skeleton is aligned to canonical GPS positions."));
    }

    public ValueTask<ProviderSchemaDescription> DescribeSchemaAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(
            new ProviderSchemaDescription(
                ProviderId,
                "gps-position-event",
                [
                    new ProviderSchemaField("truck_reference", "string", true, "Truck.Reference", "External truck reference"),
                    new ProviderSchemaField("latitude", "decimal", true, "GpsPosition.Latitude", "Latitude in decimal degrees"),
                    new ProviderSchemaField("longitude", "decimal", true, "GpsPosition.Longitude", "Longitude in decimal degrees"),
                    new ProviderSchemaField("speed_kph", "decimal", false, "GpsPosition.SpeedKph", "Vehicle speed in km/h"),
                    new ProviderSchemaField("recorded_at", "datetime", true, "GpsPosition.RecordedAt", "Timestamp of the reading")
                ]));
    }

    public ValueTask<IReadOnlyCollection<GpsPositionSnapshot>> ReadPositionsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<GpsPositionSnapshot> positions =
        [
            new GpsPositionSnapshot(
                Guid.Parse("cf7c6cc8-7b55-49d4-94ff-a5ee9e340856"),
                "TRK-19",
                48.8566m,
                2.3522m,
                58.4m,
                DateTimeOffset.UtcNow.AddMinutes(-1))
        ];

        return ValueTask.FromResult(positions);
    }
}
