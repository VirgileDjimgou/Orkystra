namespace Orkystra.Contracts.Warehouse;

public sealed record WarehouseWorkbenchReadModel(
    DateTimeOffset GeneratedAtUtc,
    int ExceptionCount,
    string Summary,
    IReadOnlyCollection<WarehouseWorkbenchGroupReadModel> Groups,
    IReadOnlyCollection<WarehouseWorkbenchItemReadModel> Items);
