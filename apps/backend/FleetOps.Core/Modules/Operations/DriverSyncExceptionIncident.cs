using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Operations;

public sealed class DriverSyncExceptionIncident : TenantEntity
{
    private DriverSyncExceptionIncident() { }

    public DriverSyncExceptionIncident(
        Guid organizationId,
        Guid missionId,
        Guid driverId,
        string incidentKey,
        string incidentCode,
        string severity,
        string scopeType,
        string message,
        string? commandId,
        DateTimeOffset occurredAtUtc)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        if (missionId == Guid.Empty)
        {
            throw new ArgumentException("Mission is required.", nameof(missionId));
        }

        if (driverId == Guid.Empty)
        {
            throw new ArgumentException("Driver is required.", nameof(driverId));
        }

        OrganizationId = organizationId;
        MissionId = missionId;
        DriverId = driverId;
        IncidentKey = RequireNonEmpty(incidentKey, nameof(incidentKey), 160);
        IncidentCode = RequireNonEmpty(incidentCode, nameof(incidentCode), 64);
        Severity = RequireNonEmpty(severity, nameof(severity), 24);
        ScopeType = RequireNonEmpty(scopeType, nameof(scopeType), 48);
        Message = RequireNonEmpty(message, nameof(message), 320);
        LastCommandId = string.IsNullOrWhiteSpace(commandId) ? null : commandId.Trim();
        FirstOccurredAtUtc = occurredAtUtc.ToUniversalTime();
        LastOccurredAtUtc = occurredAtUtc.ToUniversalTime();
        OccurrenceCount = 1;
    }

    public Guid MissionId { get; private set; }
    public Guid DriverId { get; private set; }
    public string IncidentKey { get; private set; } = string.Empty;
    public string IncidentCode { get; private set; } = string.Empty;
    public string Severity { get; private set; } = string.Empty;
    public string ScopeType { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string? LastCommandId { get; private set; }
    public DateTimeOffset FirstOccurredAtUtc { get; private set; }
    public DateTimeOffset LastOccurredAtUtc { get; private set; }
    public int OccurrenceCount { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }
    public long RowVersion { get; private set; }

    public void RecordOccurrence(string message, string severity, string? commandId, DateTimeOffset occurredAtUtc)
    {
        Message = RequireNonEmpty(message, nameof(message), 320);
        Severity = RequireNonEmpty(severity, nameof(severity), 24);
        LastCommandId = string.IsNullOrWhiteSpace(commandId) ? LastCommandId : commandId.Trim();
        LastOccurredAtUtc = occurredAtUtc.ToUniversalTime();
        OccurrenceCount++;
        ResolvedAtUtc = null;
        RowVersion++;
    }

    public void Resolve(DateTimeOffset resolvedAtUtc)
    {
        ResolvedAtUtc = resolvedAtUtc.ToUniversalTime();
        RowVersion++;
    }

    private static string RequireNonEmpty(string value, string parameterName, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
