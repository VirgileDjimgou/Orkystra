namespace Orkystra.Contracts.Bootstrap;

public sealed record BootstrapDemoRequest(
    string ScenarioName,
    int Seed,
    int AdvanceMinutes,
    bool IncludeDisruption)
{
    public static BootstrapDemoRequest Default { get; } = new(
        "Seeded Demo",
        42,
        15,
        true);
}
