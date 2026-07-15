namespace FleetOps.Api.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "FleetOps";
    public string Audience { get; init; } = "FleetOps.Web";
    public string SigningKey { get; init; } = "FleetOps_LocalDevelopment_ChangeThisSigningKey_123456789";
    public int TokenLifetimeMinutes { get; init; } = 60;
}
