namespace Orkystra.Contracts.Simulation;

public sealed record ScenarioSummaryReadModel(
    Guid ScenarioId,
    string Name,
    int Seed,
    string Status,
    DateTimeOffset CurrentTime,
    int InjectedEventCount);
