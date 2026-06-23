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
                var followUpStatus = string.Equals(entry.FollowUpStatus, "Retired", StringComparison.OrdinalIgnoreCase)
                    ? "Retired"
                    : "Active";
                var hoursUntilTarget = entry.TargetReturnAtUtc is null
                    ? (int?)null
                    : (int)Math.Floor((entry.TargetReturnAtUtc.Value - generatedAtUtc).TotalHours);
                var slaPosture = string.Equals(followUpStatus, "Retired", StringComparison.OrdinalIgnoreCase)
                    ? "Retired"
                    : entry.TargetReturnAtUtc is null
                        ? "At Risk"
                        : entry.TargetReturnAtUtc.Value < generatedAtUtc
                            ? "Overdue"
                            : entry.TargetReturnAtUtc.Value <= generatedAtUtc.AddHours(24)
                                ? "At Risk"
                                : "Healthy";
                var isOwnerMissing = string.IsNullOrWhiteSpace(entry.FollowUpOwner);
                var isOverdue = entry.TargetReturnAtUtc is not null
                                && string.Equals(followUpStatus, "Active", StringComparison.OrdinalIgnoreCase)
                                && entry.TargetReturnAtUtc.Value < generatedAtUtc;
                var alertSeverity = isOverdue
                    ? "Critical"
                    : string.Equals(followUpStatus, "Retired", StringComparison.OrdinalIgnoreCase)
                        ? "Healthy"
                    : isOwnerMissing
                        ? "Warning"
                        : "Healthy";
                var alertSummary = isOverdue
                    ? "Target return window has passed."
                    : string.Equals(followUpStatus, "Retired", StringComparison.OrdinalIgnoreCase)
                        ? "Follow-up commitment has been retired."
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
                    followUpStatus,
                    entry.FollowUpOwner,
                    entry.TargetReturnAtUtc,
                    slaPosture,
                    hoursUntilTarget,
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
            .OrderByDescending(item => AlertRank(item.AlertSeverity))
            .ThenBy(item => item.FollowUpStatus == "Retired")
            .ThenByDescending(item => item.IsStillActive)
            .ThenBy(item => item.TargetReturnAtUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(item => item.FollowUpOwner ?? "zzzz")
            .ThenBy(item => item.UpdatedAtUtc)
            .ToArray();

        var activeDeferredCount = items.Count(item => item.FollowUpStatus == "Active");
        var retiredFollowUpCount = items.Count(item => item.FollowUpStatus == "Retired");
        var ownerlessCount = items.Count(item => item.IsOwnerMissing);
        var atRiskCount = items.Count(item => item.SlaPosture == "At Risk");
        var overdueCount = items.Count(item => item.IsOverdue);
        var dueWithin24HoursCount = items.Count(item =>
            item.FollowUpStatus == "Active"
            && item.TargetReturnAtUtc is not null
            && item.TargetReturnAtUtc.Value >= generatedAtUtc
            && item.TargetReturnAtUtc.Value <= generatedAtUtc.AddHours(24));
        var dueWithin72HoursCount = items.Count(item =>
            item.FollowUpStatus == "Active"
            && item.TargetReturnAtUtc is not null
            && item.TargetReturnAtUtc.Value >= generatedAtUtc
            && item.TargetReturnAtUtc.Value <= generatedAtUtc.AddHours(72));
        var healthyCommitmentCount = items.Count(item =>
            item.SlaPosture == "Healthy");
        var focusItem = items.FirstOrDefault(item => item.FollowUpStatus == "Active") ?? items.FirstOrDefault();
        var focusSummary = focusItem is null
            ? "No deferred follow-up needs focus right now."
            : string.Equals(focusItem.FollowUpStatus, "Retired", StringComparison.OrdinalIgnoreCase)
                ? $"Reopen {focusItem.Title} only if the deferred commitment should return to active follow-up."
            : focusItem.IsOverdue
                ? $"Escalate {focusItem.Title} first because its target return window has passed."
                : focusItem.IsOwnerMissing
                    ? $"Assign an owner for {focusItem.Title} before it drifts further."
                    : $"Review {focusItem.Title} next because it is the healthiest remaining deferred follow-up.";
        var alertSummary = items.Length == 0
            ? "No follow-up commitment alerts are active."
            : overdueCount > 0
                ? $"{overdueCount} deferred commitment(s) are overdue and need escalation."
                : ownerlessCount > 0
                    ? $"{ownerlessCount} deferred commitment(s) still need an owner."
                    : retiredFollowUpCount > 0
                        ? $"{retiredFollowUpCount} deferred commitment(s) have been retired from active follow-up."
                        : "Deferred commitments are currently assigned and inside their target return windows.";
        var summary = items.Length == 0
            ? "No deferred exception follow-up is waiting right now."
            : retiredFollowUpCount == 0
                ? $"{items.Length} deferred transport exception(s) still need a return pass."
                : $"{items.Length} deferred transport exception(s) are tracked here, including {retiredFollowUpCount} retired follow-up item(s).";
        var escalationDigest = new TransportExceptionFollowUpEscalationDigestReadModel(
            overdueCount,
            atRiskCount,
            ownerlessCount,
            dueWithin24HoursCount,
            dueWithin72HoursCount,
            retiredFollowUpCount,
            BuildEscalationSummary(
                overdueCount,
                atRiskCount,
                ownerlessCount,
                dueWithin24HoursCount,
                retiredFollowUpCount));
        var ownerSummaries = items
            .GroupBy(
                item => string.IsNullOrWhiteSpace(item.FollowUpOwner) ? "Unassigned" : item.FollowUpOwner!,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new TransportExceptionFollowUpOwnerSummaryReadModel(
                group.Key,
                string.Equals(group.Key, "Unassigned", StringComparison.OrdinalIgnoreCase),
                group.Count(),
                group.Count(item => item.FollowUpStatus == "Active"),
                group.Count(item => item.IsOverdue),
                group.Count(item => item.SlaPosture == "At Risk"),
                group.Count(item => item.FollowUpStatus == "Retired")))
            .OrderByDescending(summaryItem => summaryItem.OverdueCount)
            .ThenByDescending(summaryItem => summaryItem.AtRiskCount)
            .ThenByDescending(summaryItem => summaryItem.ActiveCount)
            .ThenBy(summaryItem => summaryItem.IsUnassigned ? string.Empty : summaryItem.Owner)
            .ToArray();
        var handoffItems = items
            .Where(item => item.FollowUpStatus == "Active")
            .Take(5)
            .Select(item =>
            {
                var readinessPosture = item.IsOwnerMissing
                    ? "Missing owner"
                    : string.IsNullOrWhiteSpace(item.Note)
                        ? "Missing note"
                        : string.IsNullOrWhiteSpace(item.RouteReference)
                            ? "Missing route"
                            : item.TargetReturnAtUtc is null
                                ? "Missing target"
                                : "Ready";
                var readinessSummary = readinessPosture switch
                {
                    "Missing owner" => "Assign an owner before this item changes hands.",
                    "Missing note" => "Capture the operator note before handoff so the next shift has enough context.",
                    "Missing route" => "Attach or confirm route context before handoff.",
                    "Missing target" => "Set a target return window before handoff.",
                    _ => "Owner, route context, note, and return window are ready for handoff."
                };
                var handoffSummary =
                    $"{item.Title} / {(item.RouteReference ?? "No route")} / {(item.FollowUpOwner ?? "Unassigned")} / {FormatHoursLabel(item.HoursUntilTarget)}";

                return new TransportExceptionFollowUpHandoffItemReadModel(
                    item.ExceptionId,
                    item.Title,
                    item.RouteReference,
                    item.FollowUpOwner,
                    item.TargetReturnAtUtc,
                    item.SlaPosture,
                    item.HoursUntilTarget,
                    item.Note,
                    handoffSummary,
                    readinessPosture,
                    readinessSummary,
                    item.RecommendedAction,
                    item.ActionLabel);
            })
            .ToArray();
        var handoffOwnerHeadline = ownerSummaries
            .Where(summaryItem => summaryItem.ActiveCount > 0)
            .OrderByDescending(summaryItem => summaryItem.OverdueCount)
            .ThenByDescending(summaryItem => summaryItem.AtRiskCount)
            .ThenByDescending(summaryItem => summaryItem.ActiveCount)
            .Select(summaryItem =>
                summaryItem.IsUnassigned
                    ? $"Unassigned follow-up lane is carrying {summaryItem.ActiveCount} active item(s)."
                    : $"{summaryItem.Owner} is carrying {summaryItem.ActiveCount} active follow-up item(s).")
            .FirstOrDefault()
            ?? "No active handoff owner lane is currently carrying deferred work.";
        var immediateCount = handoffItems.Count(item =>
            item.SlaPosture == "Overdue"
            || item.SlaPosture == "At Risk"
            || string.Equals(item.ReadinessPosture, "Missing owner", StringComparison.OrdinalIgnoreCase));
        var thisShiftCount = handoffItems.Count(item =>
            item.TargetReturnAtUtc is not null
            && item.TargetReturnAtUtc.Value <= generatedAtUtc.AddHours(12));
        var nextShiftCount = handoffItems.Count(item =>
            item.TargetReturnAtUtc is not null
            && item.TargetReturnAtUtc.Value > generatedAtUtc.AddHours(12)
            && item.TargetReturnAtUtc.Value <= generatedAtUtc.AddHours(24));
        var missingOwnerCount = handoffItems.Count(item => string.IsNullOrWhiteSpace(item.FollowUpOwner));
        var missingNoteCount = handoffItems.Count(item => string.IsNullOrWhiteSpace(item.Note));
        var missingRouteContextCount = handoffItems.Count(item => string.IsNullOrWhiteSpace(item.RouteReference));
        var briefingLines = handoffItems
            .Select(item =>
                $"{item.Title}: {(item.RouteReference ?? "No route")} / {(item.FollowUpOwner ?? "Unassigned")} / {item.SlaPosture} / {FormatHoursLabel(item.HoursUntilTarget)} / {(string.IsNullOrWhiteSpace(item.Note) ? "No note" : item.Note)}")
            .ToArray();
        var handoffPack = new TransportExceptionFollowUpHandoffPackReadModel(
            activeDeferredCount,
            immediateCount,
            thisShiftCount,
            nextShiftCount,
            missingOwnerCount,
            missingNoteCount,
            missingRouteContextCount,
            BuildHandoffSummary(activeDeferredCount, immediateCount, handoffItems.Length),
            BuildShiftSummary(thisShiftCount, nextShiftCount),
            handoffOwnerHeadline,
            briefingLines,
            handoffItems);

        return new TransportExceptionFollowUpQueueReadModel(
            generatedAtUtc,
            items.Length,
            activeDeferredCount,
            retiredFollowUpCount,
            ownerlessCount,
            atRiskCount,
            overdueCount,
            healthyCommitmentCount,
            focusItem?.ExceptionId,
            focusItem?.Title,
            focusSummary,
            alertSummary,
            summary,
            escalationDigest,
            handoffPack,
            ownerSummaries,
            items);
    }

    private static int AlertRank(string severity) => severity switch
    {
        "Critical" => 3,
        "Warning" => 2,
        _ => 1
    };

    private static string BuildEscalationSummary(
        int overdueCount,
        int atRiskCount,
        int ownerlessCount,
        int dueWithin24HoursCount,
        int retiredFollowUpCount)
    {
        if (overdueCount > 0)
        {
            return $"{overdueCount} follow-up commitment(s) are already overdue and need the next escalation pass first.";
        }

        if (atRiskCount > 0)
        {
            return $"{atRiskCount} follow-up commitment(s) are inside the at-risk window and should be reviewed before they turn overdue.";
        }

        if (ownerlessCount > 0)
        {
            return $"{ownerlessCount} follow-up commitment(s) still have no owner even though their return plan is active.";
        }

        if (dueWithin24HoursCount > 0)
        {
            return $"{dueWithin24HoursCount} follow-up commitment(s) come due inside the next 24 hours.";
        }

        if (retiredFollowUpCount > 0)
        {
            return $"{retiredFollowUpCount} follow-up commitment(s) are retired and available for audit if the issue reopens.";
        }

        return "Active follow-up commitments are currently assigned and inside their expected return windows.";
    }

    private static string BuildHandoffSummary(int activeDeferredCount, int immediateCount, int handoffItemCount)
    {
        if (activeDeferredCount == 0)
        {
            return "No active deferred follow-up needs to be passed into the next shift right now.";
        }

        if (immediateCount > 0)
        {
            return $"{immediateCount} active follow-up item(s) should be called out explicitly in the next shift handoff.";
        }

        return $"{handoffItemCount} active follow-up item(s) are packaged for the next shift handoff.";
    }

    private static string BuildShiftSummary(int thisShiftCount, int nextShiftCount)
    {
        if (thisShiftCount > 0)
        {
            return $"{thisShiftCount} item(s) come due inside this shift window.";
        }

        if (nextShiftCount > 0)
        {
            return $"{nextShiftCount} item(s) land in the next shift window after the current handoff.";
        }

        return "No active follow-up item is due inside the current or next shift window.";
    }

    private static string FormatHoursLabel(int? hoursUntilTarget)
    {
        if (hoursUntilTarget is null)
        {
            return "no target";
        }

        if (hoursUntilTarget.Value < 0)
        {
            var overdueHours = Math.Abs(hoursUntilTarget.Value);
            return overdueHours < 24
                ? $"{overdueHours}h overdue"
                : $"{(int)Math.Ceiling(overdueHours / 24d)}d overdue";
        }

        if (hoursUntilTarget.Value < 24)
        {
            return $"{hoursUntilTarget.Value}h left";
        }

        return $"{(int)Math.Ceiling(hoursUntilTarget.Value / 24d)}d left";
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
