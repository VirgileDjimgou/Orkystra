using Orkystra.Contracts.Warehouse;

namespace Orkystra.Application.Connectors;

public interface IWarehouseProjectionProviderAdapter : IWarehouseProviderAdapter
{
    ValueTask<IReadOnlyCollection<WarehouseDetailReadModel>> ReadWarehouseDetailsAsync(CancellationToken cancellationToken = default);
}
