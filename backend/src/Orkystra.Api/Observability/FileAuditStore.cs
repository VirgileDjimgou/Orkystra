using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Orkystra.Api.Observability;

public sealed class FileAuditStore : IAuditStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly string _auditFilePath;

    public FileAuditStore(IOptions<ObservabilityOptions> options)
    {
        var configuredPath = options.Value.AuditLogFilePath;
        _auditFilePath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine("output", "audit", "audit-log.jsonl")
            : configuredPath);
    }

    public async ValueTask AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_auditFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var payload = JsonSerializer.Serialize(entry, SerializerOptions) + Environment.NewLine;
        await File.AppendAllTextAsync(_auditFilePath, payload, Encoding.UTF8, cancellationToken);
    }

    public async ValueTask<IReadOnlyCollection<AuditEntry>> ReadRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0 || !File.Exists(_auditFilePath))
        {
            return [];
        }

        var lines = await File.ReadAllLinesAsync(_auditFilePath, cancellationToken);
        var entries = new List<AuditEntry>(Math.Min(count, lines.Length));

        foreach (var line in lines.Reverse())
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize<AuditEntry>(line, SerializerOptions);
            if (entry is not null)
            {
                entries.Add(entry);
            }

            if (entries.Count == count)
            {
                break;
            }
        }

        entries.Reverse();
        return entries;
    }
}
