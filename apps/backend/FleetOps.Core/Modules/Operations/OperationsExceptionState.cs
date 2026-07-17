using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Operations;

public sealed class OperationsExceptionState : TenantEntity
{
    private OperationsExceptionState() { }

    public OperationsExceptionState(
        Guid organizationId,
        string exceptionKey,
        OperationsExceptionSourceType sourceType,
        Guid sourceEntityId,
        DateTimeOffset detectedAtUtc)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        if (sourceEntityId == Guid.Empty)
        {
            throw new ArgumentException("Source entity is required.", nameof(sourceEntityId));
        }

        OrganizationId = organizationId;
        ExceptionKey = RequireNonEmpty(exceptionKey, nameof(exceptionKey), 120);
        SourceType = sourceType;
        SourceEntityId = sourceEntityId;
        LastDetectedAtUtc = detectedAtUtc.ToUniversalTime();
    }

    public string ExceptionKey { get; private set; } = string.Empty;
    public OperationsExceptionSourceType SourceType { get; private set; }
    public Guid SourceEntityId { get; private set; }
    public Guid? AssignedToUserId { get; private set; }
    public string? AssignedToDisplayName { get; private set; }
    public DateTimeOffset? AssignedAtUtc { get; private set; }
    public Guid? AcknowledgedByUserId { get; private set; }
    public string? AcknowledgedByDisplayName { get; private set; }
    public DateTimeOffset? AcknowledgedAtUtc { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }
    public string? ResolvedByDisplayName { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }
    public string? ResolutionReason { get; private set; }
    public DateTimeOffset? SnoozedUntilUtc { get; private set; }
    public string? SnoozeReason { get; private set; }
    public DateTimeOffset LastDetectedAtUtc { get; private set; }
    public long RowVersion { get; private set; }

    public void Refresh(DateTimeOffset detectedAtUtc)
    {
        LastDetectedAtUtc = detectedAtUtc.ToUniversalTime();
        if (ResolvedAtUtc is not null && ResolvedAtUtc < LastDetectedAtUtc)
        {
            ResolvedAtUtc = null;
            ResolvedByUserId = null;
            ResolvedByDisplayName = null;
            ResolutionReason = null;
        }

        Touch();
    }

    public void Assign(Guid userId, string displayName, DateTimeOffset assignedAtUtc)
    {
        AssignedToUserId = userId == Guid.Empty
            ? throw new ArgumentException("Assigned user is required.", nameof(userId))
            : userId;
        AssignedToDisplayName = RequireNonEmpty(displayName, nameof(displayName), 120);
        AssignedAtUtc = assignedAtUtc.ToUniversalTime();
        Touch();
    }

    public void Acknowledge(Guid userId, string displayName, DateTimeOffset acknowledgedAtUtc)
    {
        AcknowledgedByUserId = userId == Guid.Empty
            ? throw new ArgumentException("Acknowledging user is required.", nameof(userId))
            : userId;
        AcknowledgedByDisplayName = RequireNonEmpty(displayName, nameof(displayName), 120);
        AcknowledgedAtUtc = acknowledgedAtUtc.ToUniversalTime();
        Touch();
    }

    public void Resolve(Guid userId, string displayName, string reason, DateTimeOffset resolvedAtUtc)
    {
        ResolvedByUserId = userId == Guid.Empty
            ? throw new ArgumentException("Resolving user is required.", nameof(userId))
            : userId;
        ResolvedByDisplayName = RequireNonEmpty(displayName, nameof(displayName), 120);
        ResolutionReason = RequireNonEmpty(reason, nameof(reason), 280);
        ResolvedAtUtc = resolvedAtUtc.ToUniversalTime();
        SnoozedUntilUtc = null;
        SnoozeReason = null;
        Touch();
    }

    public void Snooze(DateTimeOffset snoozedUntilUtc, string reason)
    {
        if (snoozedUntilUtc <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(snoozedUntilUtc), "Snooze end must be in the future.");
        }

        SnoozedUntilUtc = snoozedUntilUtc.ToUniversalTime();
        SnoozeReason = RequireNonEmpty(reason, nameof(reason), 280);
        Touch();
    }

    public bool IsResolvedFor(DateTimeOffset detectedAtUtc) =>
        ResolvedAtUtc is not null && ResolvedAtUtc >= detectedAtUtc.ToUniversalTime();

    private void Touch() => RowVersion++;

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
