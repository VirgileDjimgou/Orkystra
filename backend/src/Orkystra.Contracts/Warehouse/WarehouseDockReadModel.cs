namespace Orkystra.Contracts.Warehouse;

public sealed record WarehouseDockReadModel(
    string Code,
    string Status,
    string ActivityLabel);
