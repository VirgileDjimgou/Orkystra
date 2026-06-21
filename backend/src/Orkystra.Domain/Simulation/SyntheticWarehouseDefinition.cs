namespace Orkystra.Domain.Simulation;

public sealed record SyntheticWarehouseDefinition(
    string WarehouseReference,
    string Name,
    int ZoneCount,
    int DockCount);
