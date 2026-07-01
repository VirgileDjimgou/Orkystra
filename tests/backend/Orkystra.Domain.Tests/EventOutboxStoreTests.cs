using Microsoft.Extensions.Options;
using Orkystra.Api.Eventing;
using Orkystra.Api.Persistence;

namespace Orkystra.Domain.Tests;

public sealed class EventOutboxStoreTests : IDisposable
{
    private readonly TemporaryDirectory _tempDir = new("orkystra-outbox-tests");

    public void Dispose()
    {
        _tempDir.Dispose();
    }

    private EventOutboxStore CreateStore()
    {
        return new EventOutboxStore(
            Options.Create(new OperationalPersistenceOptions
            {
                DatabasePath = Path.Combine("data", "outbox.db")
            }),
            _tempDir.FullName);
    }

    [Fact]
    public async Task RecordPendingAsync_creates_pending_entry()
    {
        using var store = CreateStore();
        var entryId = await store.RecordPendingAsync(
            Guid.NewGuid().ToString("D"),
            "TestEvent",
            "test/topic",
            new { value = 42 });

        Assert.True(entryId > 0);
    }

    [Fact]
    public async Task GetPendingEntriesAsync_returns_unpublished_entries()
    {
        using var store = CreateStore();
        var messageId = Guid.NewGuid().ToString("D");
        await store.RecordPendingAsync(messageId, "TestEvent", "test/topic", new { value = 42 });

        var pending = await store.GetPendingEntriesAsync(10);
        var entry = Assert.Single(pending);
        Assert.Equal("pending", entry.Status);
        Assert.Equal(messageId, entry.MessageId);
        Assert.Equal("TestEvent", entry.EventType);
        Assert.Equal("test/topic", entry.Topic);
    }

    [Fact]
    public async Task MarkPublishedAsync_updates_entry_status()
    {
        using var store = CreateStore();
        var entryId = await store.RecordPendingAsync(
            Guid.NewGuid().ToString("D"), "TestEvent", "test/topic", new { value = 42 });

        await store.MarkPublishedAsync(entryId);

        var pending = await store.GetPendingEntriesAsync(10);
        Assert.Empty(pending);
    }

    [Fact]
    public async Task MarkFailedAsync_records_error_message()
    {
        using var store = CreateStore();
        var entryId = await store.RecordPendingAsync(
            Guid.NewGuid().ToString("D"), "TestEvent", "test/topic", new { value = 42 });

        await store.MarkFailedAsync(entryId, "Connection refused");

        var pending = await store.GetPendingEntriesAsync(10);
        var entry = Assert.Single(pending);
        Assert.Equal("failed", entry.Status);
        Assert.Equal("Connection refused", entry.ErrorMessage);
    }

    [Fact]
    public async Task GetRecentEntriesAsync_returns_entries_in_reverse_order()
    {
        using var store = CreateStore();
        await store.RecordPendingAsync("msg-1", "Event1", "test/topic", new { seq = 1 });
        await store.RecordPendingAsync("msg-2", "Event2", "test/topic", new { seq = 2 });

        var recent = await store.GetRecentEntriesAsync(10);

        Assert.Equal(2, recent.Count);
        Assert.Equal("msg-2", recent.First().MessageId);
        Assert.Equal("msg-1", recent.Last().MessageId);
    }
}
