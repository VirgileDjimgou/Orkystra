namespace Orkystra.Application.Eventing;

public sealed class InMemoryInboxStateStore : IInboxStateStore
{
    private readonly HashSet<string> _processedMessages = [];
    private readonly Lock _lock = new();

    public ValueTask<bool> HasProcessedAsync(string consumerName, Guid messageId, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(consumerName, messageId);

        lock (_lock)
        {
            return ValueTask.FromResult(_processedMessages.Contains(key));
        }
    }

    public ValueTask MarkProcessedAsync(string consumerName, Guid messageId, DateTimeOffset processedAt, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(consumerName, messageId);

        lock (_lock)
        {
            _processedMessages.Add(key);
        }

        return ValueTask.CompletedTask;
    }

    private static string BuildKey(string consumerName, Guid messageId) => $"{consumerName}:{messageId:D}";
}
