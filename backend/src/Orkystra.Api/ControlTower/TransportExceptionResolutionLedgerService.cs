using System.Text.Json;
using Orkystra.Api.Persistence;
using Orkystra.Contracts.Transport;

namespace Orkystra.Api.ControlTower;

public sealed class TransportExceptionResolutionLedgerService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly OperationalPersistenceStore _persistenceStore;
    private const string ProjectionName = "transport-exception-resolutions";
    private const string ProjectionKey = "active";

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
            ProjectionName,
            ProjectionKey,
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
        var nextEntries = current.Entries
            .Where(entry => !string.Equals(entry.ExceptionId, request.ExceptionId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        nextEntries.Add(new TransportExceptionResolutionEntryReadModel(
            request.ExceptionId,
            normalizedStatus,
            string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            DateTimeOffset.UtcNow));

        var nextLedger = new TransportExceptionResolutionLedgerReadModel(
            DateTimeOffset.UtcNow,
            nextEntries.Count,
            nextEntries
                .OrderByDescending(entry => entry.UpdatedAtUtc)
                .ToArray());

        await _persistenceStore.UpsertProjectionAsync(
            tenantId,
            ProjectionName,
            ProjectionKey,
            "api",
            nextLedger,
            cancellationToken);

        return nextLedger;
    }
}
