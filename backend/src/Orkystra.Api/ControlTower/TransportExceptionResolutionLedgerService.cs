using System.Text.Json;
using Orkystra.Api.Persistence;
using Orkystra.Contracts.Transport;

namespace Orkystra.Api.ControlTower;

public sealed class TransportExceptionResolutionLedgerService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly OperationalPersistenceStore _persistenceStore;
    private const string CurrentProjectionName = "transport-exception-resolutions";
    private const string CurrentProjectionKey = "active";
    private const string HistoryProjectionName = "transport-exception-resolution-history";
    private const string HistoryProjectionKey = "recent";
    private const int MaxHistoryEntries = 120;

    public TransportExceptionResolutionLedgerService(OperationalPersistenceStore persistenceStore)
    {
        _persistenceStore = persistenceStore;
    }

    public async ValueTask<TransportExceptionResolutionLedgerReadModel> GetAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _persistenceStore.ReadProjectionSnapshotAsync(
            tenantId,
            CurrentProjectionName,
            CurrentProjectionKey,
            cancellationToken);

        if (snapshot is null)
        {
            return new TransportExceptionResolutionLedgerReadModel(DateTimeOffset.UtcNow, 0, []);
        }

        return JsonSerializer.Deserialize<TransportExceptionResolutionLedgerReadModel>(
                   snapshot.PayloadJson,
                   SerializerOptions)
               ?? new TransportExceptionResolutionLedgerReadModel(DateTimeOffset.UtcNow, 0, []);
    }

    public async ValueTask<TransportExceptionResolutionHistoryReadModel> GetHistoryAsync(
        string tenantId,
        int count = 12,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _persistenceStore.ReadProjectionSnapshotAsync(
            tenantId,
            HistoryProjectionName,
            HistoryProjectionKey,
            cancellationToken);

        if (snapshot is null)
        {
            return new TransportExceptionResolutionHistoryReadModel(DateTimeOffset.UtcNow, 0, []);
        }

        var history = JsonSerializer.Deserialize<TransportExceptionResolutionHistoryReadModel>(
                          snapshot.PayloadJson,
                          SerializerOptions)
                      ?? new TransportExceptionResolutionHistoryReadModel(DateTimeOffset.UtcNow, 0, []);

        var normalizedCount = Math.Clamp(count, 1, 50);
        var entries = history.Entries
            .OrderByDescending(entry => entry.UpdatedAtUtc)
            .Take(normalizedCount)
            .ToArray();

        return new TransportExceptionResolutionHistoryReadModel(
            history.UpdatedAtUtc,
            entries.Length,
            entries);
    }

    public async ValueTask<TransportExceptionResolutionLedgerReadModel> SaveAsync(
        string tenantId,
        TransportExceptionResolutionWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExceptionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Status);

        var normalizedStatus = request.Status.Trim();
        if (!string.Equals(normalizedStatus, "Reviewed", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalizedStatus, "Resolved", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalizedStatus, "Deferred", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Status must be Reviewed, Resolved, or Deferred.", nameof(request));
        }

        var current = await GetAsync(tenantId, cancellationToken);
        var currentHistory = await GetFullHistoryAsync(tenantId, cancellationToken);
        var nextEntries = current.Entries
            .Where(entry => !string.Equals(entry.ExceptionId, request.ExceptionId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var updatedAtUtc = DateTimeOffset.UtcNow;

        nextEntries.Add(new TransportExceptionResolutionEntryReadModel(
            request.ExceptionId,
            normalizedStatus,
            string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            updatedAtUtc));

        var nextLedger = new TransportExceptionResolutionLedgerReadModel(
            updatedAtUtc,
            nextEntries.Count,
            nextEntries
                .OrderByDescending(entry => entry.UpdatedAtUtc)
                .ToArray());

        var nextHistoryEntries = currentHistory.Entries
            .Prepend(new TransportExceptionResolutionHistoryEntryReadModel(
                Guid.NewGuid().ToString("N"),
                request.ExceptionId,
                normalizedStatus,
                string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                updatedAtUtc))
            .OrderByDescending(entry => entry.UpdatedAtUtc)
            .Take(MaxHistoryEntries)
            .ToArray();

        var nextHistory = new TransportExceptionResolutionHistoryReadModel(
            updatedAtUtc,
            nextHistoryEntries.Length,
            nextHistoryEntries);

        await _persistenceStore.UpsertProjectionAsync(
            tenantId,
            CurrentProjectionName,
            CurrentProjectionKey,
            "api",
            nextLedger,
            cancellationToken);

        await _persistenceStore.UpsertProjectionAsync(
            tenantId,
            HistoryProjectionName,
            HistoryProjectionKey,
            "api",
            nextHistory,
            cancellationToken);

        return nextLedger;
    }

    private async ValueTask<TransportExceptionResolutionHistoryReadModel> GetFullHistoryAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        var snapshot = await _persistenceStore.ReadProjectionSnapshotAsync(
            tenantId,
            HistoryProjectionName,
            HistoryProjectionKey,
            cancellationToken);

        if (snapshot is null)
        {
            return new TransportExceptionResolutionHistoryReadModel(DateTimeOffset.UtcNow, 0, []);
        }

        return JsonSerializer.Deserialize<TransportExceptionResolutionHistoryReadModel>(
                   snapshot.PayloadJson,
                   SerializerOptions)
               ?? new TransportExceptionResolutionHistoryReadModel(DateTimeOffset.UtcNow, 0, []);
    }
}
