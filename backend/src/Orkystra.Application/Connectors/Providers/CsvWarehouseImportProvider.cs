using Orkystra.Contracts.Connectors;
using Orkystra.Contracts.Warehouse;

namespace Orkystra.Application.Connectors.Providers;

public sealed class CsvWarehouseImportProvider : IWarehouseProjectionProviderAdapter
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
        IReadOnlyCollection<WarehouseSummaryReadModel> warehouses = BuildWarehouseDetails()
            .Select(warehouse => new WarehouseSummaryReadModel(
                warehouse.WarehouseId,
                warehouse.Name,
                warehouse.ZoneCount,
                warehouse.RackCount,
                warehouse.SlotCount,
                warehouse.OccupiedDockCount,
                warehouse.StoredPalletCount))
            .ToArray();

        return ValueTask.FromResult(warehouses);
    }

    public ValueTask<IReadOnlyCollection<WarehouseDetailReadModel>> ReadWarehouseDetailsAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(BuildWarehouseDetails());
    }

    private static IReadOnlyCollection<WarehouseDetailReadModel> BuildWarehouseDetails()
    {
        return
        [
            new WarehouseDetailReadModel(
                Guid.Parse("db9a789f-9df8-45ff-a252-96d4319c2f12"),
                "North Hub A",
                4,
                18,
                820,
                3,
                612,
                DateTimeOffset.Parse("2026-06-20T10:15:00Z"),
                [
                    new WarehouseZoneReadModel("INB", "Inbound Buffer", "Stable", "Inbound pallets waiting for slotting decisions.", 62, 124, "38 pallets/h"),
                    new WarehouseZoneReadModel("AMB", "Ambient Picking", "Watch", "Ambient picking wave with rising congestion.", 81, 228, "57 picks/h"),
                    new WarehouseZoneReadModel("COL", "Cold Reserve", "Stable", "Cold chain reserve with stable replenishment rhythm.", 54, 93, "19 pallets/h"),
                    new WarehouseZoneReadModel("XDK", "Cross Dock", "Critical", "Cross-dock zone impacted by late carrier arrival.", 92, 167, "12 trucks queued")
                ],
                [
                    new WarehouseDockReadModel("D-01", "Occupied", "Trailer TRK-19 staging late handoff"),
                    new WarehouseDockReadModel("D-02", "Occupied", "Outbound wave RT-204 loading"),
                    new WarehouseDockReadModel("D-03", "Occupied", "Inbound unload slot active"),
                    new WarehouseDockReadModel("D-04", "Available", "Buffer dock ready")
                ]),
            new WarehouseDetailReadModel(
                Guid.Parse("3f224c42-00a5-49a6-955c-c8114d0a6b81"),
                "West Flow Center",
                3,
                14,
                640,
                2,
                401,
                DateTimeOffset.Parse("2026-06-20T10:12:00Z"),
                [
                    new WarehouseZoneReadModel("RET", "Returns", "Stable", "Returns lane with controlled backlog.", 45, 88, "23 cases/h"),
                    new WarehouseZoneReadModel("FUL", "Fulfillment", "Watch", "E-commerce fulfillment under peak order burst.", 79, 205, "91 lines/h"),
                    new WarehouseZoneReadModel("STG", "Staging", "Stable", "Outbound staging synced with carrier windows.", 63, 108, "7 trailers/h")
                ],
                [
                    new WarehouseDockReadModel("W-01", "Occupied", "Parcel wave consolidation"),
                    new WarehouseDockReadModel("W-02", "Occupied", "Carrier arrival on schedule"),
                    new WarehouseDockReadModel("W-03", "Available", "Returns overflow backup")
                ])
        ];
    }
}
