namespace Orkystra.Contracts.Simulation;

public sealed record PublishScenarioEventsResponse(
    Guid ScenarioId,
    string Topic,
    int PublishedCount,
    IReadOnlyCollection<string> EventTypes,
    DateTimeOffset PublishedAtUtc);
