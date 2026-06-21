using System.Text.Json;
using Microsoft.Extensions.Options;
using Orkystra.Api.Persistence;

namespace Orkystra.Domain.Tests;

public sealed class OperationalPersistenceStoreTests
{
    [Fact]
    public async Task UpsertProjectionAsync_persists_latest_snapshot_per_projection_key()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("orkystra-persistence-tests");

        try
        {
            var store = new OperationalPersistenceStore(
                Options.Create(new OperationalPersistenceOptions
                {
                    DatabasePath = Path.Combine("data", "operations.db")
                }),
                tempDirectory.FullName);

            await store.UpsertProjectionAsync(
                "tenant-a",
                "control-tower-overview",
                "current",
                "api",
                new { status = "first", routes = 3 });

            await store.UpsertProjectionAsync(
                "tenant-a",
                "control-tower-overview",
                "current",
                "api",
                new { status = "second", routes = 4 });

            await store.UpsertProjectionAsync(
                "tenant-a",
                "route-detail",
                "route-1",
                "api",
                new { routeReference = "RT-412" });

            var snapshots = await store.ReadProjectionSnapshotsAsync("tenant-a", null, 10);

            Assert.Equal(2, snapshots.Count);
            Assert.Contains(snapshots, snapshot => snapshot.ProjectionName == "control-tower-overview");
            Assert.Contains(snapshots, snapshot => snapshot.ProjectionName == "route-detail");

            var overviewSnapshot = snapshots.Single(snapshot => snapshot.ProjectionName == "control-tower-overview");
            using var overviewPayload = JsonDocument.Parse(overviewSnapshot.PayloadJson);
            Assert.Equal("second", overviewPayload.RootElement.GetProperty("status").GetString());
            Assert.Equal(4, overviewPayload.RootElement.GetProperty("routes").GetInt32());
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Fact]
    public async Task AppendWorkflowRunAsync_persists_recent_runs_in_reverse_chronological_order()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("orkystra-persistence-tests");

        try
        {
            var store = new OperationalPersistenceStore(
                Options.Create(new OperationalPersistenceOptions
                {
                    DatabasePath = Path.Combine("data", "operations.db")
                }),
                tempDirectory.FullName);

            await store.AppendWorkflowRunAsync(
                "tenant-a",
                "ai-recommendation",
                "control-tower",
                "scenario-1",
                "api",
                "completed",
                new { answer = "first" });

            await Task.Delay(20);

            await store.AppendWorkflowRunAsync(
                "tenant-a",
                "route-optimization",
                "route-1",
                "scenario-1",
                "fallback",
                "optimized",
                new { routeReference = "RT-412" });

            var runs = await store.ReadWorkflowRunsAsync("tenant-a", null, 10);

            Assert.Equal(2, runs.Count);
            Assert.Equal("route-optimization", runs.First().WorkflowKind);
            Assert.Equal("ai-recommendation", runs.Last().WorkflowKind);

            using var payload = JsonDocument.Parse(runs.First().PayloadJson);
            Assert.Equal("RT-412", payload.RootElement.GetProperty("routeReference").GetString());
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }
}
