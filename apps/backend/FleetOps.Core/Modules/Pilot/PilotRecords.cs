using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Pilot;

public enum PilotIncidentSeverity { P0, P1, P2 }
public enum PilotIncidentStatus { Open, Resolved }

public sealed class PilotEnrollment : TenantEntity
{
    private PilotEnrollment() { }
    public PilotEnrollment(Guid organizationId, bool analyticsConsent, DateTimeOffset recordedAtUtc)
    {
        OrganizationId = organizationId == Guid.Empty ? throw new ArgumentException("Organization is required.") : organizationId;
        AnalyticsConsent = analyticsConsent;
        RecordedAtUtc = recordedAtUtc.ToUniversalTime();
    }
    public bool AnalyticsConsent { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }
    public void SetConsent(bool analyticsConsent, DateTimeOffset recordedAtUtc) { AnalyticsConsent = analyticsConsent; RecordedAtUtc = recordedAtUtc.ToUniversalTime(); }
}

public sealed class PilotDailyMetric : TenantEntity
{
    private PilotDailyMetric() { }

    public PilotDailyMetric(
        Guid organizationId,
        DateOnly capturedOnUtc,
        int activationEvents,
        int activeDrivers,
        int returningDrivers,
        int processedSyncCommands,
        int completedMissions,
        int completeProofs,
        int openExceptions,
        DateTimeOffset refreshedAtUtc)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        OrganizationId = organizationId;
        CapturedOnUtc = capturedOnUtc;
        Refresh(
            activationEvents,
            activeDrivers,
            returningDrivers,
            processedSyncCommands,
            completedMissions,
            completeProofs,
            openExceptions,
            refreshedAtUtc);
    }

    public DateOnly CapturedOnUtc { get; private set; }
    public int ActivationEvents { get; private set; }
    public int ActiveDrivers { get; private set; }
    public int ReturningDrivers { get; private set; }
    public int ProcessedSyncCommands { get; private set; }
    public int CompletedMissions { get; private set; }
    public int CompleteProofs { get; private set; }
    public int OpenExceptions { get; private set; }
    public DateTimeOffset RefreshedAtUtc { get; private set; }

    public void Refresh(
        int activationEvents,
        int activeDrivers,
        int returningDrivers,
        int processedSyncCommands,
        int completedMissions,
        int completeProofs,
        int openExceptions,
        DateTimeOffset refreshedAtUtc)
    {
        ActivationEvents = RequireCounter(activationEvents, nameof(activationEvents));
        ActiveDrivers = RequireCounter(activeDrivers, nameof(activeDrivers));
        ReturningDrivers = RequireCounter(returningDrivers, nameof(returningDrivers));
        ProcessedSyncCommands = RequireCounter(processedSyncCommands, nameof(processedSyncCommands));
        CompletedMissions = RequireCounter(completedMissions, nameof(completedMissions));
        CompleteProofs = RequireCounter(completeProofs, nameof(completeProofs));
        OpenExceptions = RequireCounter(openExceptions, nameof(openExceptions));
        RefreshedAtUtc = refreshedAtUtc.ToUniversalTime();
    }

    private static int RequireCounter(int value, string parameterName) =>
        value < 0
            ? throw new ArgumentOutOfRangeException(parameterName, "Counters cannot be negative.")
            : value;
}

public sealed class PilotSupportIncident : TenantEntity
{
    private PilotSupportIncident() { }
    public PilotSupportIncident(Guid organizationId, PilotIncidentSeverity severity, string category, string summary, string? workaround, DateTimeOffset occurredAtUtc)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.");
        if (!Enum.IsDefined(severity)) throw new ArgumentException("A supported incident severity is required.");
        if (string.IsNullOrWhiteSpace(category) || category.Trim().Length > 48) throw new ArgumentException("A category of up to 48 characters is required.");
        if (string.IsNullOrWhiteSpace(summary) || summary.Trim().Length > 500) throw new ArgumentException("A summary of up to 500 characters is required.");
        OrganizationId = organizationId; Severity = severity; Category = category.Trim(); Summary = summary.Trim(); Workaround = Trim(workaround); OccurredAtUtc = occurredAtUtc.ToUniversalTime(); Status = PilotIncidentStatus.Open;
    }
    public PilotIncidentSeverity Severity { get; private set; }
    public PilotIncidentStatus Status { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string? Workaround { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }
    public void Resolve(string? workaround, DateTimeOffset resolvedAtUtc) { Workaround = Trim(workaround) ?? Workaround; Status = PilotIncidentStatus.Resolved; ResolvedAtUtc = resolvedAtUtc.ToUniversalTime(); }
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > 500 ? throw new ArgumentException("Workaround is too long.") : value.Trim();
}

public sealed class PilotDecision : TenantEntity
{
    private PilotDecision() { }
    public PilotDecision(Guid organizationId, string outcome, string primarySegment, string rationale, DateTimeOffset decidedAtUtc)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.");
        if (outcome is not ("GO" or "SIMPLIFY" or "PIVOT" or "STOP")) throw new ArgumentException("Outcome must be GO, SIMPLIFY, PIVOT or STOP.");
        if (string.IsNullOrWhiteSpace(primarySegment) || primarySegment.Trim().Length > 80) throw new ArgumentException("A primary segment is required.");
        if (string.IsNullOrWhiteSpace(rationale) || rationale.Trim().Length > 1000) throw new ArgumentException("A rationale is required.");
        OrganizationId = organizationId; Outcome = outcome; PrimarySegment = primarySegment.Trim(); Rationale = rationale.Trim(); DecidedAtUtc = decidedAtUtc.ToUniversalTime();
    }
    public string Outcome { get; private set; } = string.Empty;
    public string PrimarySegment { get; private set; } = string.Empty;
    public string Rationale { get; private set; } = string.Empty;
    public DateTimeOffset DecidedAtUtc { get; private set; }
}
