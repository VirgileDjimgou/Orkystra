namespace Orkystra.Contracts.Warehouse;

public sealed record WarehouseWorkbenchGroupReadModel(
    string GroupKey,
    string Label,
    string HighestSeverity,
    int Count,
    string Summary,
    string RecommendedAction,
    string ActionLabel);
