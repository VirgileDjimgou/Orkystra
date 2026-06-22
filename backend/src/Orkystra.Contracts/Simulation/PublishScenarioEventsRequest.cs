namespace Orkystra.Contracts.Simulation;

public sealed record PublishScenarioEventsRequest(
    string Name,
    int Seed,
    int AdvanceMinutes,
    bool IncludeDisruption,
    bool CompleteScenario)
{
    public static PublishScenarioEventsRequest Default { get; } =
        new(
            "Live MQTT Demo",
            42,
            15,
            true,
            true);
}
