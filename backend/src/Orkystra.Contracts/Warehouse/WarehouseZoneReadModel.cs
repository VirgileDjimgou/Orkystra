namespace Orkystra.Contracts.Warehouse;

public sealed record WarehouseZoneReadModel(
    string Code,
    string Name,
    string Status,
    string Description,
    int UtilizationPercent,
    int PalletCount,
    string ThroughputLabel);
