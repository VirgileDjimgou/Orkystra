using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Alerts;

public sealed class OperationalAlert : TenantEntity
{
    private OperationalAlert() { }

    public OperationalAlert(
        Guid organizationId,
        AlertRuleType ruleType,
        string deduplicationKey,
        AlertSeverity severity,
        string title,
        string message,
        string targetType,
        Guid targetEntityId,
        DateTimeOffset detectedAtUtc)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        if (targetEntityId == Guid.Empty) throw new ArgumentException("Target entity is required.", nameof(targetEntityId));

        OrganizationId = organizationId;
        RuleType = ruleType;
        DeduplicationKey = RequireText(deduplicationKey, nameof(deduplicationKey), 180);
        Severity = severity;
        Title = RequireText(title, nameof(title), 160);
        Message = RequireText(message, nameof(message), 320);
        TargetType = RequireText(targetType, nameof(targetType), 48);
        TargetEntityId = targetEntityId;
        Status = AlertStatus.Open;
        LastDetectedAtUtc = detectedAtUtc.ToUniversalTime();
    }

    public AlertRuleType RuleType { get; private set; }
    public string DeduplicationKey { get; private set; } = string.Empty;
    public AlertSeverity Severity { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public Guid TargetEntityId { get; private set; }
    public AlertStatus Status { get; private set; }
    public Guid? AssignedToUserId { get; private set; }
    public string? AssignedToDisplayName { get; private set; }
    public DateTimeOffset? AssignedAtUtc { get; private set; }
    public Guid? AcknowledgedByUserId { get; private set; }
    public string? AcknowledgedByDisplayName { get; private set; }
    public DateTimeOffset? AcknowledgedAtUtc { get; private set; }
    public DateTimeOffset LastDetectedAtUtc { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }
    public long RowVersion { get; private set; }

    public bool IsOpen => ResolvedAtUtc is null;

    public void Refresh(AlertSeverity severity, string title, string message, DateTimeOffset detectedAtUtc)
    {
        Severity = severity;
        Title = RequireText(title, nameof(title), 160);
        Message = RequireText(message, nameof(message), 320);
        LastDetectedAtUtc = detectedAtUtc.ToUniversalTime();
        if (Status == AlertStatus.Resolved)
        {
            Status = AlertStatus.Open;
            ResolvedAtUtc = null;
        }

        RowVersion++;
    }

    public void Assign(Guid assignedToUserId, string displayName, DateTimeOffset assignedAtUtc)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("Resolved alerts cannot be assigned.");
        }

        AssignedToUserId = assignedToUserId == Guid.Empty
            ? throw new ArgumentException("Assigned user is required.", nameof(assignedToUserId))
            : assignedToUserId;
        AssignedToDisplayName = RequireText(displayName, nameof(displayName), 120);
        AssignedAtUtc = assignedAtUtc.ToUniversalTime();
        RowVersion++;
    }

    public void Acknowledge(Guid acknowledgedByUserId, string displayName, DateTimeOffset acknowledgedAtUtc)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("Resolved alerts cannot be acknowledged.");
        }

        AcknowledgedByUserId = acknowledgedByUserId == Guid.Empty
            ? throw new ArgumentException("Acknowledging user is required.", nameof(acknowledgedByUserId))
            : acknowledgedByUserId;
        AcknowledgedByDisplayName = RequireText(displayName, nameof(displayName), 120);
        AcknowledgedAtUtc = acknowledgedAtUtc.ToUniversalTime();
        Status = AlertStatus.Acknowledged;
        RowVersion++;
    }

    public void Resolve(DateTimeOffset resolvedAtUtc)
    {
        if (ResolvedAtUtc is not null)
        {
            return;
        }

        ResolvedAtUtc = resolvedAtUtc.ToUniversalTime();
        Status = AlertStatus.Resolved;
        RowVersion++;
    }

    private static string RequireText(string value, string parameterName, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
