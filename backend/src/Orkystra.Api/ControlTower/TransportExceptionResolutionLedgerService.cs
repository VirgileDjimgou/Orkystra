using System.Text.Json;
using Orkystra.Api.Persistence;
using Orkystra.Contracts.Transport;

namespace Orkystra.Api.ControlTower;

public sealed class TransportExceptionResolutionLedgerService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IOperationalPersistenceStore _persistenceStore;
    private const string CurrentProjectionName = "transport-exception-resolutions";
    private const string CurrentProjectionKey = "active";
    private const string HistoryProjectionName = "transport-exception-resolution-history";
    private const string HistoryProjectionKey = "recent";
    private const int MaxHistoryEntries = 120;

    public TransportExceptionResolutionLedgerService(IOperationalPersistenceStore persistenceStore)
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
        var normalizedOwner = string.IsNullOrWhiteSpace(request.FollowUpOwner)
            ? null
            : request.FollowUpOwner.Trim();
        var normalizedTargetReturnAtUtc = request.TargetReturnAtUtc;
        var normalizedFollowUpStatus = string.Equals(normalizedStatus, "Deferred", StringComparison.OrdinalIgnoreCase)
            ? "Active"
            : null;
        var normalizedAcknowledgementStatus = string.Equals(normalizedStatus, "Deferred", StringComparison.OrdinalIgnoreCase)
            ? "Unacknowledged"
            : null;

        if (!string.Equals(normalizedStatus, "Deferred", StringComparison.OrdinalIgnoreCase))
        {
            normalizedOwner = null;
            normalizedTargetReturnAtUtc = null;
        }

        nextEntries.Add(new TransportExceptionResolutionEntryReadModel(
            request.ExceptionId,
            normalizedStatus,
            string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            normalizedFollowUpStatus,
            normalizedOwner,
            normalizedTargetReturnAtUtc,
            updatedAtUtc,
            normalizedAcknowledgementStatus,
            null,
            null));

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
                normalizedFollowUpStatus,
                normalizedOwner,
                normalizedTargetReturnAtUtc,
                updatedAtUtc,
                normalizedAcknowledgementStatus,
                null,
                null))
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

    public async ValueTask<TransportExceptionResolutionLedgerReadModel> TransitionFollowUpAsync(
        string tenantId,
        string exceptionId,
        TransportExceptionFollowUpTransitionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exceptionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Action);

        var normalizedAction = request.Action.Trim();
        if (!string.Equals(normalizedAction, "retire", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalizedAction, "reopen", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalizedAction, "acknowledge", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Action must be acknowledge, retire, or reopen.", nameof(request));
        }

        var current = await GetAsync(tenantId, cancellationToken);
        var currentEntry = current.Entries.FirstOrDefault(
            entry => string.Equals(entry.ExceptionId, exceptionId, StringComparison.OrdinalIgnoreCase));

        if (currentEntry is null || !string.Equals(currentEntry.Status, "Deferred", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only deferred follow-up items can be transitioned.");
        }

        var currentHistory = await GetFullHistoryAsync(tenantId, cancellationToken);
        var nextEntries = current.Entries
            .Where(entry => !string.Equals(entry.ExceptionId, exceptionId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var updatedAtUtc = DateTimeOffset.UtcNow;
        var isAcknowledge = string.Equals(normalizedAction, "acknowledge", StringComparison.OrdinalIgnoreCase);
        var nextFollowUpStatus = string.Equals(normalizedAction, "retire", StringComparison.OrdinalIgnoreCase)
            ? "Retired"
            : "Active";
        var nextNote = string.IsNullOrWhiteSpace(request.Note)
            ? currentEntry.Note
            : request.Note.Trim();
        var nextAcknowledgementStatus = isAcknowledge
            ? "Acknowledged"
            : currentEntry.AcknowledgementStatus;
        var nextAcknowledgedAtUtc = isAcknowledge
            ? updatedAtUtc
            : currentEntry.AcknowledgedAtUtc;
        var nextAcknowledgedBy = isAcknowledge
            ? string.IsNullOrWhiteSpace(request.AcknowledgedBy)
                ? currentEntry.FollowUpOwner ?? "Next shift"
                : request.AcknowledgedBy.Trim()
            : currentEntry.AcknowledgedBy;

        nextEntries.Add(new TransportExceptionResolutionEntryReadModel(
            currentEntry.ExceptionId,
            currentEntry.Status,
            nextNote,
            nextFollowUpStatus,
            currentEntry.FollowUpOwner,
            currentEntry.TargetReturnAtUtc,
            updatedAtUtc,
            nextAcknowledgementStatus,
            nextAcknowledgedAtUtc,
            nextAcknowledgedBy));

        var nextLedger = new TransportExceptionResolutionLedgerReadModel(
            updatedAtUtc,
            nextEntries.Count,
            nextEntries
                .OrderByDescending(entry => entry.UpdatedAtUtc)
                .ToArray());

        var nextHistoryEntries = currentHistory.Entries
            .Prepend(new TransportExceptionResolutionHistoryEntryReadModel(
                Guid.NewGuid().ToString("N"),
                currentEntry.ExceptionId,
                currentEntry.Status,
                nextNote,
                nextFollowUpStatus,
                currentEntry.FollowUpOwner,
                currentEntry.TargetReturnAtUtc,
                updatedAtUtc,
                nextAcknowledgementStatus,
                nextAcknowledgedAtUtc,
                nextAcknowledgedBy))
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
