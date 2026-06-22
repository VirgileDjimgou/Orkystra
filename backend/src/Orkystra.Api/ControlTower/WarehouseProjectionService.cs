using Orkystra.Application.Connectors;
using Orkystra.Api.Connectors;
using Orkystra.Contracts.Warehouse;

namespace Orkystra.Api.ControlTower;

public sealed class WarehouseProjectionService
{
    private readonly ProviderRegistryFactory _providerRegistryFactory;

    public WarehouseProjectionService()
    {
        _providerRegistryFactory = new ProviderRegistryFactory();
    }

    public WarehouseProjectionService(ProviderRegistryFactory providerRegistryFactory)
    {
        _providerRegistryFactory = providerRegistryFactory;
    }

    public async ValueTask<IReadOnlyCollection<WarehouseSummaryReadModel>> ListAsync(CancellationToken cancellationToken = default)
    {
        var warehouseProvider = _providerRegistryFactory.CreateRegistry().ListByDomain(ProviderDomain.Warehouse)
            .OfType<IWarehouseProviderAdapter>()
            .First();

        return await warehouseProvider.ReadWarehousesAsync(cancellationToken);
    }

    public async ValueTask<WarehouseDetailReadModel?> GetByIdAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var warehouseProvider = _providerRegistryFactory.CreateRegistry().ListByDomain(ProviderDomain.Warehouse)
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
