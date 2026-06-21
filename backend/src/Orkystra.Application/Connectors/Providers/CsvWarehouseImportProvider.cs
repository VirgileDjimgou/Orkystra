using Orkystra.Contracts.Connectors;
using Orkystra.Contracts.Warehouse;

namespace Orkystra.Application.Connectors.Providers;

public sealed class CsvWarehouseImportProvider : IWarehouseProviderAdapter
{
    public string ProviderId => "csv-warehouse-import";

    public string ProviderName => "CSV Warehouse Import";

    public ProviderDomain Domain => ProviderDomain.Warehouse;

    public ProviderKind Kind => ProviderKind.Connector;

    public ValueTask<ProviderHealthReport> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(
            new ProviderHealthReport(
                ProviderId,
                ProviderName,
                ProviderHealthStatus.Healthy,
                DateTimeOffset.UtcNow,
                "CSV adapter skeleton is ready to validate and map warehouse import files.",
                ["schema-ready", "read-only-mode"]));
    }

    public ValueTask<ProviderCapabilitySet> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(
            new ProviderCapabilitySet(
                CanRead: true,
                CanWrite: false,
                CanStreamEvents: false,
                CanIngestCommands: false,
                CanQueryHistory: false,
                SupportsReadOnlyMode: true,
                CanReplayData: true));
    }

    public ValueTask<ProviderSyncStatus> GetSyncStatusAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(
            new ProviderSyncStatus(
                ProviderId,
                "import",
                DateTimeOffset.UtcNow.AddMinutes(-18),
                DateTimeOffset.UtcNow.AddMinutes(-18),
                "ready",
                "Demo import snapshot mapped into canonical warehouse summaries."));
    }

    public ValueTask<ProviderSchemaDescription> DescribeSchemaAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(
            new ProviderSchemaDescription(
                ProviderId,
                "warehouse-csv-row",
                [
                    new ProviderSchemaField("warehouse_name", "string", true, "Warehouse.Name", "Warehouse display name"),
                    new ProviderSchemaField("zone_count", "integer", true, "Warehouse.ZoneCount", "Number of zones"),
                    new ProviderSchemaField("rack_count", "integer", true, "Warehouse.RackCount", "Number of racks"),
                    new ProviderSchemaField("slot_count", "integer", true, "Warehouse.SlotCount", "Number of slots"),
                    new ProviderSchemaField("occupied_dock_count", "integer", true, "Warehouse.OccupiedDockCount", "Occupied docks"),
                    new ProviderSchemaField("stored_pallet_count", "integer", true, "Warehouse.StoredPalletCount", "Stored pallets")
                ]));
    }

    public ValueTask<IReadOnlyCollection<WarehouseSummaryReadModel>> ReadWarehousesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<WarehouseSummaryReadModel> warehouses =
        [
            new WarehouseSummaryReadModel(
                Guid.Parse("db9a789f-9df8-45ff-a252-96d4319c2f12"),
                "North Hub A",
                4,
                18,
                820,
                3,
                612),
            new WarehouseSummaryReadModel(
                Guid.Parse("3f224c42-00a5-49a6-955c-c8114d0a6b81"),
                "West Flow Center",
                3,
                14,
                640,
                2,
                401)
        ];

        return ValueTask.FromResult(warehouses);
    }
}
