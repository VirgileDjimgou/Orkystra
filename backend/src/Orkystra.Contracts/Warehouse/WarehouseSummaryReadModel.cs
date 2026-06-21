namespace Orkystra.Contracts.Warehouse;

public sealed record WarehouseSummaryReadModel(
    Guid WarehouseId,
    string Name,
    int ZoneCount,
    int RackCount,
    int SlotCount,
    int OccupiedDockCount,
    int StoredPalletCount);
