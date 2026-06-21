using Orkystra.Application.Connectors;
using Orkystra.Application.Connectors.Providers;
using Orkystra.Contracts.Warehouse;

namespace Orkystra.Api.ControlTower;

public sealed class WarehouseProjectionService
{
    private readonly ProviderRegistry _providerRegistry;

    public WarehouseProjectionService()
    {
        _providerRegistry = new ProviderRegistry(
        [
            new CsvWarehouseImportProvider(),
            new RestTransportProvider(),
            new GpsTelematicsProvider()
        ]);
    }

    public async ValueTask<IReadOnlyCollection<WarehouseSummaryReadModel>> ListAsync(CancellationToken cancellationToken = default)
    {
        var warehouseProvider = _providerRegistry.ListByDomain(ProviderDomain.Warehouse)
            .OfType<IWarehouseProviderAdapter>()
            .First();

        return await warehouseProvider.ReadWarehousesAsync(cancellationToken);
    }

    public async ValueTask<WarehouseDetailReadModel?> GetByIdAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var warehouseProvider = _providerRegistry.ListByDomain(ProviderDomain.Warehouse)
            .OfType<IWarehouseProjectionProviderAdapter>()
            .FirstOrDefault();

        if (warehouseProvider is null)
        {
            return null;
        }

        var warehouses = await warehouseProvider.ReadWarehouseDetailsAsync(cancellationToken);
        return warehouses.FirstOrDefault(warehouse => warehouse.WarehouseId == warehouseId);
    }
}
