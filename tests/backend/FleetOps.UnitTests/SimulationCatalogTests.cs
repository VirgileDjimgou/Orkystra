using FleetOps.Simulation;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class SimulationCatalogTests
{
    [Fact]
    public void FullSimulationCatalogHasThreeDistinctSectorsAndEveryRole()
    {
        Assert.Equal(3, SimulationCatalog.Tenants.Count);
        Assert.Equal(3, SimulationCatalog.Tenants.Select(x => x.Sector).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(3, SimulationCatalog.Tenants.Select(x => x.Slug).Distinct(StringComparer.Ordinal).Count());
        Assert.All(SimulationCatalog.Tenants, tenant =>
        {
            Assert.Equal("Admin", tenant.Admin.Role);
            Assert.Equal("Operator", tenant.Operator.Role);
            Assert.Equal("Driver", tenant.Driver.Role);
            Assert.EndsWith($"@{tenant.Slug}.local", tenant.Admin.Email);
            Assert.EndsWith($"@{tenant.Slug}.local", tenant.Operator.Email);
            Assert.EndsWith($"@{tenant.Slug}.local", tenant.Driver.Email);
        });
    }
}
