namespace FleetOps.Api.Demo;

public sealed class PublicDemoOptions
{
    public const string SectionName = "PublicDemo";
    public const string TenantSlug = "public-demo";
    public const string OperatorEmail = "public-demo@fleetops.invalid";

    public bool Enabled { get; init; }
    public bool SideEffectsSandboxed { get; init; } = true;
    public int SessionLifetimeSeconds { get; init; } = 900;
    public int LaunchPermitLimit { get; init; } = 10;
    public int MaxConcurrentSessions { get; init; } = 20;
}
