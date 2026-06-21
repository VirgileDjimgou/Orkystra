namespace Orkystra.Api.Observability;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public bool EnableAuditLogging { get; init; } = true;
}
