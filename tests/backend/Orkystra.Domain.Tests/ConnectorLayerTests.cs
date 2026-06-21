using Orkystra.Application.Connectors;
using Orkystra.Application.Connectors.Providers;
using Orkystra.Contracts.Connectors;

namespace Orkystra.Domain.Tests;

public sealed class ConnectorLayerTests
{
    [Fact]
    public async Task ProviderRegistry_can_resolve_registered_providers_by_domain_and_capability()
    {
        var registry = new ProviderRegistry(
        [
            new CsvWarehouseImportProvider(),
            new RestTransportProvider(),
            new GpsTelematicsProvider()
        ]);

        var gpsProviders = registry.ListByDomain(ProviderDomain.Gps);
        var streamProviders = await registry.ListByCapabilityAsync(capabilities => capabilities.CanStreamEvents);

        Assert.Single(gpsProviders);
        Assert.Single(streamProviders);
        Assert.Equal("gps-telematics-adapter", streamProviders.Single().ProviderId);
    }

    [Fact]
    public async Task CsvWarehouseImportProvider_exposes_vendor_neutral_schema_and_health()
    {
        var provider = new CsvWarehouseImportProvider();

        var health = await provider.GetHealthAsync();
        var schema = await provider.DescribeSchemaAsync();
        var warehouses = await provider.ReadWarehousesAsync();

        Assert.Equal(ProviderHealthStatus.Healthy, health.Status);
        Assert.Contains(schema.Fields, field => field.CanonicalMapping == "Warehouse.StoredPalletCount");
        Assert.Equal(2, warehouses.Count);
        Assert.Contains(warehouses, warehouse => warehouse.Name == "West Flow Center");
    }

    [Fact]
    public async Task RestTransportProvider_and_GpsTelematicsProvider_expose_same_common_contract_surface()
    {
        IProviderAdapter restProvider = new RestTransportProvider();
        IProviderAdapter gpsProvider = new GpsTelematicsProvider();

        var restCapabilities = await restProvider.GetCapabilitiesAsync();
        var gpsCapabilities = await gpsProvider.GetCapabilitiesAsync();
        var restSyncStatus = await restProvider.GetSyncStatusAsync();
        var gpsSyncStatus = await gpsProvider.GetSyncStatusAsync();

        Assert.True(restCapabilities.CanRead);
        Assert.True(gpsCapabilities.CanRead);
        Assert.NotNull(restSyncStatus.Status);
        Assert.NotNull(gpsSyncStatus.Status);
    }

    [Fact]
    public async Task Registry_supports_swappable_domain_specific_provider_access()
    {
        var csvProvider = new CsvWarehouseImportProvider();
        var registry = new ProviderRegistry([csvProvider]);

        var found = registry.TryGet(csvProvider.ProviderId, out var resolved);
        var warehouses = await ((IWarehouseProviderAdapter)resolved!).ReadWarehousesAsync();

        Assert.True(found);
        Assert.NotNull(resolved);
        Assert.Equal(ProviderDomain.Warehouse, resolved.Domain);
        Assert.Equal(2, warehouses.Count);
    }
}
