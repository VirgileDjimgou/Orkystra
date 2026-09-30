namespace FleetOps.Worker.Demo;

public sealed class DemoEngineOptions
{
    public const string SectionName = "DemoEngine";
    public bool Enabled { get; init; }
    public string RuntimeMode { get; init; } = "Disabled";
    public int TickSeconds { get; init; } = 5;
    public string? ApiBaseUrl { get; init; }
    public string? InternalApiKey { get; init; }
    public string? ApiAccessToken { get; init; }
    public string? DriverAccessToken { get; init; }
    public string OrganizationSlug { get; init; } = "northwind";
    public string Scenario { get; init; } = "NORMAL_SHIFT";
    public int Seed { get; init; } = 2701;
    public double SpeedMultiplier { get; init; } = 1;
    public string StatePath { get; init; } = ".runtime/demo-engine-state.json";
    public bool SideEffectsSandboxed { get; init; } = true;
    public List<VirtualDriverOptions> Agents { get; init; } = [];
}

public sealed class VirtualDriverOptions
{
    public Guid AgentId { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid DriverId { get; init; }
    public Guid VehicleId { get; init; }
    public Guid MissionId { get; init; }
    public Guid StopId { get; init; }
    public string? DriverAccessToken { get; init; }
}
