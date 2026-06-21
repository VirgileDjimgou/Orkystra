namespace Orkystra.Api.Persistence;

public sealed record PersistedWorkflowRun(
    long RunId,
    string TenantId,
    string WorkflowKind,
    string SubjectKey,
    string? ScenarioId,
    string Source,
    string Status,
    DateTimeOffset CreatedAtUtc,
    string PayloadJson);
