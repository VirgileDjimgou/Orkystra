using Orkystra.Contracts.Eventing;

namespace Orkystra.Application.Eventing;

public sealed class IdempotentProjectionRunner
{
    private readonly IReadOnlyCollection<IEventProjection> _projections;
    private readonly IInboxStateStore _inboxStateStore;

    public IdempotentProjectionRunner(IEnumerable<IEventProjection> projections, IInboxStateStore inboxStateStore)
    {
        _projections = projections?.ToArray() ?? throw new ArgumentNullException(nameof(projections));
        _inboxStateStore = inboxStateStore ?? throw new ArgumentNullException(nameof(inboxStateStore));
    }

    public async ValueTask<ProjectionDispatchResult> DispatchAsync(
        IEventEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var applied = new List<string>();
        var skipped = new List<string>();

        foreach (var projection in _projections)
        {
            if (!projection.CanProject(envelope))
            {
                continue;
            }

            if (await _inboxStateStore.HasProcessedAsync(projection.ProjectionName, envelope.MessageId, cancellationToken))
            {
                skipped.Add(projection.ProjectionName);
                continue;
            }

            await projection.ProjectAsync(envelope, cancellationToken);
            await _inboxStateStore.MarkProcessedAsync(projection.ProjectionName, envelope.MessageId, DateTimeOffset.UtcNow, cancellationToken);
            applied.Add(projection.ProjectionName);
        }

        return new ProjectionDispatchResult(applied, skipped);
    }
}
