namespace Orkystra.Api.Persistence;

public interface IOperationalPersistenceStore
{
    Task UpsertProjectionAsync<TPayload>(
        string tenantId,
        string projectionName,
        string projectionKey,
        string source,
        TPayload payload,
        CancellationToken cancellationToken = default);

    Task AppendWorkflowRunAsync<TPayload>(
        string tenantId,
        string workflowKind,
        string subjectKey,
        string? scenarioId,
        string source,
        string status,
        TPayload payload,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PersistedProjectionSnapshot>> ReadProjectionSnapshotsAsync(
        string tenantId,
        string? projectionName,
        int count,
        CancellationToken cancellationToken = default);

    Task<PersistedProjectionSnapshot?> ReadProjectionSnapshotAsync(
        string tenantId,
        string projectionName,
        string projectionKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PersistedWorkflowRun>> ReadWorkflowRunsAsync(
        string tenantId,
        string? workflowKind,
        int count,
        CancellationToken cancellationToken = default);
}
