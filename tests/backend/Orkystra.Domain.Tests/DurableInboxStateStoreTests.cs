using Microsoft.Extensions.Options;
using Orkystra.Api.Eventing;
using Orkystra.Api.Persistence;

namespace Orkystra.Domain.Tests;

public sealed class DurableInboxStateStoreTests : IDisposable
{
    private readonly TemporaryDirectory _tempDir = new("orkystra-inbox-tests");

    public void Dispose()
    {
        _tempDir.Dispose();
    }

    private DurableInboxStateStore CreateStore()
    {
        return new DurableInboxStateStore(
            Options.Create(new OperationalPersistenceOptions
            {
                DatabasePath = Path.Combine("data", "inbox.db")
            }),
            _tempDir.FullName);
    }

    [Fact]
    public async Task HasProcessedAsync_returns_false_for_unprocessed_message()
    {
        using var store = CreateStore();
        var processed = await store.HasProcessedAsync("test-consumer", Guid.NewGuid());
        Assert.False(processed);
    }

    [Fact]
    public async Task MarkProcessedAsync_then_HasProcessedAsync_returns_true()
    {
        using var store = CreateStore();
        var messageId = Guid.NewGuid();
        await store.MarkProcessedAsync("test-consumer", messageId, DateTimeOffset.UtcNow);

        var processed = await store.HasProcessedAsync("test-consumer", messageId);
        Assert.True(processed);
    }

    [Fact]
    public async Task HasProcessedAsync_is_consumer_scoped()
    {
        using var store = CreateStore();
        var messageId = Guid.NewGuid();
        await store.MarkProcessedAsync("consumer-a", messageId, DateTimeOffset.UtcNow);

        Assert.True(await store.HasProcessedAsync("consumer-a", messageId));
        Assert.False(await store.HasProcessedAsync("consumer-b", messageId));
    }

    [Fact]
    public async Task MarkProcessedAsync_is_idempotent()
    {
        using var store = CreateStore();
        var messageId = Guid.NewGuid();
        await store.MarkProcessedAsync("consumer-a", messageId, DateTimeOffset.UtcNow);
        await store.MarkProcessedAsync("consumer-a", messageId, DateTimeOffset.UtcNow);

        Assert.True(await store.HasProcessedAsync("consumer-a", messageId));
    }

    [Fact]
    public async Task data_survives_store_recreation()
    {
        var messageId = Guid.NewGuid();

        using (var store1 = CreateStore())
        {
            await store1.MarkProcessedAsync("consumer-a", messageId, DateTimeOffset.UtcNow);
        }

        using (var store2 = CreateStore())
        {
            Assert.True(await store2.HasProcessedAsync("consumer-a", messageId));
        }
    }
}

public sealed class TemporaryDirectory : IDisposable
{
    private readonly DirectoryInfo _directory;

    public TemporaryDirectory(string prefix)
    {
        _directory = Directory.CreateTempSubdirectory(prefix);
    }

    public string FullName => _directory.FullName;

    public void Dispose()
    {
        try
        {
            _directory.Delete(true);
        }
        catch
        {
        }
    }
}
