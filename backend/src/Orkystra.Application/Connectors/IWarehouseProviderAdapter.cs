using Orkystra.Contracts.Warehouse;

namespace Orkystra.Application.Connectors;

public interface IWarehouseProviderAdapter : IProviderAdapter
{
    ValueTask<IReadOnlyCollection<WarehouseSummaryReadModel>> ReadWarehousesAsync(CancellationToken cancellationToken = default);
}
