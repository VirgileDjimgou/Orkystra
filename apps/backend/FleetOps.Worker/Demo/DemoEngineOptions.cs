namespace FleetOps.Worker.Demo;

public sealed class DemoEngineOptions
{
    public const string SectionName = "DemoEngine";
    public bool Enabled { get; init; }
    public int TickSeconds { get; init; } = 5;
    public string? ApiBaseUrl { get; init; }
}
