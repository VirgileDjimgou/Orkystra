using System.Text.Json;
using Orkystra.Api.Persistence;
using Orkystra.Contracts.Transport;

namespace Orkystra.Api.ControlTower;

public sealed class TransportSyncHistoryService
{
  private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
  private readonly OperationalPersistenceStore _persistenceStore;

  public TransportSyncHistoryService(OperationalPersistenceStore persistenceStore)
  {
    _persistenceStore = persistenceStore;
  }

  public async ValueTask<TransportSyncDiffReadModel> BuildLatestDiffAsync(
      string tenantId,
      CancellationToken cancellationToken = default)
  {
    var runs = await _persistenceStore.ReadWorkflowRunsAsync(
        tenantId,
        "transport-sync-import",
        12,
        cancellationToken);

    var snapshots = runs
        .Select(TryReadSnapshot)
        .Where(snapshot => snapshot is not null)
        .Select(snapshot => snapshot!)
        .ToArray();

    if (snapshots.Length == 0)
    {
      return new TransportSyncDiffReadModel(
          false,
          "No transport import history exists yet.",
          null,
          null,
          0,
          0,
          0,
          0,
          0,
          []);
    }

    var latest = snapshots[0];
    var previous = snapshots.Skip(1).FirstOrDefault(snapshot => snapshot.Routes.Count > 0);

    if (previous is null)
    {
      return new TransportSyncDiffReadModel(
          false,
          "Only one transport import with comparable route evidence is available. Import another snapshot to unlock before/after route diffs.",
          latest.ImportedAtUtc,
          null,
          latest.Routes.Count,
          0,
          0,
          0,
          0,
          []);
    }

    var previousByReference = previous.Routes.ToDictionary(
        route => route.Reference,
        route => route,
        StringComparer.OrdinalIgnoreCase);
    var currentByReference = latest.Routes.ToDictionary(
        route => route.Reference,
        route => route,
        StringComparer.OrdinalIgnoreCase);

    var allReferences = previousByReference.Keys
        .Union(currentByReference.Keys, StringComparer.OrdinalIgnoreCase)
        .OrderBy(reference => reference, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    var diffs = new List<TransportSyncRouteDiffItemReadModel>(allReferences.Length);

    foreach (var reference in allReferences)
    {
      previousByReference.TryGetValue(reference, out var previousRoute);
      currentByReference.TryGetValue(reference, out var currentRoute);

      diffs.Add(BuildRouteDiff(reference, previousRoute, currentRoute));
    }

    var added = diffs.Count(diff => diff.ChangeType == "Added");
    var removed = diffs.Count(diff => diff.ChangeType == "Removed");
    var changed = diffs.Count(diff => diff.ChangeType == "Changed");

    var detail = changed == 0 && added == 0 && removed == 0
        ? "Latest import matches the previous snapshot route-for-route."
        : $"{changed} changed, {added} added, {removed} removed route(s) versus the previous import.";

    return new TransportSyncDiffReadModel(
        true,
        detail,
        latest.ImportedAtUtc,
        previous.ImportedAtUtc,
        latest.Routes.Count,
        previous.Routes.Count,
        added,
        removed,
        changed,
        diffs);
  }

  private static TransportSyncRouteDiffItemReadModel BuildRouteDiff(
      string reference,
      RouteSummaryReadModel? previous,
      RouteSummaryReadModel? current)
  {
    if (previous is null && current is not null)
    {
      return new TransportSyncRouteDiffItemReadModel(
          reference,
          null,
          current.RouteId,
          "Added",
          null,
          current.Status,
          null,
          current.StopCount,
          null,
          current.ShipmentCount,
          null,
          current.CompletedDeliveryCount,
          $"{reference} was added in the latest import.");
    }

    if (previous is not null && current is null)
    {
      return new TransportSyncRouteDiffItemReadModel(
          reference,
          previous.RouteId,
          null,
          "Removed",
          previous.Status,
          null,
          previous.StopCount,
          null,
          previous.ShipmentCount,
          null,
          previous.CompletedDeliveryCount,
          null,
          $"{reference} is no longer present in the latest import.");
    }

    var isChanged = previous!.Status != current!.Status
        || previous.StopCount != current.StopCount
        || previous.ShipmentCount != current.ShipmentCount
        || previous.CompletedDeliveryCount != current.CompletedDeliveryCount;

    var changeType = isChanged ? "Changed" : "Unchanged";
    var summary = isChanged
        ? $"{reference} changed from {previous.Status} to {current.Status}; stops {previous.StopCount}->{current.StopCount}, shipments {previous.ShipmentCount}->{current.ShipmentCount}, completed deliveries {previous.CompletedDeliveryCount}->{current.CompletedDeliveryCount}."
        : $"{reference} is unchanged across the latest two imports.";

    return new TransportSyncRouteDiffItemReadModel(
        reference,
        previous.RouteId,
        current.RouteId,
        changeType,
        previous.Status,
        current.Status,
        previous.StopCount,
        current.StopCount,
        previous.ShipmentCount,
        current.ShipmentCount,
        previous.CompletedDeliveryCount,
        current.CompletedDeliveryCount,
        summary);
  }

  private static SnapshotEvidence? TryReadSnapshot(PersistedWorkflowRun run)
  {
    // New payload format (Sprint 34): includes route summaries per import.
    var evidence = JsonSerializer.Deserialize<TransportSyncImportEvidenceReadModel>(run.PayloadJson, SerializerOptions);
    if (evidence is not null && evidence.Routes is { Count: > 0 })
    {
      return new SnapshotEvidence(
          evidence.SyncStatus.LastImportedAtUtc ?? run.CreatedAtUtc,
          evidence.Routes);
    }

    // Legacy payload format (Sprint 29-S33): status-only payload with route references.
    var legacy = JsonSerializer.Deserialize<TransportSyncStatusReadModel>(run.PayloadJson, SerializerOptions);
    if (legacy is null)
    {
      return null;
    }

    var fallbackRoutes = legacy.ImportedRouteReferences
        .Select(reference => new RouteSummaryReadModel(
            Guid.Empty,
            reference,
            Guid.Empty,
            "unknown",
            "Unknown",
            0,
            0,
            0))
        .ToArray();

    return new SnapshotEvidence(
        legacy.LastImportedAtUtc ?? run.CreatedAtUtc,
        fallbackRoutes);
  }

  private sealed record SnapshotEvidence(
      DateTimeOffset ImportedAtUtc,
      IReadOnlyCollection<RouteSummaryReadModel> Routes);
}
