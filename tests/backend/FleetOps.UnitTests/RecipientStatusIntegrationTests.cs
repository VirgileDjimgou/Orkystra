using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Dispatch;
using FleetOps.Api.RecipientStatus;
using FleetOps.Core.Modules.Dispatch;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using FleetOps.UnitTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class RecipientStatusIntegrationTests(FleetOpsApiFactory factory) : IClassFixture<FleetOpsApiFactory>
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web);

    [Fact]
    public async Task PublicLinkIsMinimalNonCacheableAndImmediatelyRevocable()
    {
        await ResetDatabaseAsync();
        using var operatorClient = factory.CreateClient();
        var login = await operatorClient.LoginAsync("operator@northwind.local", "Operator123!");
        operatorClient.SetBearer(login.AccessToken);
        var mission = await CreateMissionAsync(operatorClient);

        var create = await operatorClient.PostAsJsonAsync(
            $"/api/v1/dispatch/missions/{mission.Id}/recipient-status/links",
            new CreateRecipientStatusLinkRequest(DateTimeOffset.UtcNow.AddHours(12)));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var link = await create.Content.ReadFromJsonAsync<RecipientStatusLinkResponse>();
        Assert.NotNull(link);

        using var anonymous = factory.CreateClient();
        var publicApiUrl = link!.Url.Replace("/public/recipient-status/", "/public/v1/recipient-status/", StringComparison.Ordinal);
        var publicResponse = await anonymous.GetAsync(publicApiUrl);
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        Assert.Equal("no-store, max-age=0", publicResponse.Headers.CacheControl!.ToString());
        var body = await publicResponse.Content.ReadAsStringAsync();
        var payload = System.Text.Json.JsonSerializer.Deserialize<PublicRecipientStatusResponse>(body, JsonOptions);
        Assert.NotNull(payload);
        Assert.DoesNotContain("Fleet Street", body, StringComparison.OrdinalIgnoreCase);
        Assert.False(payload!.TrackingAvailable);

        var revoke = await operatorClient.DeleteAsync($"/api/v1/dispatch/missions/{mission.Id}/recipient-status/links/{link.Id}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(publicApiUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/public/v1/recipient-status/" + new string('a', 64))).StatusCode);
    }

    private static async Task<MissionDetailResponse> CreateMissionAsync(HttpClient client)
    {
        var start = DateTimeOffset.UtcNow.AddHours(2);
        var response = await client.PostAsJsonAsync("/api/v1/dispatch/missions", new CreateMissionRequest(
            "NW-RECIPIENT", "Recipient status demo", start, start.AddHours(2),
            [new MissionStopRequest(1, "Customer", "22 Fleet Street", start.AddHours(1))]));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MissionDetailResponse>())!;
    }

    private async Task ResetDatabaseAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        await FleetOpsSeedData.EnsureSeededAsync(db, roles, users, new BootstrapOptions { SeedDemoData = true }, CancellationToken.None);
    }
}
