using Microsoft.Extensions.Options;
using Orkystra.Api.ControlTower;
using Orkystra.Api.Persistence;
using Orkystra.Application.Connectors;
using Orkystra.Contracts.Connectors;
using Orkystra.Contracts.Transport;

namespace Orkystra.Domain.Tests;

public sealed class TransportExceptionWorkbenchServiceTests
{
    [Fact]
    public async Task BuildAsync_returns_prioritized_transport_exceptions()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("orkystra-transport-exceptions-tests");

        try
        {
            var store = new OperationalPersistenceStore(
                Options.Create(new OperationalPersistenceOptions
                {
                    DatabasePath = Path.Combine("data", "operations.db")
                }),
                tempDirectory.FullName);

            var delayedRouteId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var atRiskRouteId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var provider = new StubTransportProvider(
                [
                    BuildRouteDetail(delayedRouteId, "RT-100", "Delayed", 4, 6, 1),
                    BuildRouteDetail(atRiskRouteId, "RT-200", "At risk", 3, 4, 1),
                ],
                new ProviderSyncStatus(
                    "rest-transport-adapter",
                    "live",
                    DateTimeOffset.Parse("2026-06-22T09:00:00Z"),
                    DateTimeOffset.Parse("2026-06-22T09:05:00Z"),
                    "live-configured",
                    null),
                new ProviderHealthReport(
                    "rest-transport-adapter",
                    "REST Transport Adapter",
                    ProviderHealthStatus.Healthy,
                    DateTimeOffset.Parse("2026-06-22T09:05:00Z"),
                    "Healthy",
                    ["live-endpoint-configured"]));

            var projectionService = new TransportProjectionService(provider, store);
            var syncWorkflowService = new TransportSyncWorkflowService(provider, store);
            var resolutionLedgerService = new TransportExceptionResolutionLedgerService(store);

            var firstImport = await syncWorkflowService.ImportSnapshotAsync("tenant-a");
            await store.AppendWorkflowRunAsync(
                "tenant-a",
                "transport-sync-import",
                firstImport.ProviderId,
                null,
                firstImport.Source,
                firstImport.SyncStatus,
                new TransportSyncImportEvidenceReadModel(
                    firstImport,
                    [
                        new RouteSummaryReadModel(
                            delayedRouteId,
                            "RT-100",
                            Guid.NewGuid(),
                            "TRK-100",
                            "Delayed",
                            4,
                            6,
                            1),
                        new RouteSummaryReadModel(
                            atRiskRouteId,
                            "RT-200",
                            Guid.NewGuid(),
                            "TRK-200",
                            "At risk",
                            3,
                            4,
                            1)
                    ]));

            var latestStatus = new TransportSyncStatusReadModel(
                "rest-transport-adapter",
                "live",
                true,
                true,
                3,
                [delayedRouteId, Guid.Parse("33333333-3333-3333-3333-333333333333"), Guid.Parse("44444444-4444-4444-4444-444444444444")],
                ["RT-100", "RT-300", "RT-400"],
                DateTimeOffset.Parse("2026-06-22T10:00:00Z"),
                DateTimeOffset.Parse("2026-06-22T10:00:00Z"),
                DateTimeOffset.Parse("2026-06-22T10:00:00Z"),
                "live-configured",
                null,
                new ProviderHealthReport(
                    "rest-transport-adapter",
                    "REST Transport Adapter",
                    ProviderHealthStatus.Healthy,
                    DateTimeOffset.Parse("2026-06-22T10:00:00Z"),
                    "Healthy",
                    ["live-endpoint-configured"]));

            await store.AppendWorkflowRunAsync(
                "tenant-a",
                "transport-sync-import",
                "rest-transport-adapter",
                null,
                "live",
                "live-configured",
                new TransportSyncImportEvidenceReadModel(
                    latestStatus,
                    [
                        new RouteSummaryReadModel(
                            delayedRouteId,
                            "RT-100",
                            Guid.NewGuid(),
                            "TRK-100",
                            "Delayed",
                            5,
                            6,
                            1),
                        new RouteSummaryReadModel(
                            Guid.Parse("33333333-3333-3333-3333-333333333333"),
                            "RT-300",
                            Guid.NewGuid(),
                            "TRK-300",
                            "On time",
                            2,
                            2,
                            0),
                        new RouteSummaryReadModel(
                            Guid.Parse("44444444-4444-4444-4444-444444444444"),
                            "RT-400",
                            Guid.NewGuid(),
                            "TRK-400",
                            "On time",
                            3,
                            3,
                            1)
                    ]));

            var historyService = new TransportSyncHistoryService(store);
            var workbenchService = new TransportExceptionWorkbenchService(
                projectionService,
                syncWorkflowService,
                historyService,
                resolutionLedgerService);

            var workbench = await workbenchService.BuildAsync("tenant-a");

            Assert.True(workbench.ExceptionCount >= 3);
            Assert.True(workbench.Groups.Count >= 2);
            Assert.Contains(workbench.Groups, group => group.Label == "Route");
            Assert.Contains(workbench.Items, item => item.ExceptionId == "latest-import-delta");
            Assert.Contains(workbench.Items, item => item.RouteReference == "RT-100" && item.RecommendedAction == "focus-route-diff");
            Assert.Contains(workbench.Items, item => item.RouteReference == "RT-200" && item.RecommendedAction == "focus-route-diff");

            await resolutionLedgerService.SaveAsync(
                "tenant-a",
                new TransportExceptionResolutionWriteRequest(
                    "latest-import-delta",
                    "Resolved",
                    "Reviewed during support pass.",
                    null,
                    null));

            var refreshedWorkbench = await workbenchService.BuildAsync("tenant-a");
            var resolvedItem = refreshedWorkbench.Items.First(item => item.ExceptionId == "latest-import-delta");
            Assert.Equal("Resolved", resolvedItem.ResolutionStatus);
            Assert.Equal("Reviewed during support pass.", resolvedItem.ResolutionNote);

            await resolutionLedgerService.SaveAsync(
                "tenant-a",
                new TransportExceptionResolutionWriteRequest(
                    "latest-import-delta",
                    "Deferred",
                    "Waiting for upstream confirmation.",
                    "Ops Alice",
                    DateTimeOffset.Parse("2026-06-30T16:00:00Z")));

            await resolutionLedgerService.SaveAsync(
                "tenant-a",
                new TransportExceptionResolutionWriteRequest(
                    "sync-posture-attention",
                    "Deferred",
                    "Needs escalation owner assignment.",
                    null,
                    DateTimeOffset.Parse("2020-01-01T10:00:00Z")));

            var resolutionHistory = await resolutionLedgerService.GetHistoryAsync("tenant-a", 10);
            Assert.Equal(3, resolutionHistory.EntryCount);
            Assert.Contains(
                resolutionHistory.Entries,
                entry => entry.ExceptionId == "latest-import-delta"
                         && entry.Status == "Deferred"
                         && entry.FollowUpOwner == "Ops Alice");
            Assert.Contains(
                resolutionHistory.Entries,
                entry => entry.ExceptionId == "sync-posture-attention"
                         && entry.Status == "Deferred"
                         && entry.FollowUpOwner is null);

            var latestLedger = await resolutionLedgerService.GetAsync("tenant-a");
            var latestResolution = latestLedger.Entries.Single(entry => entry.ExceptionId == "latest-import-delta");
            Assert.Equal("Deferred", latestResolution.Status);
            Assert.Equal("Waiting for upstream confirmation.", latestResolution.Note);
            Assert.Equal("Ops Alice", latestResolution.FollowUpOwner);
            Assert.Equal(DateTimeOffset.Parse("2026-06-30T16:00:00Z"), latestResolution.TargetReturnAtUtc);

            var followUpQueueService = new TransportExceptionFollowUpQueueService(
                workbenchService,
                resolutionLedgerService);
            var followUpQueue = await followUpQueueService.BuildAsync("tenant-a");

            Assert.Equal(2, followUpQueue.FollowUpCount);
            Assert.Equal(2, followUpQueue.ActiveDeferredCount);
            Assert.Equal(0, followUpQueue.RetiredFollowUpCount);
            Assert.Equal(1, followUpQueue.OwnerlessCount);
            Assert.Equal(0, followUpQueue.AtRiskCount);
            Assert.Equal(1, followUpQueue.OverdueCount);
            Assert.Equal(1, followUpQueue.HealthyCommitmentCount);
            Assert.Equal("sync-posture-attention", followUpQueue.FocusExceptionId);
            Assert.Equal("sync-posture-attention", followUpQueue.FocusTitle);
            Assert.Contains("Escalate", followUpQueue.FocusSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("overdue", followUpQueue.EscalationDigest.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(2, followUpQueue.HandoffPack.ActiveItemCount);
            Assert.Equal(1, followUpQueue.HandoffPack.ImmediateCount);
            Assert.Equal(1, followUpQueue.HandoffPack.MissingOwnerCount);
            Assert.Equal(0, followUpQueue.HandoffPack.MissingNoteCount);
            Assert.Equal(2, followUpQueue.HandoffPack.MissingRouteContextCount);
            Assert.True(followUpQueue.HandoffPack.BriefingLines.Count >= 2);
            Assert.Contains("handoff", followUpQueue.HandoffPack.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(2, followUpQueue.OwnerSummaries.Count);
            Assert.Contains(
                followUpQueue.OwnerSummaries,
                summary => summary.Owner == "Ops Alice"
                           && summary.FollowUpCount == 1
                           && summary.ActiveCount == 1
                           && summary.OverdueCount == 0);
            Assert.Contains(
                followUpQueue.OwnerSummaries,
                summary => summary.IsUnassigned
                           && summary.FollowUpCount == 1
                           && summary.OverdueCount == 1);

            var followUpItem = followUpQueue.Items.Single(item => item.ExceptionId == "latest-import-delta");
            Assert.Equal("latest-import-delta", followUpItem.ExceptionId);
            Assert.Equal("Deferred", followUpItem.Status);
            Assert.Equal("Waiting for upstream confirmation.", followUpItem.Note);
            Assert.Equal("Ops Alice", followUpItem.FollowUpOwner);
            Assert.Equal(DateTimeOffset.Parse("2026-06-30T16:00:00Z"), followUpItem.TargetReturnAtUtc);
            Assert.True(followUpItem.IsStillActive);
            Assert.False(followUpItem.IsOwnerMissing);
            Assert.False(followUpItem.IsOverdue);
            Assert.Equal("Healthy", followUpItem.AlertSeverity);
            Assert.Equal("Healthy", followUpItem.SlaPosture);
            Assert.Equal(2, followUpItem.UpdateCount);
            Assert.Equal("Resolved", followUpItem.PreviousStatus);
            var handoffHealthyItem = followUpQueue.HandoffPack.Items.Single(item => item.ExceptionId == "latest-import-delta");
            Assert.Equal("Missing route", handoffHealthyItem.ReadinessPosture);
            Assert.Contains("Ops Alice", handoffHealthyItem.HandoffSummary, StringComparison.OrdinalIgnoreCase);

            var overdueItem = followUpQueue.Items.Single(item => item.ExceptionId == "sync-posture-attention");
            Assert.False(overdueItem.IsStillActive);
            Assert.True(overdueItem.IsOwnerMissing);
            Assert.True(overdueItem.IsOverdue);
            Assert.Equal("Critical", overdueItem.AlertSeverity);
            Assert.Equal("Overdue", overdueItem.SlaPosture);
            Assert.Contains("passed", overdueItem.AlertSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("sync-posture-attention", followUpQueue.Items.First().ExceptionId);
            var handoffOverdueItem = followUpQueue.HandoffPack.Items.Single(item => item.ExceptionId == "sync-posture-attention");
            Assert.Equal("Missing owner", handoffOverdueItem.ReadinessPosture);
            Assert.Contains("Assign an owner", handoffOverdueItem.ReadinessSummary, StringComparison.OrdinalIgnoreCase);

            var refreshedWorkbenchWithCommitment = await workbenchService.BuildAsync("tenant-a");
            var deferredItem = refreshedWorkbenchWithCommitment.Items.First(item => item.ExceptionId == "latest-import-delta");
            Assert.Equal("Active", deferredItem.ResolutionFollowUpStatus);
            Assert.Equal("Ops Alice", deferredItem.ResolutionFollowUpOwner);
            Assert.Equal(DateTimeOffset.Parse("2026-06-30T16:00:00Z"), deferredItem.ResolutionTargetReturnAtUtc);

            await resolutionLedgerService.TransitionFollowUpAsync(
                "tenant-a",
                "latest-import-delta",
                new TransportExceptionFollowUpTransitionRequest("retire", "Follow-up retired after manual confirmation.", null));

            var retiredQueue = await followUpQueueService.BuildAsync("tenant-a");
            var retiredItem = retiredQueue.Items.Single(item => item.ExceptionId == "latest-import-delta");
            Assert.Equal("Retired", retiredItem.FollowUpStatus);
            Assert.Equal(1, retiredQueue.RetiredFollowUpCount);

            var retiredWorkbench = await workbenchService.BuildAsync("tenant-a");
            var retiredWorkbenchItem = retiredWorkbench.Items.First(item => item.ExceptionId == "latest-import-delta");
            Assert.Equal("Retired", retiredWorkbenchItem.ResolutionFollowUpStatus);

            await resolutionLedgerService.TransitionFollowUpAsync(
                "tenant-a",
                "latest-import-delta",
                new TransportExceptionFollowUpTransitionRequest("reopen", "Issue resurfaced after closure.", null));

            var reopenedQueue = await followUpQueueService.BuildAsync("tenant-a");
            var reopenedItem = reopenedQueue.Items.Single(item => item.ExceptionId == "latest-import-delta");
            Assert.Equal("Active", reopenedItem.FollowUpStatus);
            Assert.Equal(0, reopenedQueue.RetiredFollowUpCount);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    private static RouteDetailReadModel BuildRouteDetail(
        Guid routeId,
        string reference,
        string status,
        int stopCount,
        int shipmentCount,
        int completedDeliveries)
    {
        return new RouteDetailReadModel(
            routeId,
            reference,
            Guid.NewGuid(),
            $"TRK-{reference}",
            "Operator Driver",
            status,
            status,
            500,
            420,
            stopCount,
            shipmentCount,
            completedDeliveries,
            DateTimeOffset.Parse("2026-06-22T10:00:00Z"),
            [],
            [],
            []);
    }

    private sealed class StubTransportProvider : ITransportProjectionProviderAdapter
    {
        private readonly IReadOnlyCollection<RouteDetailReadModel> _routes;
        private readonly ProviderSyncStatus _syncStatus;
        private readonly ProviderHealthReport _health;

        public StubTransportProvider(
            IReadOnlyCollection<RouteDetailReadModel> routes,
            ProviderSyncStatus syncStatus,
            ProviderHealthReport health)
        {
            _routes = routes;
            _syncStatus = syncStatus;
            _health = health;
        }

        public string ProviderId => "rest-transport-adapter";

        public string ProviderName => "REST Transport Adapter";

        public ProviderDomain Domain => ProviderDomain.Transport;

        public ProviderKind Kind => ProviderKind.Connector;

        public ValueTask<ProviderHealthReport> GetHealthAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_health);

        public ValueTask<ProviderCapabilitySet> GetCapabilitiesAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ProviderCapabilitySet(true, false, false, false, true, true, false));

        public ValueTask<ProviderSyncStatus> GetSyncStatusAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_syncStatus);

        public ValueTask<ProviderSchemaDescription> DescribeSchemaAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ProviderSchemaDescription(ProviderId, "routes", []));

        public ValueTask<IReadOnlyCollection<RouteSummaryReadModel>> ReadRoutesAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyCollection<RouteSummaryReadModel>>(
                _routes.Select(route => new RouteSummaryReadModel(
                    route.RouteId,
                    route.Reference,
                    route.TruckId,
                    route.TruckReference,
                    route.Status,
                    route.StopCount,
                    route.ShipmentCount,
                    route.CompletedDeliveryCount)).ToArray());

        public ValueTask<IReadOnlyCollection<RouteDetailReadModel>> ReadRouteDetailsAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_routes);
    }
}
