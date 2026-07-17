using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Dispatch;
using FleetOps.UnitTests.Infrastructure;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class DispatchProductivityIntegrationTests(FleetOpsApiFactory factory) : IClassFixture<FleetOpsApiFactory>
{
    [Fact]
    public async Task TemplateCanBeCreatedAndDuplicatedInsideTenant()
    {
        using var client = factory.CreateClient();
        client.SetBearer((await client.LoginAsync("operator@northwind.local", "Operator123!")).AccessToken);
        var templateResponse = await client.PostAsJsonAsync("/api/v1/dispatch/templates", new CreateMissionTemplateRequest("Morning route", "Morning delivery", [new MissionTemplateStopRequest(1, "Depot", "1 Dispatch Way", 15), new MissionTemplateStopRequest(2, "Customer", "2 Fleet Way", 60)]));
        var template = await templateResponse.Content.ReadFromJsonAsync<MissionTemplateResponse>();
        var duplicateResponse = await client.PostAsJsonAsync($"/api/v1/dispatch/templates/{template!.Id}/duplicate", new DuplicateTemplateRequest("TPL-001", DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(2)));

        Assert.Equal(HttpStatusCode.Created, templateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task ReplayedImportDoesNotCreateDuplicateMissions()
    {
        using var client = factory.CreateClient();
        client.SetBearer((await client.LoginAsync("operator@northwind.local", "Operator123!")).AccessToken);
        var start = DateTimeOffset.UtcNow.AddDays(2);
        var import = new DispatchImportPreviewRequest("daily-import-001", [new DispatchImportRow("IMP-001", "Imported route", start, start.AddHours(2), "Depot", "1 Dispatch Way", start.AddMinutes(15)), new DispatchImportRow("IMP-001", "Imported route", start, start.AddHours(2), "Customer", "2 Fleet Way", start.AddMinutes(75))]);
        var preview = await client.PostAsJsonAsync("/api/v1/dispatch/imports/preview", import);
        var first = await client.PostAsJsonAsync("/api/v1/dispatch/imports/confirm", import);
        var second = await client.PostAsJsonAsync("/api/v1/dispatch/imports/confirm", import);

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var board = await client.GetFromJsonAsync<DispatchBoardResponse>($"/api/v1/dispatch/board?fromUtc={Uri.EscapeDataString(start.AddDays(-1).ToString("O"))}&toUtc={Uri.EscapeDataString(start.AddDays(2).ToString("O"))}");
        Assert.Single(board!.Items, x => x.Reference == "IMP-001");
    }

    [Fact]
    public async Task AnotherTenantCannotAccessTemplate()
    {
        using var north = factory.CreateClient();
        north.SetBearer((await north.LoginAsync("operator@northwind.local", "Operator123!")).AccessToken);
        var created = await north.PostAsJsonAsync("/api/v1/dispatch/templates", new CreateMissionTemplateRequest("Tenant private", "Private route", [new MissionTemplateStopRequest(1, "Depot", "1 Dispatch Way", 10)]));
        var template = await created.Content.ReadFromJsonAsync<MissionTemplateResponse>();
        using var south = factory.CreateClient();
        south.SetBearer((await south.LoginAsync("admin@southridge.local", "Admin123!")).AccessToken);
        var duplicate = await south.PostAsJsonAsync($"/api/v1/dispatch/templates/{template!.Id}/duplicate", new DuplicateTemplateRequest("SOUTH-001", DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(1)));
        Assert.Equal(HttpStatusCode.NotFound, duplicate.StatusCode);
    }
}
