namespace Orkystra.Contracts.Warehouse;

public sealed record WarehouseDetailReadModel(
    Guid WarehouseId,
    string Name,
    int ZoneCount,
    int RackCount,
    int SlotCount,
    int OccupiedDockCount,
    int StoredPalletCount,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyCollection<WarehouseZoneReadModel> Zones,
    IReadOnlyCollection<WarehouseDockReadModel> Docks);
