using FleetOps.Core.Modules.Pilot;

namespace FleetOps.Api.Pilot;

public sealed record UpdatePilotConsentRequest(bool AnalyticsConsent);
public sealed record CreatePilotIncidentRequest(PilotIncidentSeverity Severity, string Category, string Summary, string? Workaround);
public sealed record ResolvePilotIncidentRequest(string? Workaround);
public sealed record RecordPilotDecisionRequest(string Outcome, string PrimarySegment, string Rationale);
public sealed record PilotMetricsResponse(bool AnalyticsConsent, PilotDailyMetricResponse? LatestDailyMetric, int OpenIncidents, DateTimeOffset GeneratedAtUtc);
public sealed record PilotDailyMetricResponse(DateOnly CapturedOnUtc, int ActivationEvents, int ActiveDrivers, int ReturningDrivers, int ProcessedSyncCommands, int CompletedMissions, int CompleteProofs, int OpenExceptions, DateTimeOffset RefreshedAtUtc);
public sealed record PilotIncidentResponse(Guid Id, PilotIncidentSeverity Severity, PilotIncidentStatus Status, string Category, string Summary, string? Workaround, DateTimeOffset OccurredAtUtc, DateTimeOffset? ResolvedAtUtc);
public sealed record PilotDecisionResponse(Guid Id, string Outcome, string PrimarySegment, string Rationale, DateTimeOffset DecidedAtUtc);
public sealed record PilotEvidenceExportResponse(bool AnalyticsConsent, DateTimeOffset? ConsentRecordedAtUtc, IReadOnlyList<PilotDailyMetricResponse> DailyMetrics, IReadOnlyList<PilotIncidentResponse> Incidents, IReadOnlyList<PilotDecisionResponse> Decisions, DateTimeOffset ExportedAtUtc);
