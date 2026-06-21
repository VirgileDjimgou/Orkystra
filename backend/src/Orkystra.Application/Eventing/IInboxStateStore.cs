namespace Orkystra.Application.Eventing;

public interface IInboxStateStore
{
    ValueTask<bool> HasProcessedAsync(string consumerName, Guid messageId, CancellationToken cancellationToken = default);

    ValueTask MarkProcessedAsync(string consumerName, Guid messageId, DateTimeOffset processedAt, CancellationToken cancellationToken = default);
}
