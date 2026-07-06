using System.Text.Json;
using Orkystra.Api.Eventing;

namespace Orkystra.Api.Observability;

public sealed record SupportBundleSnapshot(
    DateTimeOffset GeneratedAtUtc,
    string TenantId,
    SupportBundleContextSnapshot Context,
    RequestMetricsSnapshot Metrics,
    PersistenceDiagnosticsSnapshot Persistence,
    EventBackboneTelemetrySnapshot EventBackbone,
    SupportBundleCollectionSnapshot Collections,
    SupportBundleSummarySnapshot Summary);

public sealed record SupportBundleContextSnapshot(
    string Environment,
    string User,
    string CorrelationId);

public sealed record SupportBundleCollectionSnapshot(
    int ProjectionCount,
    int WorkflowCount,
    int AuditCount,
    IReadOnlyCollection<SupportBundleProjectionSnapshot> Projections,
    IReadOnlyCollection<SupportBundleWorkflowSnapshot> Workflows,
    IReadOnlyCollection<SupportBundleAuditSnapshot> Audits);

public sealed record SupportBundleProjectionSnapshot(
    string ProjectionName,
    string ProjectionKey,
    string Source,
    DateTimeOffset CapturedAtUtc,
    JsonElement Payload);

public sealed record SupportBundleWorkflowSnapshot(
    long RunId,
    string WorkflowKind,
    string SubjectKey,
    string? ScenarioId,
    string Source,
    string Status,
    DateTimeOffset CreatedAtUtc,
    JsonElement Payload);

public sealed record SupportBundleAuditSnapshot(
    string Method,
    string Path,
    DateTimeOffset OccurredAtUtc,
    string TenantId,
    string Reason,
    string CorrelationId,
    int ResponseStatusCode);

public sealed record SupportBundleSummarySnapshot(
    string Posture,
    string Summary,
    string EscalationTarget,
    IReadOnlyCollection<string> Signals,
    IReadOnlyCollection<string> CollectionHints,
    IReadOnlyCollection<string> ArtifactChecklist);
