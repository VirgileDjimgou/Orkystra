using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Dispatch;
using FleetOps.Api.DriverApp;
using FleetOps.Api.Operations;
using FleetOps.Core.Modules.Dispatch;
using FleetOps.Core.Modules.Operations;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using FleetOps.UnitTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class OperationsIntegrationTests(FleetOpsApiFactory factory) : IClassFixture<FleetOpsApiFactory>
{
    [Fact]
    public async Task CriticalInspectionBlocksMissionStart()
    {
        await ResetDatabaseAsync();

        var mission = await CreateAssignedMissionAsync();
        using var driverClient = await CreateDriverClientAsync();

        var inspectionResponse = await driverClient.PostAsJsonAsync(
            $"/api/v1/driver/missions/{mission.Id}/inspection",
            new SubmitPreDepartureInspectionRequest(
                "inspection-critical-001",
                DateTimeOffset.UtcNow,
                "Front-right brake issue",
                [
                    new InspectionItemResultRequest(1, "brakes", "Brakes and steering", false, DefectSeverity.Critical, "Pedal pressure is unstable.", null),
                    new InspectionItemResultRequest(2, "lights", "Lights and signals", true, DefectSeverity.None, null, null),
                    new InspectionItemResultRequest(3, "cargo-secured", "Cargo and doors secured", true, DefectSeverity.None, null, null),
                ]));
        Assert.Equal(HttpStatusCode.OK, inspectionResponse.StatusCode);

        var startResponse = await driverClient.PostAsJsonAsync(
            $"/api/v1/driver/missions/{mission.Id}/commands",
            new SyncMissionCommandRequest(
                "start-after-critical-001",
                DriverMissionCommandAction.Start,
                mission.RowVersion,
                DateTimeOffset.UtcNow));

        Assert.Equal(HttpStatusCode.Conflict, startResponse.StatusCode);
    }

    [Fact]
    public async Task CleanInspectionAllowsMissionStart()
    {
        await ResetDatabaseAsync();

        var mission = await CreateAssignedMissionAsync();
        using var driverClient = await CreateDriverClientAsync();

        var inspectionResponse = await driverClient.PostAsJsonAsync(
            $"/api/v1/driver/missions/{mission.Id}/inspection",
            new SubmitPreDepartureInspectionRequest(
                "inspection-clean-001",
                DateTimeOffset.UtcNow,
                "Vehicle ready",
                [
                    new InspectionItemResultRequest(1, "brakes", "Brakes and steering", true, DefectSeverity.None, null, null),
                    new InspectionItemResultRequest(2, "lights", "Lights and signals", true, DefectSeverity.None, null, null),
                    new InspectionItemResultRequest(3, "cargo-secured", "Cargo and doors secured", true, DefectSeverity.None, null, null),
                ]));
        Assert.Equal(HttpStatusCode.OK, inspectionResponse.StatusCode);

        var startResponse = await driverClient.PostAsJsonAsync(
            $"/api/v1/driver/missions/{mission.Id}/commands",
            new SyncMissionCommandRequest(
                "start-clean-001",
                DriverMissionCommandAction.Start,
                mission.RowVersion,
                DateTimeOffset.UtcNow));
        var payload = await startResponse.Content.ReadFromJsonAsync<SyncMissionCommandResponse>();

        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(MissionStatus.EnRoute, payload!.Mission.Status);
    }

    [Fact]
    public async Task UploadSessionResumesAndMediaRequiresSignedUrl()
    {
        await ResetDatabaseAsync();

        using var driverClient = await CreateDriverClientAsync();
        var createResponse = await driverClient.PostAsJsonAsync(
            "/api/v1/driver/uploads/sessions",
            new UploadSessionRequest("proof.jpg", "image/jpeg", 10, MediaUploadPurpose.DeliveryProofPhoto));
        var session = await createResponse.Content.ReadFromJsonAsync<UploadSessionResponse>();
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(session);

        var image = DemoJpegBytes();
        var chunkOne = Convert.ToBase64String(image[..5]);
        var chunkTwo = Convert.ToBase64String(image[5..]);

        var appendOne = await driverClient.PostAsJsonAsync(
            $"/api/v1/driver/uploads/sessions/{session!.UploadSessionId}/chunks",
            new AppendUploadChunkRequest(0, chunkOne));
        var appendTwo = await driverClient.PostAsJsonAsync(
            $"/api/v1/driver/uploads/sessions/{session.UploadSessionId}/chunks",
            new AppendUploadChunkRequest(5, chunkTwo));
        var completeResponse = await driverClient.PostAsync(
            $"/api/v1/driver/uploads/sessions/{session.UploadSessionId}/complete",
            null);
        var asset = await completeResponse.Content.ReadFromJsonAsync<MediaAssetResponse>();

        Assert.Equal(HttpStatusCode.OK, appendOne.StatusCode);
        Assert.Equal(HttpStatusCode.OK, appendTwo.StatusCode);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        Assert.NotNull(asset);

        using var anonymousClient = factory.CreateClient();
        var unsignedResponse = await anonymousClient.GetAsync($"/api/v1/media/{asset!.AssetId}");
        var signedResponse = await anonymousClient.GetAsync(asset.ReadUrl);

        Assert.Equal(HttpStatusCode.BadRequest, unsignedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, signedResponse.StatusCode);
    }

    [Fact]
    public async Task MalformedUploadIsQuarantinedBeforeItCanBecomeMedia()
    {
        await ResetDatabaseAsync();
        using var driverClient = await CreateDriverClientAsync();
        var suspicious = "not-an-image"u8.ToArray();
        var createResponse = await driverClient.PostAsJsonAsync(
            "/api/v1/driver/uploads/sessions",
            new UploadSessionRequest("proof.jpg", "image/jpeg", suspicious.Length, MediaUploadPurpose.DeliveryProofPhoto));
        var session = await createResponse.Content.ReadFromJsonAsync<UploadSessionResponse>();

        await driverClient.PostAsJsonAsync(
            $"/api/v1/driver/uploads/sessions/{session!.UploadSessionId}/chunks",
            new AppendUploadChunkRequest(0, Convert.ToBase64String(suspicious)));
        var completeResponse = await driverClient.PostAsync(
            $"/api/v1/driver/uploads/sessions/{session.UploadSessionId}/complete",
            null);

        Assert.True(
            completeResponse.StatusCode == HttpStatusCode.UnprocessableEntity,
            $"Expected quarantine response, received {(int)completeResponse.StatusCode}: {await completeResponse.Content.ReadAsStringAsync()}");
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        Assert.DoesNotContain(dbContext.MediaAssets, x => x.FileName == "proof.jpg");
        Assert.Contains(dbContext.AuditLogs, x => x.ActionType == "media.upload_quarantined");
    }

    [Fact]
    public async Task DeliveryProofIsVisibleToOperatorAndHiddenCrossTenant()
    {
        await ResetDatabaseAsync();

        var mission = await CreateAssignedMissionAsync();
        using var driverClient = await CreateDriverClientAsync();
        var asset = await UploadDemoAssetAsync(driverClient);

        var proofResponse = await driverClient.PostAsJsonAsync(
            $"/api/v1/driver/missions/{mission.Id}/stops/{mission.Stops[0].Id}/proof",
            new SubmitDeliveryProofRequest(
                "proof-001",
                "Taylor Receiver",
                "Taylor Receiver",
                DateTimeOffset.UtcNow,
                "Left at reception",
                [
                    new DeliveryProofPhotoRequest(asset.AssetId, "Delivery photo"),
                    new DeliveryProofPhotoRequest(asset.AssetId, "Recipient signature")
                ]));
        Assert.Equal(HttpStatusCode.OK, proofResponse.StatusCode);

        using var operatorClient = factory.CreateClient();
        var operatorLogin = await operatorClient.LoginAsync("operator@northwind.local", "Operator123!");
        operatorClient.SetBearer(operatorLogin.AccessToken);

        var missionDetail = await operatorClient.GetFromJsonAsync<MissionDetailResponse>($"/api/v1/dispatch/missions/{mission.Id}");
        Assert.NotNull(missionDetail);
        Assert.Single(missionDetail!.DeliveryProofs);
        Assert.Equal("Taylor Receiver", missionDetail.DeliveryProofs[0].RecipientName);

        using var southClient = factory.CreateClient();
        var southLogin = await southClient.LoginAsync("admin@southridge.local", "Admin123!");
        southClient.SetBearer(southLogin.AccessToken);
        var crossTenantResponse = await southClient.GetAsync($"/api/v1/dispatch/missions/{mission.Id}");

        Assert.Equal(HttpStatusCode.NotFound, crossTenantResponse.StatusCode);
    }

    private async Task<HttpClient> CreateDriverClientAsync()
    {
        var client = factory.CreateClient();
        var login = await client.LoginAsync("driver@northwind.local", "Driver123!");
        client.SetBearer(login.AccessToken);
        return client;
    }

    private static async Task<MediaAssetResponse> UploadDemoAssetAsync(HttpClient driverClient)
    {
        var createResponse = await driverClient.PostAsJsonAsync(
            "/api/v1/driver/uploads/sessions",
            new UploadSessionRequest("proof.jpg", "image/jpeg", 10, MediaUploadPurpose.DeliveryProofPhoto));
        var session = await createResponse.Content.ReadFromJsonAsync<UploadSessionResponse>();
        Assert.NotNull(session);

        var image = DemoJpegBytes();
        await driverClient.PostAsJsonAsync(
            $"/api/v1/driver/uploads/sessions/{session!.UploadSessionId}/chunks",
            new AppendUploadChunkRequest(0, Convert.ToBase64String(image[..5])));
        await driverClient.PostAsJsonAsync(
            $"/api/v1/driver/uploads/sessions/{session.UploadSessionId}/chunks",
            new AppendUploadChunkRequest(5, Convert.ToBase64String(image[5..])));
        var completeResponse = await driverClient.PostAsync(
            $"/api/v1/driver/uploads/sessions/{session.UploadSessionId}/complete",
            null);
        var asset = await completeResponse.Content.ReadFromJsonAsync<MediaAssetResponse>();
        Assert.NotNull(asset);
        return asset!;
    }

    private static byte[] DemoJpegBytes() => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];

    private async Task<MissionDetailResponse> CreateAssignedMissionAsync()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("operator@northwind.local", "Operator123!");
        client.SetBearer(login.AccessToken);

        var start = DateTimeOffset.UtcNow.AddHours(2);
        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/dispatch/missions",
            new CreateMissionRequest(
                "NW-M-OPS-1",
                "Inspection and proof route",
                start,
                start.AddHours(2),
                [
                    new MissionStopRequest(1, "Depot", "1 Dispatch Way", start.AddMinutes(20)),
                    new MissionStopRequest(2, "Customer", "22 Fleet Street", start.AddMinutes(90))
                ]));
        var created = await createResponse.Content.ReadFromJsonAsync<MissionDetailResponse>();
        Assert.NotNull(created);

        var plannedResponse = await client.PostAsJsonAsync(
            $"/api/v1/dispatch/missions/{created!.Id}/status",
            new TransitionMissionStatusRequest(MissionStatus.Planned, created.RowVersion));
        var planned = await plannedResponse.Content.ReadFromJsonAsync<MissionDetailResponse>();
        Assert.NotNull(planned);

        var drivers = await client.GetFromJsonAsync<List<DriverCandidate>>("/api/v1/fleet/drivers");
        var vehicles = await client.GetFromJsonAsync<List<VehicleCandidate>>("/api/v1/fleet/vehicles");
        var driver = Assert.Single(drivers!, x => x.LicenseNumber == "NW-DL-001");
        var vehicle = Assert.Single(vehicles!, x => x.RegistrationNumber == "NW-100");

        var assignmentResponse = await client.PutAsJsonAsync(
            $"/api/v1/dispatch/missions/{planned!.Id}/assignment",
            new SetMissionAssignmentRequest(driver.Id, vehicle.Id, planned.RowVersion));
        var assigned = await assignmentResponse.Content.ReadFromJsonAsync<MissionDetailResponse>();
        Assert.NotNull(assigned);

        var assignedStatusResponse = await client.PostAsJsonAsync(
            $"/api/v1/dispatch/missions/{assigned!.Id}/status",
            new TransitionMissionStatusRequest(MissionStatus.Assigned, assigned.RowVersion));
        var finalAssigned = await assignedStatusResponse.Content.ReadFromJsonAsync<MissionDetailResponse>();
        Assert.NotNull(finalAssigned);
        return finalAssigned!;
    }

    private async Task ResetDatabaseAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
        await FleetOpsSeedData.EnsureSeededAsync(dbContext, roleManager, userManager, new BootstrapOptions { SeedDemoData = true }, CancellationToken.None);
    }

    private sealed record DriverCandidate(Guid Id, string FullName, string LicenseNumber, string? PhoneNumber, bool IsActive, long RowVersion);
    private sealed record VehicleCandidate(Guid Id, string RegistrationNumber, string DisplayName, bool IsActive, long RowVersion);
}
