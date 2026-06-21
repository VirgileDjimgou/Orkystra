namespace Orkystra.Domain.Simulation;

public sealed record SyntheticWorldState(
    IReadOnlyCollection<SyntheticWarehouseDefinition> Warehouses,
    IReadOnlyCollection<SyntheticOrderDefinition> Orders,
    IReadOnlyCollection<SyntheticTruckDefinition> Trucks);
