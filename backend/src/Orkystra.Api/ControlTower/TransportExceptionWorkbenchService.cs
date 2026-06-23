using Orkystra.Contracts.Connectors;
using Orkystra.Contracts.Transport;

namespace Orkystra.Api.ControlTower;

public sealed class TransportExceptionWorkbenchService
{
    private readonly TransportProjectionService _transportProjectionService;
    private readonly TransportSyncWorkflowService _transportSyncWorkflowService;
    private readonly TransportSyncHistoryService _transportSyncHistoryService;
    private readonly TransportExceptionResolutionLedgerService _resolutionLedgerService;

    public TransportExceptionWorkbenchService(
        TransportProjectionService transportProjectionService,
        TransportSyncWorkflowService transportSyncWorkflowService,
        TransportSyncHistoryService transportSyncHistoryService,
        TransportExceptionResolutionLedgerService resolutionLedgerService)
    {
        _transportProjectionService = transportProjectionService;
        _transportSyncWorkflowService = transportSyncWorkflowService;
        _transportSyncHistoryService = transportSyncHistoryService;
        _resolutionLedgerService = resolutionLedgerService;
    }

    public async ValueTask<TransportExceptionWorkbenchReadModel> BuildAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var routes = await _transportProjectionService.ListAsync(tenantId, cancellationToken);
        var syncStatus = await _transportSyncWorkflowService.GetLatestStatusAsync(tenantId, cancellationToken);
        var syncDiff = await _transportSyncHistoryService.BuildLatestDiffAsync(tenantId, cancellationToken);
        var recentHistory = await _transportSyncHistoryService.BuildRecentHistoryAsync(tenantId, 4, cancellationToken);
        var resolutionLedger = await _resolutionLedgerService.GetAsync(tenantId, cancellationToken);
        var resolutionsByExceptionId = resolutionLedger.Entries.ToDictionary(
            entry => entry.ExceptionId,
            entry => entry,
            StringComparer.OrdinalIgnoreCase);

        var items = new List<TransportExceptionWorkbenchItemReadModel>();
        var importedReferences = new HashSet<string>(
            syncStatus.ImportedRouteReferences,
            StringComparer.OrdinalIgnoreCase);
        var diffByReference = syncDiff.RouteDiffs.ToDictionary(
            diff => diff.RouteReference,
            diff => diff,
            StringComparer.OrdinalIgnoreCase);

        if (!syncStatus.HasPersistedSnapshot)
        {
            items.Add(BuildWorkbenchItem(
                resolutionsByExceptionId,
                "sync-baseline-missing",
                "Warning",
                "Sync",
                "No persisted transport baseline exists yet",
                "Import a transport snapshot before trusting route-by-route transport decisions for this tenant.",
                "sync-import",
                "Import snapshot",
                BuildEvidence(
                    $"Source posture: {syncStatus.Source}",
                    $"Sync status: {syncStatus.SyncStatus}",
                    "No persisted imported routes are available yet.")));
        }

        if (syncStatus.Health.Status != ProviderHealthStatus.Healthy
            || !string.Equals(syncStatus.Source, "live", StringComparison.OrdinalIgnoreCase))
        {
            var action = syncStatus.HasPersistedSnapshot ? "sync-refresh" : "sync-import";
            var actionLabel = syncStatus.HasPersistedSnapshot ? "Refresh sync" : "Import snapshot";
            items.Add(BuildWorkbenchItem(
                resolutionsByExceptionId,
                "sync-posture-attention",
                syncStatus.Health.Status == ProviderHealthStatus.Unhealthy ? "Critical" : "Warning",
                "Sync",
                "Transport sync posture needs attention",
                syncStatus.SyncDetail
                    ?? "The transport import posture is not fully healthy, so operator evidence may be drifting away from the source.",
                action,
                actionLabel,
                BuildEvidence(
                    $"Health: {syncStatus.Health.Status}",
                    $"Source: {syncStatus.Source}",
                    $"Imported routes: {syncStatus.ImportedRouteCount}")));
        }

        if (syncDiff.HasComparableHistory
            && (syncDiff.ChangedRouteCount > 0 || syncDiff.AddedRouteCount > 0 || syncDiff.RemovedRouteCount > 0))
        {
            items.Add(BuildWorkbenchItem(
                resolutionsByExceptionId,
                "latest-import-delta",
                syncDiff.ChangedRouteCount + syncDiff.RemovedRouteCount >= 3 ? "Critical" : "Warning",
                "Import Delta",
                "Latest import diverges from the previous transport baseline",
                syncDiff.Detail,
                "selected-diff",
                "Review latest diff",
                BuildEvidence(
                    $"{syncDiff.ChangedRouteCount} changed route(s)",
                    $"{syncDiff.AddedRouteCount} added route(s)",
                    $"{syncDiff.RemovedRouteCount} removed route(s)")));
        }

        var latestHistoryEntry = recentHistory.Entries.FirstOrDefault();
        if (latestHistoryEntry is not null
            && latestHistoryEntry.HasComparablePrevious
            && latestHistoryEntry.ChangedRouteCount + latestHistoryEntry.AddedRouteCount + latestHistoryEntry.RemovedRouteCount >= 4)
        {
            items.Add(BuildWorkbenchItem(
                resolutionsByExceptionId,
                $"import-volatility-{latestHistoryEntry.RunId}",
                "Warning",
                "History",
                "Recent imports are changing rapidly",
                latestHistoryEntry.Summary,
                "review-history",
                "Review import history",
                BuildEvidence(
                    $"{latestHistoryEntry.ChangedRouteCount} changed route(s)",
                    $"{latestHistoryEntry.AddedRouteCount} added route(s)",
                    $"{latestHistoryEntry.RemovedRouteCount} removed route(s)")));
        }

        foreach (var route in routes)
        {
            if (!string.Equals(route.Status, "Delayed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(route.Status, "At risk", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            diffByReference.TryGetValue(route.Reference, out var routeDiff);
            var isImported = importedReferences.Contains(route.Reference);
            var action = routeDiff is not null && routeDiff.ChangeType != "Unchanged"
                ? "focus-route-diff"
                : string.Equals(route.Status, "Delayed", StringComparison.OrdinalIgnoreCase)
                    ? "optimization-refresh"
                    : "focus-route";
            var actionLabel = action switch
            {
                "focus-route-diff" => "Review route diff",
                "optimization-refresh" => "Re-run optimization",
                _ => "Focus route"
            };

            items.Add(BuildWorkbenchItem(
                resolutionsByExceptionId,
                $"route-{route.RouteId:D}",
                string.Equals(route.Status, "Delayed", StringComparison.OrdinalIgnoreCase) ? "Critical" : "Warning",
                "Route",
                $"{route.Reference} is {route.Status.ToLowerInvariant()}",
                BuildRouteExceptionDetail(route, routeDiff, isImported),
                route.RouteId,
                route.Reference,
                action,
                actionLabel,
                BuildEvidence(
                    $"{route.StopCount} stop(s)",
                    $"{route.ShipmentCount} shipment(s)",
                    isImported
                        ? "Present in latest imported snapshot"
                        : "Missing from latest imported snapshot")));
        }

        var orderedItems = items
            .OrderByDescending(item => SeverityRank(item.Severity))
            .ThenBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.RouteReference ?? item.Title, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();

        var groups = orderedItems
            .GroupBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var representative = group
                    .OrderByDescending(item => SeverityRank(item.Severity))
                    .First();
                var count = group.Count();

                return new TransportExceptionWorkbenchGroupReadModel(
                    group.Key.ToLowerInvariant().Replace(' ', '-'),
                    group.Key,
                    representative.Severity,
                    count,
                    count == 1
                        ? $"1 {group.Key.ToLowerInvariant()} exception is active."
                        : $"{count} {group.Key.ToLowerInvariant()} exceptions are active.",
                    representative.RecommendedAction,
                    representative.ActionLabel);
            })
            .OrderByDescending(group => SeverityRank(group.HighestSeverity))
            .ThenByDescending(group => group.Count)
            .ThenBy(group => group.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var summary = orderedItems.Length == 0
            ? "Transport exception workbench is clear right now."
            : $"{orderedItems.Length} transport exception(s) need operator review.";

        return new TransportExceptionWorkbenchReadModel(
            generatedAtUtc,
            orderedItems.Length,
            summary,
            groups,
            orderedItems);
    }

    private static string BuildRouteExceptionDetail(
        RouteSummaryReadModel route,
        TransportSyncRouteDiffItemReadModel? routeDiff,
        bool isImported)
    {
        var details = new List<string>
        {
            $"{route.CompletedDeliveryCount} completed delivery stop(s) out of {route.StopCount}."
        };

        if (!isImported)
        {
            details.Add("The route is not present in the latest imported snapshot.");
        }

        if (routeDiff is not null && !string.Equals(routeDiff.ChangeType, "Unchanged", StringComparison.OrdinalIgnoreCase))
        {
            details.Add(routeDiff.Summary);
        }

        return string.Join(" ", details);
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "Critical" => 3,
        "Warning" => 2,
        _ => 1
    };

    private static IReadOnlyCollection<string> BuildEvidence(params string[] values) =>
        values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();

    private static TransportExceptionWorkbenchItemReadModel BuildWorkbenchItem(
        IReadOnlyDictionary<string, TransportExceptionResolutionEntryReadModel> resolutionsByExceptionId,
        string exceptionId,
        string severity,
        string category,
        string title,
        string detail,
        string recommendedAction,
        string actionLabel,
        IReadOnlyCollection<string> evidence) =>
        BuildWorkbenchItem(
            resolutionsByExceptionId,
            exceptionId,
            severity,
            category,
            title,
            detail,
            null,
            null,
            recommendedAction,
            actionLabel,
            evidence);

    private static TransportExceptionWorkbenchItemReadModel BuildWorkbenchItem(
        IReadOnlyDictionary<string, TransportExceptionResolutionEntryReadModel> resolutionsByExceptionId,
        string exceptionId,
        string severity,
        string category,
        string title,
        string detail,
        Guid? routeId,
        string? routeReference,
        string recommendedAction,
        string actionLabel,
        IReadOnlyCollection<string> evidence)
    {
        resolutionsByExceptionId.TryGetValue(exceptionId, out var resolution);

        return new TransportExceptionWorkbenchItemReadModel(
            exceptionId,
            severity,
            category,
            title,
            detail,
            routeId,
            routeReference,
            recommendedAction,
            actionLabel,
            resolution?.Status,
            resolution?.Note,
            resolution?.FollowUpOwner,
            resolution?.TargetReturnAtUtc,
            resolution?.UpdatedAtUtc,
            evidence);
    }
}
