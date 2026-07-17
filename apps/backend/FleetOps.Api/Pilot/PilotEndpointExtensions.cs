using FleetOps.Api.Auditing;
using FleetOps.Api.Auth;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Dispatch;
using FleetOps.Core.Modules.Identity;
using FleetOps.Core.Modules.Pilot;
using FleetOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.Pilot;

public static class PilotEndpointExtensions
{
    public static IEndpointRouteBuilder MapPilotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/pilot").RequireAuthorization(AuthorizationPolicies.AdminOnly);
        group.MapGet("/metrics", GetMetricsAsync);
        group.MapPut("/consent", UpdateConsentAsync);
        group.MapGet("/incidents", ListIncidentsAsync);
        group.MapPost("/incidents", CreateIncidentAsync);
        group.MapPost("/incidents/{id:guid}/resolve", ResolveIncidentAsync);
        group.MapGet("/decisions", ListDecisionsAsync);
        group.MapPost("/decisions", RecordDecisionAsync);
        group.MapGet("/export", ExportAsync);
        return app;
    }

    private static async Task<IResult> GetMetricsAsync(HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, TimeProvider timeProvider, CancellationToken ct)
    {
        var tenant = tenants.GetRequiredTenant(context.User);
        var consent = await db.PilotEnrollments.Where(x => x.OrganizationId == tenant.OrganizationId).Select(x => (bool?)x.AnalyticsConsent).SingleOrDefaultAsync(ct) ?? false;
        if (!consent) return Results.Ok(new PilotMetricsResponse(false, 0, 0, 0, 0, 0, timeProvider.GetUtcNow()));
        var completed = await db.Missions.CountAsync(x => x.OrganizationId == tenant.OrganizationId && x.Status == MissionStatus.Completed, ct);
        var completeProofs = await db.DeliveryProofs.CountAsync(x => x.OrganizationId == tenant.OrganizationId, ct);
        var activeDrivers = await db.Drivers.CountAsync(x => x.OrganizationId == tenant.OrganizationId && x.IsActive, ct);
        var openExceptions = await db.OperationsExceptionStates.CountAsync(x => x.OrganizationId == tenant.OrganizationId && x.ResolvedAtUtc == null, ct);
        var openIncidents = await db.PilotSupportIncidents.CountAsync(x => x.OrganizationId == tenant.OrganizationId && x.Status == PilotIncidentStatus.Open, ct);
        return Results.Ok(new PilotMetricsResponse(true, activeDrivers, completed, completeProofs, openExceptions, openIncidents, timeProvider.GetUtcNow()));
    }

    private static async Task<IResult> UpdateConsentAsync(UpdatePilotConsentRequest request, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, IAuditService audit, TimeProvider timeProvider, CancellationToken ct)
    {
        var tenant = tenants.GetRequiredTenant(context.User); var enrollment = await db.PilotEnrollments.SingleOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId, ct);
        if (enrollment is null) { enrollment = new PilotEnrollment(tenant.OrganizationId, request.AnalyticsConsent, timeProvider.GetUtcNow()); db.PilotEnrollments.Add(enrollment); } else enrollment.SetConsent(request.AnalyticsConsent, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(ct); await audit.WriteAsync(tenant.OrganizationId, tenant.UserId, "pilot.analytics_consent_updated", "pilot-enrollment", enrollment.Id.ToString(), new { request.AnalyticsConsent }, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ListIncidentsAsync(HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, CancellationToken ct) { var t = tenants.GetRequiredTenant(context.User); return Results.Ok(await db.PilotSupportIncidents.AsNoTracking().Where(x => x.OrganizationId == t.OrganizationId).OrderByDescending(x => x.OccurredAtUtc).Select(x => new PilotIncidentResponse(x.Id, x.Severity, x.Status, x.Category, x.Summary, x.Workaround, x.OccurredAtUtc, x.ResolvedAtUtc)).ToListAsync(ct)); }
    private static async Task<IResult> CreateIncidentAsync(CreatePilotIncidentRequest request, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, IAuditService audit, TimeProvider timeProvider, CancellationToken ct) { var t = tenants.GetRequiredTenant(context.User); try { var incident = new PilotSupportIncident(t.OrganizationId, request.Severity, request.Category, request.Summary, request.Workaround, timeProvider.GetUtcNow()); db.PilotSupportIncidents.Add(incident); await db.SaveChangesAsync(ct); await audit.WriteAsync(t.OrganizationId, t.UserId, "pilot.incident_recorded", "pilot-incident", incident.Id.ToString(), new { request.Severity, incident.Category }, ct); return Results.Created($"/api/v1/pilot/incidents/{incident.Id}", Map(incident)); } catch (ArgumentException ex) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["incident"] = [ex.Message] }); } }
    private static async Task<IResult> ResolveIncidentAsync(Guid id, ResolvePilotIncidentRequest request, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, IAuditService audit, TimeProvider timeProvider, CancellationToken ct) { var t = tenants.GetRequiredTenant(context.User); var incident = await db.PilotSupportIncidents.SingleOrDefaultAsync(x => x.OrganizationId == t.OrganizationId && x.Id == id, ct); if (incident is null) return Results.NotFound(); incident.Resolve(request.Workaround, timeProvider.GetUtcNow()); await db.SaveChangesAsync(ct); await audit.WriteAsync(t.OrganizationId, t.UserId, "pilot.incident_resolved", "pilot-incident", incident.Id.ToString(), null, ct); return Results.Ok(Map(incident)); }
    private static async Task<IResult> ListDecisionsAsync(HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, CancellationToken ct) { var t = tenants.GetRequiredTenant(context.User); return Results.Ok(await db.PilotDecisions.AsNoTracking().Where(x => x.OrganizationId == t.OrganizationId).OrderByDescending(x => x.DecidedAtUtc).Select(x => new PilotDecisionResponse(x.Id, x.Outcome, x.PrimarySegment, x.Rationale, x.DecidedAtUtc)).ToListAsync(ct)); }
    private static async Task<IResult> RecordDecisionAsync(RecordPilotDecisionRequest request, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, IAuditService audit, TimeProvider timeProvider, CancellationToken ct) { var t = tenants.GetRequiredTenant(context.User); try { var decision = new PilotDecision(t.OrganizationId, request.Outcome.Trim().ToUpperInvariant(), request.PrimarySegment, request.Rationale, timeProvider.GetUtcNow()); db.PilotDecisions.Add(decision); await db.SaveChangesAsync(ct); await audit.WriteAsync(t.OrganizationId, t.UserId, "pilot.decision_recorded", "pilot-decision", decision.Id.ToString(), new { decision.Outcome, decision.PrimarySegment }, ct); return Results.Created($"/api/v1/pilot/decisions/{decision.Id}", new PilotDecisionResponse(decision.Id, decision.Outcome, decision.PrimarySegment, decision.Rationale, decision.DecidedAtUtc)); } catch (ArgumentException ex) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["decision"] = [ex.Message] }); } }
    private static async Task<IResult> ExportAsync(HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, TimeProvider timeProvider, CancellationToken ct) { var metrics = await GetMetricsAsync(context, db, tenants, timeProvider, ct); return metrics; }
    private static PilotIncidentResponse Map(PilotSupportIncident x) => new(x.Id, x.Severity, x.Status, x.Category, x.Summary, x.Workaround, x.OccurredAtUtc, x.ResolvedAtUtc);
}
