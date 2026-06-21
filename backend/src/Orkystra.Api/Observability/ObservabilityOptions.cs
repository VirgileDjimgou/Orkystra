namespace Orkystra.Api.Observability;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public bool EnableAuditLogging { get; init; } = true;

    public string AuditLogFilePath { get; init; } = Path.Combine("output", "audit", "audit-log.jsonl");

    public int AuditReadLimit { get; init; } = 200;
}
