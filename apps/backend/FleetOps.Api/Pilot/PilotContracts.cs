using FleetOps.Core.Modules.Pilot;

namespace FleetOps.Api.Pilot;

public sealed record UpdatePilotConsentRequest(bool AnalyticsConsent);
public sealed record CreatePilotIncidentRequest(PilotIncidentSeverity Severity, string Category, string Summary, string? Workaround);
public sealed record ResolvePilotIncidentRequest(string? Workaround);
public sealed record RecordPilotDecisionRequest(string Outcome, string PrimarySegment, string Rationale);
public sealed record PilotMetricsResponse(bool AnalyticsConsent, int ActiveDrivers, int CompletedMissions, int CompleteProofs, int OpenExceptions, int OpenIncidents, DateTimeOffset GeneratedAtUtc);
public sealed record PilotIncidentResponse(Guid Id, PilotIncidentSeverity Severity, PilotIncidentStatus Status, string Category, string Summary, string? Workaround, DateTimeOffset OccurredAtUtc, DateTimeOffset? ResolvedAtUtc);
public sealed record PilotDecisionResponse(Guid Id, string Outcome, string PrimarySegment, string Rationale, DateTimeOffset DecidedAtUtc);
