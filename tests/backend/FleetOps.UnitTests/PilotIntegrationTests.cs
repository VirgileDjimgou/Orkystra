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
    public async Task ConsentMetricsIncidentsAndExportsStayInsideAuthenticatedTenants()
    {
        using var north = factory.CreateClient();
        north.SetBearer((await north.LoginAsync("admin@northwind.local", "Admin123!")).AccessToken);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await north.PostAsync("/api/v1/pilot/metrics/collect", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await north.PutAsJsonAsync("/api/v1/pilot/consent", new UpdatePilotConsentRequest(true))).StatusCode);

        var firstDailyMetric = await north.PostAsync("/api/v1/pilot/metrics/collect", null);
        Assert.Equal(HttpStatusCode.OK, firstDailyMetric.StatusCode);
        var secondDailyMetric = await north.PostAsync("/api/v1/pilot/metrics/collect", null);
        Assert.Equal(HttpStatusCode.OK, secondDailyMetric.StatusCode);
        Assert.Equal(
            (await firstDailyMetric.Content.ReadFromJsonAsync<PilotDailyMetricResponse>())!.CapturedOnUtc,
            (await secondDailyMetric.Content.ReadFromJsonAsync<PilotDailyMetricResponse>())!.CapturedOnUtc);

        var incident = await north.PostAsJsonAsync(
            "/api/v1/pilot/incidents",
            new CreatePilotIncidentRequest(
                PilotIncidentSeverity.P1,
                "sync",
                "A pilot sync was delayed.",
                "Retry from the driver app."));
        Assert.Equal(HttpStatusCode.Created, incident.StatusCode);
        var incidentResponse = (await incident.Content.ReadFromJsonAsync<PilotIncidentResponse>())!;

        var decision = await north.PostAsJsonAsync(
            "/api/v1/pilot/decisions",
            new RecordPilotDecisionRequest("go", "Local delivery", "Pilot evidence supports the segment."));
        Assert.Equal(HttpStatusCode.Created, decision.StatusCode);

        var northExport = await north.GetFromJsonAsync<PilotEvidenceExportResponse>("/api/v1/pilot/export");
        Assert.True(northExport!.AnalyticsConsent);
        Assert.Single(northExport.DailyMetrics);
        Assert.Single(northExport.Incidents);
        Assert.Single(northExport.Decisions);

        using var south = factory.CreateClient();
        south.SetBearer((await south.LoginAsync("admin@southridge.local", "Admin123!")).AccessToken);
        Assert.Empty((await south.GetFromJsonAsync<List<PilotIncidentResponse>>("/api/v1/pilot/incidents"))!);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await south.PostAsJsonAsync(
                $"/api/v1/pilot/incidents/{incidentResponse.Id}/resolve",
                new ResolvePilotIncidentRequest("No access."))).StatusCode);
        var southExport = await south.GetFromJsonAsync<PilotEvidenceExportResponse>("/api/v1/pilot/export");
        Assert.False(southExport!.AnalyticsConsent);
        Assert.Empty(southExport.DailyMetrics);
        Assert.Empty(southExport.Incidents);
        Assert.Empty(southExport.Decisions);

        using var west = factory.CreateClient();
        west.SetBearer((await west.LoginAsync("admin@westland.local", "Admin123!")).AccessToken);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await west.PostAsJsonAsync(
                $"/api/v1/pilot/incidents/{incidentResponse.Id}/resolve",
                new ResolvePilotIncidentRequest("No access."))).StatusCode);
        var westExport = await west.GetFromJsonAsync<PilotEvidenceExportResponse>("/api/v1/pilot/export");
        Assert.False(westExport!.AnalyticsConsent);
        Assert.Empty(westExport.DailyMetrics);
        Assert.Empty(westExport.Incidents);
        Assert.Empty(westExport.Decisions);
    }

    [Fact]
    public async Task PilotEvidenceRoutesRequireAnAdministrator()
    {
        using var operatorClient = factory.CreateClient();
        operatorClient.SetBearer((await operatorClient.LoginAsync("operator@northwind.local", "Operator123!")).AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.GetAsync("/api/v1/pilot/metrics")).StatusCode);
    }
}
