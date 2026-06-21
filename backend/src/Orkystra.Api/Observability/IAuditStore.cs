namespace Orkystra.Api.Observability;

public interface IAuditStore
{
    ValueTask AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyCollection<AuditEntry>> ReadRecentAsync(int count, CancellationToken cancellationToken = default);
}
