using Orkystra.Contracts.Connectors;

namespace Orkystra.Application.Connectors;

public interface IProviderAdapter
{
    string ProviderId { get; }

    string ProviderName { get; }

    ProviderDomain Domain { get; }

    ProviderKind Kind { get; }

    ValueTask<ProviderHealthReport> GetHealthAsync(CancellationToken cancellationToken = default);

    ValueTask<ProviderCapabilitySet> GetCapabilitiesAsync(CancellationToken cancellationToken = default);

    ValueTask<ProviderSyncStatus> GetSyncStatusAsync(CancellationToken cancellationToken = default);

    ValueTask<ProviderSchemaDescription> DescribeSchemaAsync(CancellationToken cancellationToken = default);
}
