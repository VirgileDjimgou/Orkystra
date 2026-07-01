namespace Orkystra.Contracts.Warehouse;

public sealed record WarehouseWorkbenchItemReadModel(
    string ExceptionId,
    string Severity,
    string Category,
    string Title,
    string Detail,
    string? WarehouseId,
    string? WarehouseName,
    string? ZoneCode,
    string RecommendedAction,
    string ActionLabel,
    IReadOnlyCollection<string> Evidence);
