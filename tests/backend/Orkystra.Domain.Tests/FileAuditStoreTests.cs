using Microsoft.Extensions.Options;
using Orkystra.Api.Observability;

namespace Orkystra.Domain.Tests;

public sealed class FileAuditStoreTests
{
    [Fact]
    public async Task FileAuditStore_appends_entries_and_reads_recent_subset_in_order()
    {
        var auditFilePath = Path.Combine(Path.GetTempPath(), $"orkystra-audit-{Guid.NewGuid():N}.jsonl");
        var store = new FileAuditStore(Options.Create(new ObservabilityOptions
        {
            AuditLogFilePath = auditFilePath
        }));

        try
        {
            await store.AppendAsync(new AuditEntry("user-a", "GET", "/one", DateTimeOffset.Parse("2026-06-21T08:00:00Z"), "tenant-a", "", "127.0.0.1", "corr-1", 200));
            await store.AppendAsync(new AuditEntry("user-b", "POST", "/two", DateTimeOffset.Parse("2026-06-21T08:01:00Z"), "tenant-a", "demo", "127.0.0.1", "corr-2", 201));
            await store.AppendAsync(new AuditEntry("user-c", "GET", "/three", DateTimeOffset.Parse("2026-06-21T08:02:00Z"), "tenant-b", "", "127.0.0.1", "corr-3", 503));

            var recent = await store.ReadRecentAsync(2);

            Assert.Equal(2, recent.Count);
            Assert.Equal("/two", recent.ElementAt(0).Path);
            Assert.Equal("/three", recent.ElementAt(1).Path);
        }
        finally
        {
            if (File.Exists(auditFilePath))
            {
                File.Delete(auditFilePath);
            }
        }
    }
}
