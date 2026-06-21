namespace Orkystra.Api.Observability;

public sealed record AuditEntry(
    string User,
    string Method,
    string Path,
    DateTimeOffset TimestampUtc,
    string TenantId,
    string Reason,
    string RemoteIp,
    string CorrelationId,
    int StatusCode);
