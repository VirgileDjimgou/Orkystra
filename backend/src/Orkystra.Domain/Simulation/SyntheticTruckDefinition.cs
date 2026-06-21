namespace Orkystra.Domain.Simulation;

public sealed record SyntheticTruckDefinition(
    string TruckReference,
    decimal CapacityKilograms,
    string HomeDepot);
