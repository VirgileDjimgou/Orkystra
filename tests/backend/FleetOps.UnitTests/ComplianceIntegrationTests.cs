using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Compliance;
using FleetOps.Api.Fleet;
using FleetOps.UnitTests.Infrastructure;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class ComplianceIntegrationTests(FleetOpsApiFactory factory) : IClassFixture<FleetOpsApiFactory>
{
    [Fact]
    public async Task AdminConfiguresTenantPolicyAndOtherTenantCannotReadIt()
    {
        using var north = factory.CreateClient();
        north.SetBearer((await north.LoginAsync("admin@northwind.local", "Admin123!")).AccessToken);

        var saved = await north.PutAsJsonAsync("/api/v1/compliance/policy", new UpdateCompliancePolicyRequest(true, 0));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var policy = await saved.Content.ReadFromJsonAsync<CompliancePolicyResponse>();
        Assert.True(policy!.BlocksAssignments);

        var type = await north.PostAsJsonAsync("/api/v1/compliance/document-types", new SaveComplianceDocumentTypeRequest("Season check", Core.Modules.Compliance.ComplianceSubjectType.Vehicle, true, true, true, null));
        Assert.Equal(HttpStatusCode.OK, type.StatusCode);

        using var south = factory.CreateClient();
        south.SetBearer((await south.LoginAsync("admin@southridge.local", "Admin123!")).AccessToken);
        var types = await south.GetFromJsonAsync<List<ComplianceDocumentTypeResponse>>("/api/v1/compliance/document-types");
        Assert.DoesNotContain(types!, x => x.Name == "Season check");
    }

    [Fact]
    public async Task DriverCannotChangeCompliancePolicy()
    {
        using var client = factory.CreateClient();
        client.SetBearer((await client.LoginAsync("driver@northwind.local", "Driver123!")).AccessToken);
        var response = await client.PutAsJsonAsync("/api/v1/compliance/policy", new UpdateCompliancePolicyRequest(true, 0));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReplacementKeepsOnlyTheNewDocumentActiveInTheMatrix()
    {
        using var client = factory.CreateClient();
        client.SetBearer((await client.LoginAsync("admin@northwind.local", "Admin123!")).AccessToken);
        var vehicles = await client.GetFromJsonAsync<List<VehicleResponse>>("/api/v1/fleet/vehicles");
        var vehicle = Assert.Single(vehicles!, x => x.RegistrationNumber == "NW-100");
        var type = await client.PostAsJsonAsync("/api/v1/compliance/document-types", new SaveComplianceDocumentTypeRequest("Season permit", Core.Modules.Compliance.ComplianceSubjectType.Vehicle, true, false, true, null));
        Assert.Equal(HttpStatusCode.OK, type.StatusCode);
        var savedType = await type.Content.ReadFromJsonAsync<ComplianceDocumentTypeResponse>();

        var first = await client.PostAsJsonAsync("/api/v1/compliance/documents", new CreateComplianceDocumentV2Request(savedType!.Id, Core.Modules.Compliance.ComplianceSubjectType.Vehicle, vehicle.Id, "Season permit", "PERMIT-1", DateTimeOffset.UtcNow.AddDays(90), null, null, null));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<DocumentCreatedResponse>();
        var second = await client.PostAsJsonAsync("/api/v1/compliance/documents", new CreateComplianceDocumentV2Request(savedType.Id, Core.Modules.Compliance.ComplianceSubjectType.Vehicle, vehicle.Id, "Season permit", "PERMIT-2", DateTimeOffset.UtcNow.AddDays(120), null, null, firstBody!.Id));

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var matrix = await client.GetFromJsonAsync<List<ComplianceMatrixRowResponse>>("/api/v1/compliance/matrix");
        Assert.Equal((await second.Content.ReadFromJsonAsync<DocumentCreatedResponse>())!.Id, Assert.Single(matrix!, x => x.SubjectId == vehicle.Id && x.DocumentType == "Season permit").DocumentId);
    }

    private sealed record DocumentCreatedResponse(Guid Id, int ReviewStatus, long RowVersion);
}
