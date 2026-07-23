using System.Security.Cryptography;
using System.Text;
using FleetOps.Api.Auditing;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Dispatch;
using FleetOps.Core.Modules.Identity;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.RecipientStatus;

public static class RecipientStatusEndpointExtensions
{
    private const string DispatcherRoles = SystemRoles.Admin + "," + SystemRoles.Operator;

    public static IEndpointRouteBuilder MapRecipientStatusEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/v1/dispatch/missions/{missionId:guid}/recipient-status")
            .RequireAuthorization(new AuthorizeAttribute { Roles = DispatcherRoles });
        admin.MapPost("/links", CreateAsync);
        admin.MapDelete("/links/{linkId:guid}", RevokeAsync);

        app.MapGet("/public/v1/recipient-status/{token}", ReadAsync)
            .AllowAnonymous()
            .RequireRateLimiting("recipient-status");
        return app;
    }

    private static async Task<IResult> CreateAsync(Guid missionId, CreateRecipientStatusLinkRequest request, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenantAccessor, IAuditService audit, CancellationToken cancellationToken)
    {
        var tenant = tenantAccessor.GetRequiredTenant(context.User);
        if (request.ExpiresAtUtc <= DateTimeOffset.UtcNow || request.ExpiresAtUtc > DateTimeOffset.UtcNow.AddDays(30))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["expiresAtUtc"] = ["Expiry must be within the next 30 days."] });
        var missionExists = await db.Missions.AnyAsync(x => x.Id == missionId && x.OrganizationId == tenant.OrganizationId, cancellationToken);
        if (!missionExists) return Results.NotFound();

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var link = new RecipientStatusLink(tenant.OrganizationId, missionId, Hash(token), request.ExpiresAtUtc);
        db.RecipientStatusLinks.Add(link);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(tenant.OrganizationId, tenant.UserId, "recipient_status.link_created", "mission", missionId.ToString(), new { link.Id, link.ExpiresAtUtc }, cancellationToken);
        return Results.Created($"/api/v1/dispatch/missions/{missionId}/recipient-status/links/{link.Id}", new RecipientStatusLinkResponse(link.Id, $"/public/recipient-status/{token}", link.ExpiresAtUtc));
    }

    private static async Task<IResult> RevokeAsync(Guid missionId, Guid linkId, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenantAccessor, IAuditService audit, CancellationToken cancellationToken)
    {
        var tenant = tenantAccessor.GetRequiredTenant(context.User);
        var link = await db.RecipientStatusLinks.SingleOrDefaultAsync(x => x.Id == linkId && x.MissionId == missionId && x.OrganizationId == tenant.OrganizationId, cancellationToken);
        if (link is null) return Results.NotFound();
        link.Revoke(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(tenant.OrganizationId, tenant.UserId, "recipient_status.link_revoked", "mission", missionId.ToString(), new { link.Id }, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ReadAsync(string token, HttpContext context, FleetOpsDbContext db, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store, max-age=0";
        context.Response.Headers.Pragma = "no-cache";
        if (token.Length != 64 || !token.All(Uri.IsHexDigit)) return Results.NotFound();
        var now = DateTimeOffset.UtcNow;
        var link = await db.RecipientStatusLinks.SingleOrDefaultAsync(x => x.TokenHash == Hash(token), cancellationToken);
        if (link is null || !link.IsAvailableAt(now)) return Results.NotFound();
        var mission = await db.Missions.SingleOrDefaultAsync(x => x.Id == link.MissionId && x.OrganizationId == link.OrganizationId, cancellationToken);
        if (mission is null || mission.Status is MissionStatus.Completed or MissionStatus.Cancelled) return Results.NotFound();
        link.RecordView(now);
        await db.SaveChangesAsync(cancellationToken);
        var delayedEnd = mission.ScheduledEndUtc.AddMinutes(mission.SimulatedDelayMinutes);
        var eta = $"Estimated between {delayedEnd.AddMinutes(-30):HH:mm} and {delayedEnd.AddMinutes(30):HH:mm} UTC";
        return Results.Ok(new PublicRecipientStatusResponse(mission.Status.ToString(), eta, mission.CreatedAtUtc, false, false));
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
