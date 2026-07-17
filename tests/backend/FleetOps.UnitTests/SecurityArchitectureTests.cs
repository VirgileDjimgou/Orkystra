using System.Net;
using FleetOps.Api.Security;
using FleetOps.Core.Common;
using FleetOps.Core.Modules.Identity;
using FleetOps.UnitTests.Infrastructure;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class SecurityArchitectureTests(FleetOpsApiFactory factory) : IClassFixture<FleetOpsApiFactory>
{
    [Fact]
    public void EveryTenantEntityExposesTheMandatoryOrganizationId()
    {
        var tenantEntityTypes = typeof(TenantEntity).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(TenantEntity).IsAssignableFrom(type))
            .ToList();

        Assert.NotEmpty(tenantEntityTypes);
        Assert.All(tenantEntityTypes, type =>
        {
            var property = type.GetProperty(nameof(TenantEntity.OrganizationId));
            Assert.NotNull(property);
            Assert.Equal(typeof(Guid), property!.PropertyType);
        });
    }

    [Fact]
    public void CentralRoleMatrixKeepsAdministrativeAndDriverCapabilitiesDisjoint()
    {
        Assert.Equal([SystemRoles.Admin], AuthorizationPolicies.RoleMatrix[AuthorizationPolicies.AdminOnly]);
        Assert.Equal([SystemRoles.Driver], AuthorizationPolicies.RoleMatrix[AuthorizationPolicies.DriverOnly]);
        Assert.DoesNotContain(SystemRoles.Driver, AuthorizationPolicies.RoleMatrix[AuthorizationPolicies.OperationsWrite]);
        Assert.DoesNotContain(SystemRoles.Operator, AuthorizationPolicies.RoleMatrix[AuthorizationPolicies.AdminOnly]);
    }

    [Fact]
    public async Task SensitiveOperationsEnforceTheRoleMatrixOnTheServer()
    {
        using var operatorClient = factory.CreateClient();
        var operatorLogin = await operatorClient.LoginAsync("operator@northwind.local", "Operator123!");
        operatorClient.SetBearer(operatorLogin.AccessToken);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await operatorClient.PostAsync("/api/v1/driver/uploads/sessions", null)).StatusCode);

        using var driverClient = factory.CreateClient();
        var driverLogin = await driverClient.LoginAsync("driver@northwind.local", "Driver123!");
        driverClient.SetBearer(driverLogin.AccessToken);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await driverClient.GetAsync("/api/v1/admin/users")).StatusCode);
    }
}
