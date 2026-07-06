using System.Text.Json;
using Microsoft.Extensions.Options;
using Orkystra.Api.Eventing;
using Orkystra.Api.Persistence;

namespace Orkystra.Api.Observability;

public sealed class SupportBundleService
{
    private readonly RequestMetricsStore _metricsStore;
    private readonly EventBackboneTelemetryStore _eventBackboneTelemetryStore;
    private readonly PersistenceDiagnosticsService _persistenceDiagnosticsService;
    private readonly IOperationalPersistenceStore _persistenceStore;
    private readonly IAuditStore _auditStore;
    private readonly OperationalPersistenceOptions _persistenceOptions;
    private readonly ObservabilityOptions _observabilityOptions;
    private readonly IWebHostEnvironment _environment;

    public SupportBundleService(
        RequestMetricsStore metricsStore,
        EventBackboneTelemetryStore eventBackboneTelemetryStore,
        PersistenceDiagnosticsService persistenceDiagnosticsService,
        IOperationalPersistenceStore persistenceStore,
        IAuditStore auditStore,
        IOptions<OperationalPersistenceOptions> persistenceOptions,
        IOptions<ObservabilityOptions> observabilityOptions,
        IWebHostEnvironment environment)
    {
        _metricsStore = metricsStore;
        _eventBackboneTelemetryStore = eventBackboneTelemetryStore;
        _persistenceDiagnosticsService = persistenceDiagnosticsService;
        _persistenceStore = persistenceStore;
        _auditStore = auditStore;
        _persistenceOptions = persistenceOptions.Value;
        _observabilityOptions = observabilityOptions.Value;
        _environment = environment;
    }

    public async ValueTask<SupportBundleSnapshot> BuildAsync(
        string tenantId,
        string user,
        string correlationId,
        int? count = null,
        CancellationToken cancellationToken = default)
    {
        var boundedCount = Math.Clamp(count ?? 10, 1, Math.Min(_persistenceOptions.ReadLimit, _observabilityOptions.AuditReadLimit));
        var generatedAtUtc = DateTimeOffset.UtcNow;

        var metrics = _metricsStore.Snapshot();
        var eventBackbone = _eventBackboneTelemetryStore.Snapshot();
        var persistence = await _persistenceDiagnosticsService.BuildAsync(cancellationToken);
        var projections = await _persistenceStore.ReadProjectionSnapshotsAsync(tenantId, null, boundedCount, cancellationToken);
        var workflows = await _persistenceStore.ReadWorkflowRunsAsync(tenantId, null, boundedCount, cancellationToken);
        var audits = await _auditStore.ReadRecentAsync(boundedCount, cancellationToken);

        var signals = BuildSignals(persistence, eventBackbone, workflows.Count, audits.Count);
        var hints = BuildCollectionHints(persistence, eventBackbone, workflows.Count, audits.Count);

        return new SupportBundleSnapshot(
            generatedAtUtc,
            tenantId,
            new SupportBundleContextSnapshot(
                _environment.EnvironmentName,
                user,
                correlationId),
            metrics,
            persistence,
            eventBackbone,
            new SupportBundleCollectionSnapshot(
                projections.Count,
                workflows.Count,
                audits.Count,
                projections.Select(snapshot => new SupportBundleProjectionSnapshot(
                    snapshot.ProjectionName,
                    snapshot.ProjectionKey,
                    snapshot.Source,
                    snapshot.CapturedAtUtc,
                    JsonDocument.Parse(snapshot.PayloadJson).RootElement.Clone()))
                    .ToArray(),
                workflows.Select(run => new SupportBundleWorkflowSnapshot(
                    run.RunId,
                    run.WorkflowKind,
                    run.SubjectKey,
                    run.ScenarioId,
                    run.Source,
                    run.Status,
                    run.CreatedAtUtc,
                    JsonDocument.Parse(run.PayloadJson).RootElement.Clone()))
                    .ToArray(),
                audits.Select(entry => new SupportBundleAuditSnapshot(
                    entry.Method,
                    entry.Path,
                    entry.TimestampUtc,
                    entry.TenantId,
                    entry.Reason,
                    entry.CorrelationId,
                    entry.StatusCode))
                    .ToArray()),
            new SupportBundleSummarySnapshot(
                BuildPosture(persistence, eventBackbone),
                BuildSummary(tenantId, persistence, eventBackbone, projections.Count, workflows.Count, audits.Count),
                BuildEscalationTarget(persistence, eventBackbone, workflows.Count, audits.Count),
                signals,
                hints,
                BuildArtifactChecklist(persistence, eventBackbone, workflows.Count, audits.Count)));
    }

    private static string BuildPosture(
        PersistenceDiagnosticsSnapshot persistence,
        EventBackboneTelemetrySnapshot eventBackbone)
    {
        if (!persistence.Healthy)
        {
            return "Persistence attention needed";
        }

        if (eventBackbone.Enabled && !string.IsNullOrWhiteSpace(eventBackbone.LastError))
        {
            return "Event backbone attention needed";
        }

        if (eventBackbone.Enabled)
        {
            return "Brokered support snapshot";
        }

        return "Local support snapshot";
    }

    private static string BuildSummary(
        string tenantId,
        PersistenceDiagnosticsSnapshot persistence,
        EventBackboneTelemetrySnapshot eventBackbone,
        int projectionCount,
        int workflowCount,
        int auditCount)
    {
        var brokerState = eventBackbone.Enabled
            ? $"{eventBackbone.PublishedCount} published / {eventBackbone.ConsumedCount} consumed"
            : "event backbone disabled";

        return $"{tenantId} support bundle collected with {projectionCount} projections, {workflowCount} workflows, and {auditCount} audit entries; persistence is {persistence.Provider}/{persistence.Posture}, broker state is {brokerState}.";
    }

    private static string[] BuildSignals(
        PersistenceDiagnosticsSnapshot persistence,
        EventBackboneTelemetrySnapshot eventBackbone,
        int workflowCount,
        int auditCount)
    {
        var signals = new List<string>
        {
            persistence.Healthy ? "persistence-healthy" : "persistence-degraded",
            eventBackbone.Enabled ? "event-backbone-enabled" : "event-backbone-disabled",
            workflowCount > 0 ? "workflow-evidence-present" : "workflow-evidence-thin",
            auditCount > 0 ? "audit-evidence-present" : "audit-evidence-thin"
        };

        if (!string.IsNullOrWhiteSpace(eventBackbone.LastError))
        {
            signals.Add("event-backbone-last-error-present");
        }

        return signals.ToArray();
    }

    private static string[] BuildCollectionHints(
        PersistenceDiagnosticsSnapshot persistence,
        EventBackboneTelemetrySnapshot eventBackbone,
        int workflowCount,
        int auditCount)
    {
        var hints = new List<string>();

        if (!persistence.Healthy)
        {
            hints.Add("Verify /observability/persistence/provider before troubleshooting higher-level workflows.");
        }

        if (eventBackbone.Enabled && eventBackbone.PublishedCount == 0 && eventBackbone.ConsumedCount == 0)
        {
            hints.Add("Publish demo scenario or GPS telemetry to collect broker-backed evidence.");
        }

        if (workflowCount == 0)
        {
            hints.Add("Run at least one bounded workflow such as transport sync, AI recommendation, or route optimization before exporting.");
        }

        if (auditCount == 0)
        {
            hints.Add("Exercise at least one protected API route so the bundle includes audit evidence.");
        }

        if (hints.Count == 0)
        {
            hints.Add("Bundle already contains the core evidence needed for first-line self-host debugging.");
        }

        return hints.ToArray();
    }

    private static string BuildEscalationTarget(
        PersistenceDiagnosticsSnapshot persistence,
        EventBackboneTelemetrySnapshot eventBackbone,
        int workflowCount,
        int auditCount)
    {
        if (!persistence.Healthy)
        {
            return "configuration-or-persistence";
        }

        if (eventBackbone.Enabled && !string.IsNullOrWhiteSpace(eventBackbone.LastError))
        {
            return "dependency-or-event-backbone";
        }

        if (workflowCount == 0 || auditCount == 0)
        {
            return "evidence-thin";
        }

        return "product-or-workflow";
    }

    private static string[] BuildArtifactChecklist(
        PersistenceDiagnosticsSnapshot persistence,
        EventBackboneTelemetrySnapshot eventBackbone,
        int workflowCount,
        int auditCount)
    {
        var checklist = new List<string>
        {
            "Attach the exported support bundle JSON.",
            "Include one focused screenshot or UI capture of the first failing state.",
            "Record the exact reproduction steps used in the operator workflow."
        };

        if (!persistence.Healthy)
        {
            checklist.Add("Include the persistence diagnostics response or provider posture from /observability/persistence/provider.");
        }

        if (eventBackbone.Enabled)
        {
            checklist.Add("Capture broker-backed workflow evidence such as sync, telemetry publish, or optimization activity.");
        }

        if (workflowCount == 0)
        {
            checklist.Add("Run one bounded workflow before escalating so the issue packet includes workflow evidence.");
        }

        if (auditCount == 0)
        {
            checklist.Add("Exercise at least one protected API call so the issue packet includes audit evidence.");
        }

        return checklist.ToArray();
    }
}
