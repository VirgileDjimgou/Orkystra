namespace Orkystra.Domain.Simulation;

public sealed record SyntheticOrderDefinition(
    string OrderReference,
    int Priority,
    decimal TotalQuantity);
