using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using FleetOps.Api.Auditing;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Dispatch;
using FleetOps.Core.Modules.Identity;
using FleetOps.Infrastructure.Persistence;
using FleetOps.Infrastructure.RecipientStatus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
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

        var preferences = app.MapGroup("/api/v1/recipient-status")
            .RequireAuthorization(new AuthorizeAttribute { Roles = SystemRoles.Admin });
        preferences.MapGet("/preferences", GetPreferencesAsync);
        preferences.MapPut("/preferences", UpdatePreferencesAsync);
        preferences.MapGet("/metrics", GetMetricsAsync);

        app.MapGet("/public/v1/recipient-status/{token}", ReadAsync)
            .AllowAnonymous()
            .RequireRateLimiting("recipient-status");
        app.MapPost("/public/v1/recipient-status/{token}/contact", CorrectContactAsync)
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

    private static async Task<IResult> GetPreferencesAsync(HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenantAccessor, CancellationToken cancellationToken)
    {
        var tenant = tenantAccessor.GetRequiredTenant(context.User);
        var preference = await db.RecipientNotificationPreferences.SingleOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken);
        return Results.Ok(ToResponse(preference));
    }

    private static async Task<IResult> UpdatePreferencesAsync(UpdateRecipientNotificationPreferenceRequest request, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenantAccessor, IAuditService audit, CancellationToken cancellationToken)
    {
        var tenant = tenantAccessor.GetRequiredTenant(context.User);
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(request.TimeZoneId, out _)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["timeZoneId"] = ["A valid server time-zone identifier is required."] });
        var preference = await db.RecipientNotificationPreferences.SingleOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken);
        try
        {
            if (preference is null) { preference = new RecipientNotificationPreference(tenant.OrganizationId); db.RecipientNotificationPreferences.Add(preference); }
            preference.Update(request.ConsentGranted, request.Channel, request.Language, request.TimeZoneId, request.QuietHoursStartHour, request.QuietHoursEndHour);
        }
        catch (ArgumentOutOfRangeException ex) { return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.ParamName ?? "body"] = [ex.Message] }); }
        catch (ArgumentException ex) { return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.ParamName ?? "body"] = [ex.Message] }); }
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(tenant.OrganizationId, tenant.UserId, "recipient_status.preferences_updated", "recipient-notification-preference", preference.Id.ToString(), new { preference.ConsentGranted, preference.Channel, preference.Language, preference.TimeZoneId, preference.QuietHoursStartHour, preference.QuietHoursEndHour }, cancellationToken);
        return Results.Ok(ToResponse(preference));
    }

    private static async Task<IResult> GetMetricsAsync(HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenantAccessor, CancellationToken cancellationToken)
    {
        var tenant = tenantAccessor.GetRequiredTenant(context.User); var now = DateTimeOffset.UtcNow;
        var activeLinks = await db.RecipientStatusLinks.CountAsync(x => x.OrganizationId == tenant.OrganizationId && x.RevokedAtUtc == null && x.ExpiresAtUtc > now, cancellationToken);
        var usefulViews = await db.RecipientStatusLinks.Where(x => x.OrganizationId == tenant.OrganizationId).SumAsync(x => (int?)x.ViewCount, cancellationToken) ?? 0;
        var delivered = await db.RecipientStatusNotifications.CountAsync(x => x.OrganizationId == tenant.OrganizationId && x.DeliveryStatus == RecipientStatusNotificationDeliveryStatus.Delivered, cancellationToken);
        var failed = await db.RecipientStatusNotifications.CountAsync(x => x.OrganizationId == tenant.OrganizationId && x.DeliveryStatus == RecipientStatusNotificationDeliveryStatus.DeadLetter, cancellationToken);
        return Results.Ok(new RecipientStatusMetricsResponse(activeLinks, usefulViews, delivered, failed));
    }

    private static async Task<IResult> ReadAsync(string token, HttpContext context, FleetOpsDbContext db, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store, max-age=0";
        context.Response.Headers.Pragma = "no-cache";
        if (token.Length != 64 || !token.All(Uri.IsHexDigit)) return Results.NotFound();
        var now = DateTimeOffset.UtcNow;
        var link = await db.RecipientStatusLinks.SingleOrDefaultAsync(x => x.TokenHash == Hash(token), cancellationToken);
        if (link is null || !link.IsAvailableAt(now)) return Results.NotFound();
        var mission = await db.Missions.Include(x => x.Timeline).SingleOrDefaultAsync(x => x.Id == link.MissionId && x.OrganizationId == link.OrganizationId, cancellationToken);
        if (mission is null || mission.Status is MissionStatus.Completed or MissionStatus.Cancelled) return Results.NotFound();
        link.RecordView(now);
        await db.SaveChangesAsync(cancellationToken);
        var delayedEnd = mission.ScheduledEndUtc.AddMinutes(mission.SimulatedDelayMinutes);
        var eta = $"Estimated between {delayedEnd.AddMinutes(-30):HH:mm} and {delayedEnd.AddMinutes(30):HH:mm} UTC";
        var lastUpdated = mission.Timeline.MaxBy(x => x.OccurredAtUtc)?.OccurredAtUtc ?? mission.CreatedAtUtc;
        return Results.Ok(new PublicRecipientStatusResponse(mission.Status.ToString(), eta, lastUpdated, false, false));
    }

    private static async Task<IResult> CorrectContactAsync(string token, CorrectRecipientContactRequest request, HttpContext context, FleetOpsDbContext db, IDataProtectionProvider dataProtectionProvider, IAuditService audit, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store, max-age=0";
        if (token.Length != 64 || !token.All(Uri.IsHexDigit) || !IsEmail(request.Email)) return Results.NotFound();
        var now = DateTimeOffset.UtcNow;
        var link = await db.RecipientStatusLinks.SingleOrDefaultAsync(x => x.TokenHash == Hash(token), cancellationToken);
        if (link is null || !link.IsAvailableAt(now)) return Results.NotFound();
        var missionIsPublic = await db.Missions.AnyAsync(x => x.Id == link.MissionId && x.OrganizationId == link.OrganizationId && x.Status != MissionStatus.Completed && x.Status != MissionStatus.Cancelled, cancellationToken);
        if (!missionIsPublic) return Results.NotFound();
        link.SetRecipientEmailProtected(dataProtectionProvider.CreateProtector("FleetOps.RecipientStatus.Email.v1").Protect(request.Email.Trim()));
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(link.OrganizationId, null, "recipient_status.contact_corrected", "recipient-status-link", link.Id.ToString(), new { source = "public-link" }, cancellationToken);
        return Results.NoContent();
    }

    private static RecipientNotificationPreferenceResponse ToResponse(RecipientNotificationPreference? preference) => preference is null
        ? new(false, RecipientNotificationChannel.Email, "en", "UTC", null, null)
        : new(preference.ConsentGranted, preference.Channel, preference.Language, preference.TimeZoneId, preference.QuietHoursStartHour, preference.QuietHoursEndHour);
    private static bool IsEmail(string? value) { try { var address = new MailAddress(value ?? string.Empty); return address.Address == value?.Trim(); } catch { return false; } }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
