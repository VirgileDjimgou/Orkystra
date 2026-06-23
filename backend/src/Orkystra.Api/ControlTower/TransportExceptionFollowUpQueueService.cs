using Orkystra.Contracts.Transport;

namespace Orkystra.Api.ControlTower;

public sealed class TransportExceptionFollowUpQueueService
{
    private readonly TransportExceptionWorkbenchService _workbenchService;
    private readonly TransportExceptionResolutionLedgerService _resolutionLedgerService;

    public TransportExceptionFollowUpQueueService(
        TransportExceptionWorkbenchService workbenchService,
        TransportExceptionResolutionLedgerService resolutionLedgerService)
    {
        _workbenchService = workbenchService;
        _resolutionLedgerService = resolutionLedgerService;
    }

    public async ValueTask<TransportExceptionFollowUpQueueReadModel> BuildAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var workbench = await _workbenchService.BuildAsync(tenantId, cancellationToken);
        var ledger = await _resolutionLedgerService.GetAsync(tenantId, cancellationToken);
        var history = await _resolutionLedgerService.GetHistoryAsync(tenantId, 50, cancellationToken);

        var workbenchByExceptionId = workbench.Items.ToDictionary(
            item => item.ExceptionId,
            item => item,
            StringComparer.OrdinalIgnoreCase);

        var historyByExceptionId = history.Entries
            .GroupBy(entry => entry.ExceptionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(entry => entry.UpdatedAtUtc).ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var items = ledger.Entries
            .Where(entry => string.Equals(entry.Status, "Deferred", StringComparison.OrdinalIgnoreCase))
            .Select(entry =>
            {
                workbenchByExceptionId.TryGetValue(entry.ExceptionId, out var workbenchItem);
                historyByExceptionId.TryGetValue(entry.ExceptionId, out var updateTrail);
                updateTrail ??= [];

                var previousStatus = updateTrail
                    .Skip(1)
                    .Select(historyEntry => historyEntry.Status)
                    .FirstOrDefault();
                var isOwnerMissing = string.IsNullOrWhiteSpace(entry.FollowUpOwner);
                var isOverdue = entry.TargetReturnAtUtc is not null
                                && entry.TargetReturnAtUtc.Value < generatedAtUtc;
                var alertSeverity = isOverdue
                    ? "Critical"
                    : isOwnerMissing
                        ? "Warning"
                        : "Healthy";
                var alertSummary = isOverdue
                    ? "Target return window has passed."
                    : isOwnerMissing
                        ? "Follow-up owner is still missing."
                        : "Commitment is assigned and within its return window.";

                return new TransportExceptionFollowUpQueueItemReadModel(
                    entry.ExceptionId,
                    workbenchItem?.Title ?? entry.ExceptionId,
                    workbenchItem?.Category ?? "Follow-up",
                    workbenchItem?.Detail
                    ?? "Deferred exception is waiting for an operator follow-up pass.",
                    workbenchItem?.RouteId,
                    workbenchItem?.RouteReference,
                    "Deferred",
                    entry.Note,
                    entry.FollowUpOwner,
                    entry.TargetReturnAtUtc,
                    entry.UpdatedAtUtc,
                    workbenchItem is not null,
                    isOwnerMissing,
                    isOverdue,
                    alertSeverity,
                    alertSummary,
                    updateTrail.Length,
                    previousStatus,
                    workbenchItem?.RecommendedAction ?? "review-history",
                    workbenchItem?.ActionLabel ?? "Review history",
                    BuildEvidence(workbenchItem, entry, updateTrail.Length, previousStatus));
            })
            .OrderByDescending(item => item.IsStillActive)
            .ThenByDescending(item => item.UpdatedAtUtc)
            .ToArray();

        var activeDeferredCount = items.Count(item => item.IsStillActive);
        var watchlistCount = items.Length - activeDeferredCount;
        var ownerlessCount = items.Count(item => item.IsOwnerMissing);
        var overdueCount = items.Count(item => item.IsOverdue);
        var healthyCommitmentCount = items.Count(item =>
            !item.IsOwnerMissing && !item.IsOverdue);
        var alertSummary = items.Length == 0
            ? "No follow-up commitment alerts are active."
            : overdueCount > 0
                ? $"{overdueCount} deferred commitment(s) are overdue and need escalation."
                : ownerlessCount > 0
                    ? $"{ownerlessCount} deferred commitment(s) still need an owner."
                    : "Deferred commitments are currently assigned and inside their target return windows.";
        var summary = items.Length == 0
            ? "No deferred exception follow-up is waiting right now."
            : watchlistCount == 0
                ? $"{items.Length} deferred transport exception(s) still need a return pass."
                : $"{items.Length} deferred transport exception(s) remain in follow-up, including {watchlistCount} watchlist item(s) that are not currently active.";

        return new TransportExceptionFollowUpQueueReadModel(
            generatedAtUtc,
            items.Length,
            activeDeferredCount,
            watchlistCount,
            ownerlessCount,
            overdueCount,
            healthyCommitmentCount,
            alertSummary,
            summary,
            items);
    }

    private static IReadOnlyCollection<string> BuildEvidence(
        TransportExceptionWorkbenchItemReadModel? workbenchItem,
        TransportExceptionResolutionEntryReadModel ledgerEntry,
        int updateCount,
        string? previousStatus)
    {
        var evidence = new List<string>();

        if (workbenchItem is not null)
        {
            evidence.AddRange(workbenchItem.Evidence);
        }
        else
        {
            evidence.Add("The deferred exception is not currently present in the active workbench.");
        }

        if (!string.IsNullOrWhiteSpace(ledgerEntry.Note))
        {
            evidence.Add($"Latest note: {ledgerEntry.Note}");
        }

        if (!string.IsNullOrWhiteSpace(ledgerEntry.FollowUpOwner))
        {
            evidence.Add($"Owner: {ledgerEntry.FollowUpOwner}");
        }

        if (ledgerEntry.TargetReturnAtUtc is not null)
        {
            evidence.Add($"Target return: {ledgerEntry.TargetReturnAtUtc:yyyy-MM-dd HH:mm} UTC");
        }

        if (string.IsNullOrWhiteSpace(ledgerEntry.FollowUpOwner))
        {
            evidence.Add("Owner is still missing.");
        }

        if (ledgerEntry.TargetReturnAtUtc is not null
            && ledgerEntry.TargetReturnAtUtc.Value < DateTimeOffset.UtcNow)
        {
            evidence.Add("Target return window is overdue.");
        }

        evidence.Add(updateCount == 1
            ? "1 saved resolution update in history."
            : $"{updateCount} saved resolution updates in history.");

        if (!string.IsNullOrWhiteSpace(previousStatus))
        {
            evidence.Add($"Previous saved status: {previousStatus}");
        }

        return evidence.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
