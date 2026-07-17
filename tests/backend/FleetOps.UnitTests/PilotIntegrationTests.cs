using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Pilot;
using FleetOps.Core.Modules.Pilot;
using FleetOps.UnitTests.Infrastructure;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class PilotIntegrationTests(FleetOpsApiFactory factory) : IClassFixture<FleetOpsApiFactory>
{
    [Fact]
    public async Task ConsentMetricsAndIncidentsStayInsideAuthenticatedTenant()
    {
        using var north = factory.CreateClient();
        north.SetBearer((await north.LoginAsync("admin@northwind.local", "Admin123!")).AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await north.PutAsJsonAsync("/api/v1/pilot/consent", new UpdatePilotConsentRequest(true))).StatusCode);
        var incident = await north.PostAsJsonAsync("/api/v1/pilot/incidents", new CreatePilotIncidentRequest(PilotIncidentSeverity.P1, "sync", "A pilot sync was delayed.", "Retry from the driver app."));
        Assert.Equal(HttpStatusCode.Created, incident.StatusCode);
        var northMetrics = await north.GetFromJsonAsync<PilotMetricsResponse>("/api/v1/pilot/metrics");
        Assert.True(northMetrics!.AnalyticsConsent);
        Assert.Equal(1, northMetrics.OpenIncidents);

        using var south = factory.CreateClient();
        south.SetBearer((await south.LoginAsync("admin@southridge.local", "Admin123!")).AccessToken);
        var southIncidents = await south.GetFromJsonAsync<List<PilotIncidentResponse>>("/api/v1/pilot/incidents");
        Assert.Empty(southIncidents!);
        var foreign = await south.PostAsJsonAsync($"/api/v1/pilot/incidents/{(await incident.Content.ReadFromJsonAsync<PilotIncidentResponse>())!.Id}/resolve", new ResolvePilotIncidentRequest("No access."));
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
}
