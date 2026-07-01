using Microsoft.Extensions.Options;
using Orkystra.Api.ControlTower;
using Orkystra.Api.Persistence;
using Orkystra.Contracts.Transport;

namespace Orkystra.Domain.Tests;

public sealed class TransportSyncHistoryServiceTests
{
  [Fact]
  public async Task BuildLatestDiffAsync_returns_route_level_changes_between_latest_two_imports()
  {
    var tempDirectory = Directory.CreateTempSubdirectory("orkystra-transport-sync-diff-tests");

    try
    {
      var store = new SqliteOperationalPersistenceStore(
          Options.Create(new OperationalPersistenceOptions
          {
            DatabasePath = Path.Combine("data", "operations.db")
          }),
          tempDirectory.FullName);

      var previousStatus = new TransportSyncStatusReadModel(
          "rest-transport-adapter",
          "live",
          true,
          true,
          2,
          [Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222")],
          ["RT-100", "RT-200"],
          DateTimeOffset.Parse("2026-06-22T08:00:00Z"),
          DateTimeOffset.Parse("2026-06-22T08:00:00Z"),
          DateTimeOffset.Parse("2026-06-22T08:00:00Z"),
          "live-configured",
          null,
          new Orkystra.Contracts.Connectors.ProviderHealthReport(
              "rest-transport-adapter",
              "REST Transport Adapter",
              Orkystra.Contracts.Connectors.ProviderHealthStatus.Healthy,
              DateTimeOffset.Parse("2026-06-22T08:00:00Z"),
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
              previousStatus,
              [
                  new RouteSummaryReadModel(
                            Guid.Parse("11111111-1111-1111-1111-111111111111"),
                            "RT-100",
                            Guid.NewGuid(),
                            "TRK-100",
                            "On time",
                            3,
                            5,
                            1),
                        new RouteSummaryReadModel(
                            Guid.Parse("22222222-2222-2222-2222-222222222222"),
                            "RT-200",
                            Guid.NewGuid(),
                            "TRK-200",
                            "Delayed",
                            4,
                            6,
                            2)
              ]));

      var latestStatus = previousStatus with
      {
        ImportedRouteCount = 2,
        ImportedRouteIds = [Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("33333333-3333-3333-3333-333333333333")],
        ImportedRouteReferences = ["RT-100", "RT-300"],
        LastImportedAtUtc = DateTimeOffset.Parse("2026-06-22T09:00:00Z")
      };

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
                  // RT-100 changed
                  new RouteSummaryReadModel(
                            Guid.Parse("11111111-1111-1111-1111-111111111111"),
                            "RT-100",
                            Guid.NewGuid(),
                            "TRK-100",
                            "Delayed",
                            4,
                            5,
                            1),
                        // RT-300 added
                        new RouteSummaryReadModel(
                            Guid.Parse("33333333-3333-3333-3333-333333333333"),
                            "RT-300",
                            Guid.NewGuid(),
                            "TRK-300",
                            "On time",
                            2,
                            2,
                            0)
              ]));

      var service = new TransportSyncHistoryService(store);

      var diff = await service.BuildLatestDiffAsync("tenant-a");

      Assert.True(diff.HasComparableHistory);
      Assert.Equal(2, diff.LatestRouteCount);
      Assert.Equal(2, diff.PreviousRouteCount);
      Assert.Equal(1, diff.AddedRouteCount);
      Assert.Equal(1, diff.RemovedRouteCount);
      Assert.Equal(1, diff.ChangedRouteCount);

      Assert.Contains(diff.RouteDiffs, item => item.RouteReference == "RT-300" && item.ChangeType == "Added");
      Assert.Contains(diff.RouteDiffs, item => item.RouteReference == "RT-200" && item.ChangeType == "Removed");
      Assert.Contains(diff.RouteDiffs, item => item.RouteReference == "RT-100" && item.ChangeType == "Changed");
    }
    finally
    {
      tempDirectory.Delete(true);
    }
  }

  [Fact]
  public async Task BuildLatestDiffAsync_handles_empty_history()
  {
    var tempDirectory = Directory.CreateTempSubdirectory("orkystra-transport-sync-diff-tests");

    try
    {
      var store = new SqliteOperationalPersistenceStore(
          Options.Create(new OperationalPersistenceOptions
          {
            DatabasePath = Path.Combine("data", "operations.db")
          }),
          tempDirectory.FullName);

      var service = new TransportSyncHistoryService(store);
      var diff = await service.BuildLatestDiffAsync("tenant-a");

      Assert.False(diff.HasComparableHistory);
      Assert.Equal("No transport import history exists yet.", diff.Detail);
      Assert.Empty(diff.RouteDiffs);
    }
    finally
    {
      tempDirectory.Delete(true);
    }
  }

  [Fact]
  public async Task BuildRecentHistoryAsync_returns_structured_recent_import_entries()
  {
    var tempDirectory = Directory.CreateTempSubdirectory("orkystra-transport-sync-history-tests");

    try
    {
      var store = new SqliteOperationalPersistenceStore(
          Options.Create(new OperationalPersistenceOptions
          {
            DatabasePath = Path.Combine("data", "operations.db")
          }),
          tempDirectory.FullName);

      var firstStatus = new TransportSyncStatusReadModel(
          "rest-transport-adapter",
          "live",
          true,
          true,
          1,
          [Guid.Parse("11111111-1111-1111-1111-111111111111")],
          ["RT-100"],
          DateTimeOffset.Parse("2026-06-22T08:00:00Z"),
          DateTimeOffset.Parse("2026-06-22T08:00:00Z"),
          DateTimeOffset.Parse("2026-06-22T08:00:00Z"),
          "live-configured",
          null,
          new Orkystra.Contracts.Connectors.ProviderHealthReport(
              "rest-transport-adapter",
              "REST Transport Adapter",
              Orkystra.Contracts.Connectors.ProviderHealthStatus.Healthy,
              DateTimeOffset.Parse("2026-06-22T08:00:00Z"),
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
              firstStatus,
              [
                  new RouteSummaryReadModel(
                      Guid.Parse("11111111-1111-1111-1111-111111111111"),
                      "RT-100",
                      Guid.NewGuid(),
                      "TRK-100",
                      "On time",
                      3,
                      5,
                      1)
              ]));

      var secondStatus = firstStatus with
      {
        ImportedRouteCount = 2,
        ImportedRouteIds = [Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222")],
        ImportedRouteReferences = ["RT-100", "RT-200"],
        LastImportedAtUtc = DateTimeOffset.Parse("2026-06-22T09:00:00Z")
      };

      await store.AppendWorkflowRunAsync(
          "tenant-a",
          "transport-sync-import",
          "rest-transport-adapter",
          null,
          "live",
          "live-configured",
          new TransportSyncImportEvidenceReadModel(
              secondStatus,
              [
                  new RouteSummaryReadModel(
                      Guid.Parse("11111111-1111-1111-1111-111111111111"),
                      "RT-100",
                      Guid.NewGuid(),
                      "TRK-100",
                      "Delayed",
                      4,
                      5,
                      1),
                  new RouteSummaryReadModel(
                      Guid.Parse("22222222-2222-2222-2222-222222222222"),
                      "RT-200",
                      Guid.NewGuid(),
                      "TRK-200",
                      "On time",
                      2,
                      2,
                      0)
              ]));

      var service = new TransportSyncHistoryService(store);
      var history = await service.BuildRecentHistoryAsync("tenant-a", 6);

      Assert.Equal(2, history.Count);
      Assert.Equal(2, history.Entries.Count);

      var latest = history.Entries.First();
      Assert.True(latest.HasComparablePrevious);
      Assert.Equal(1, latest.AddedRouteCount);
      Assert.Equal(0, latest.RemovedRouteCount);
      Assert.Equal(1, latest.ChangedRouteCount);
      Assert.Contains("2 routes imported from live", latest.Summary);

      var oldest = history.Entries.Last();
      Assert.False(oldest.HasComparablePrevious);
      Assert.Equal(0, oldest.AddedRouteCount);
      Assert.Equal(0, oldest.RemovedRouteCount);
      Assert.Equal(0, oldest.ChangedRouteCount);
    }
    finally
    {
      tempDirectory.Delete(true);
    }
  }
}
