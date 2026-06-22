using System.Text.Json;
using Orkystra.Api.Connectors;
using Orkystra.Api.Persistence;
using Orkystra.Application.Connectors;
using Orkystra.Contracts.Connectors;
using Orkystra.Contracts.Transport;

namespace Orkystra.Api.ControlTower;

public sealed class TransportSyncWorkflowService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ProviderRegistryFactory? _providerRegistryFactory;
    private readonly ITransportProjectionProviderAdapter? _transportProvider;
    private readonly OperationalPersistenceStore _persistenceStore;

    public TransportSyncWorkflowService(
        ProviderRegistryFactory providerRegistryFactory,
        OperationalPersistenceStore persistenceStore)
    {
        _providerRegistryFactory = providerRegistryFactory;
        _persistenceStore = persistenceStore;
    }

    public TransportSyncWorkflowService(
        ITransportProjectionProviderAdapter transportProvider,
        OperationalPersistenceStore persistenceStore)
    {
        _transportProvider = transportProvider;
        _persistenceStore = persistenceStore;
    }

    public async ValueTask<TransportSyncStatusReadModel> ImportSnapshotAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var provider = ResolveTransportProvider();
        var routeDetails = await provider.ReadRouteDetailsAsync(cancellationToken);
        var routeSummaries = routeDetails
            .Select(route => new RouteSummaryReadModel(
                route.RouteId,
                route.Reference,
                route.TruckId,
                route.TruckReference,
                route.Status,
                route.StopCount,
                route.ShipmentCount,
                route.CompletedDeliveryCount))
            .ToArray();

        var syncStatus = await provider.GetSyncStatusAsync(cancellationToken);
        var health = await provider.GetHealthAsync(cancellationToken);
        var importedAtUtc = DateTimeOffset.UtcNow;
        var source = DetermineSource(syncStatus);
        var statusReadModel = new TransportSyncStatusReadModel(
            provider.ProviderId,
            source,
            source == "live",
            true,
            routeDetails.Count,
            routeDetails.Select(route => route.RouteId).ToArray(),
            routeDetails.Select(route => route.Reference).ToArray(),
            importedAtUtc,
            syncStatus.LastSuccessfulSyncAt ?? importedAtUtc,
            syncStatus.LastAttemptedSyncAt ?? importedAtUtc,
            syncStatus.Status,
            syncStatus.Detail,
            health);

        await _persistenceStore.UpsertProjectionAsync(
            tenantId,
            "route-summaries",
            "all",
            source,
            routeSummaries,
            cancellationToken);

        foreach (var route in routeDetails)
        {
            await _persistenceStore.UpsertProjectionAsync(
                tenantId,
                "route-detail",
                route.RouteId.ToString("D"),
                source,
                route,
                cancellationToken);
        }

        await _persistenceStore.UpsertProjectionAsync(
            tenantId,
            "transport-sync-status",
            provider.ProviderId,
            source,
            statusReadModel,
            cancellationToken);

        return statusReadModel;
    }

    public async ValueTask<TransportSyncStatusReadModel> GetLatestStatusAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var provider = ResolveTransportProvider();
        var persistedSnapshot = await _persistenceStore.ReadProjectionSnapshotAsync(
            tenantId,
            "transport-sync-status",
            provider.ProviderId,
            cancellationToken);

        if (persistedSnapshot is not null)
        {
            var persistedStatus = JsonSerializer.Deserialize<TransportSyncStatusReadModel>(
                persistedSnapshot.PayloadJson,
                SerializerOptions);
            if (persistedStatus is not null)
            {
                return persistedStatus;
            }
        }

        var syncStatus = await provider.GetSyncStatusAsync(cancellationToken);
        var health = await provider.GetHealthAsync(cancellationToken);
        var source = DetermineSource(syncStatus);

        return new TransportSyncStatusReadModel(
            provider.ProviderId,
            source,
            source == "live",
            false,
            0,
            [],
            [],
            null,
            syncStatus.LastSuccessfulSyncAt,
            syncStatus.LastAttemptedSyncAt,
            syncStatus.Status,
            syncStatus.Detail,
            health);
    }

    private ITransportProjectionProviderAdapter ResolveTransportProvider()
    {
        if (_transportProvider is not null)
        {
            return _transportProvider;
        }

        return _providerRegistryFactory!.CreateRegistry().ListByDomain(ProviderDomain.Transport)
            .OfType<ITransportProjectionProviderAdapter>()
            .First();
    }

    private static string DetermineSource(ProviderSyncStatus syncStatus)
    {
        return syncStatus.Status switch
        {
            "live-configured" => "live",
            "disabled" => "disabled",
            "auth-key-missing" => "configuration-incomplete",
            _ => "demo-fallback"
        };
    }
}
