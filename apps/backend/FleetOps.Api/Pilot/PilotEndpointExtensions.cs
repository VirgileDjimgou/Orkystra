using FleetOps.Api.Auditing;
using FleetOps.Api.Auth;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Dispatch;
using FleetOps.Core.Modules.Pilot;
using FleetOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.Pilot;

public static class PilotEndpointExtensions
{
    public static IEndpointRouteBuilder MapPilotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/pilot")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        group.MapGet("/metrics", GetMetricsAsync);
        group.MapPost("/metrics/collect", CollectMetricsAsync);
        group.MapPut("/consent", UpdateConsentAsync);
        group.MapGet("/incidents", ListIncidentsAsync);
        group.MapPost("/incidents", CreateIncidentAsync);
        group.MapPost("/incidents/{id:guid}/resolve", ResolveIncidentAsync);
        group.MapGet("/decisions", ListDecisionsAsync);
        group.MapPost("/decisions", RecordDecisionAsync);
        group.MapGet("/export", ExportAsync);

        return app;
    }

    private static async Task<IResult> GetMetricsAsync(
        HttpContext context,
        FleetOpsDbContext db,
        ICurrentTenantAccessor tenants,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var tenant = tenants.GetRequiredTenant(context.User);
        var enrollment = await db.PilotEnrollments
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId, ct);
        var openIncidents = await db.PilotSupportIncidents.CountAsync(
            x => x.OrganizationId == tenant.OrganizationId && x.Status == PilotIncidentStatus.Open,
            ct);

        if (enrollment is null || !enrollment.AnalyticsConsent)
        {
            return Results.Ok(new PilotMetricsResponse(false, null, openIncidents, timeProvider.GetUtcNow()));
        }

        var latest = await db.PilotDailyMetrics
            .AsNoTracking()
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderByDescending(x => x.CapturedOnUtc)
            .FirstOrDefaultAsync(ct);

        return Results.Ok(new PilotMetricsResponse(
            true,
            latest is null ? null : Map(latest),
            openIncidents,
            timeProvider.GetUtcNow()));
    }

    private static async Task<IResult> CollectMetricsAsync(
        HttpContext context,
        FleetOpsDbContext db,
        ICurrentTenantAccessor tenants,
        IAuditService audit,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var tenant = tenants.GetRequiredTenant(context.User);
        var enrollment = await db.PilotEnrollments.SingleOrDefaultAsync(
            x => x.OrganizationId == tenant.OrganizationId,
            ct);
        if (enrollment is null || !enrollment.AnalyticsConsent)
        {
            return Results.Problem(
                "Explicit pilot measurement consent is required before collecting aggregate metrics.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var now = timeProvider.GetUtcNow();
        var capturedOnUtc = DateOnly.FromDateTime(now.UtcDateTime);
        var dayStartUtc = new DateTimeOffset(capturedOnUtc.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEndUtc = dayStartUtc.AddDays(1);
        var activeDriverIds = await db.DriverSyncCommandReceipts
            .Where(x => x.OrganizationId == tenant.OrganizationId
                && x.ProcessedAtUtc >= dayStartUtc
                && x.ProcessedAtUtc < dayEndUtc)
            .Select(x => x.DriverId)
            .Distinct()
            .ToListAsync(ct);
        var returningDrivers = activeDriverIds.Count == 0
            ? 0
            : await db.DriverSyncCommandReceipts
                .Where(x => x.OrganizationId == tenant.OrganizationId
                    && activeDriverIds.Contains(x.DriverId)
                    && x.ProcessedAtUtc >= dayStartUtc.AddDays(-7)
                    && x.ProcessedAtUtc < dayStartUtc)
                .Select(x => x.DriverId)
                .Distinct()
                .CountAsync(ct);

        var activationEvents = await db.OnboardingActivationEvents.CountAsync(
            x => x.OrganizationId == tenant.OrganizationId
                && x.OccurredAtUtc >= dayStartUtc
                && x.OccurredAtUtc < dayEndUtc,
            ct);
        var processedSyncCommands = await db.DriverSyncCommandReceipts.CountAsync(
            x => x.OrganizationId == tenant.OrganizationId
                && x.ProcessedAtUtc >= dayStartUtc
                && x.ProcessedAtUtc < dayEndUtc,
            ct);
        var completedMissions = await db.Missions.CountAsync(
            x => x.OrganizationId == tenant.OrganizationId && x.Status == MissionStatus.Completed,
            ct);
        var completeProofs = await db.DeliveryProofPhotos
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .Select(x => x.DeliveryProofId)
            .Distinct()
            .CountAsync(ct);
        var openExceptions = await db.OperationsExceptionStates.CountAsync(
            x => x.OrganizationId == tenant.OrganizationId && x.ResolvedAtUtc == null,
            ct);

        var metric = await db.PilotDailyMetrics.SingleOrDefaultAsync(
            x => x.OrganizationId == tenant.OrganizationId && x.CapturedOnUtc == capturedOnUtc,
            ct);
        if (metric is null)
        {
            metric = new PilotDailyMetric(
                tenant.OrganizationId,
                capturedOnUtc,
                activationEvents,
                activeDriverIds.Count,
                returningDrivers,
                processedSyncCommands,
                completedMissions,
                completeProofs,
                openExceptions,
                now);
            db.PilotDailyMetrics.Add(metric);
        }
        else
        {
            metric.Refresh(
                activationEvents,
                activeDriverIds.Count,
                returningDrivers,
                processedSyncCommands,
                completedMissions,
                completeProofs,
                openExceptions,
                now);
        }

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "pilot.daily_metrics_collected",
            "pilot-daily-metric",
            metric.Id.ToString(),
            new { metric.CapturedOnUtc },
            ct);
        return Results.Ok(Map(metric));
    }

    private static async Task<IResult> UpdateConsentAsync(
        UpdatePilotConsentRequest request,
        HttpContext context,
        FleetOpsDbContext db,
        ICurrentTenantAccessor tenants,
        IAuditService audit,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var tenant = tenants.GetRequiredTenant(context.User);
        var enrollment = await db.PilotEnrollments.SingleOrDefaultAsync(
            x => x.OrganizationId == tenant.OrganizationId,
            ct);
        if (enrollment is null)
        {
            enrollment = new PilotEnrollment(
                tenant.OrganizationId,
                request.AnalyticsConsent,
                timeProvider.GetUtcNow());
            db.PilotEnrollments.Add(enrollment);
        }
        else
        {
            enrollment.SetConsent(request.AnalyticsConsent, timeProvider.GetUtcNow());
        }

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "pilot.analytics_consent_updated",
            "pilot-enrollment",
            enrollment.Id.ToString(),
            new { request.AnalyticsConsent },
            ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ListIncidentsAsync(
        HttpContext context,
        FleetOpsDbContext db,
        ICurrentTenantAccessor tenants,
        CancellationToken ct)
    {
        var tenant = tenants.GetRequiredTenant(context.User);
        var incidents = await db.PilotSupportIncidents
            .AsNoTracking()
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Select(x => new PilotIncidentResponse(
                x.Id,
                x.Severity,
                x.Status,
                x.Category,
                x.Summary,
                x.Workaround,
                x.OccurredAtUtc,
                x.ResolvedAtUtc))
            .ToListAsync(ct);
        return Results.Ok(incidents);
    }

    private static async Task<IResult> CreateIncidentAsync(
        CreatePilotIncidentRequest request,
        HttpContext context,
        FleetOpsDbContext db,
        ICurrentTenantAccessor tenants,
        IAuditService audit,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var tenant = tenants.GetRequiredTenant(context.User);
        try
        {
            var incident = new PilotSupportIncident(
                tenant.OrganizationId,
                request.Severity,
                request.Category,
                request.Summary,
                request.Workaround,
                timeProvider.GetUtcNow());
            db.PilotSupportIncidents.Add(incident);
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync(
                tenant.OrganizationId,
                tenant.UserId,
                "pilot.incident_recorded",
                "pilot-incident",
                incident.Id.ToString(),
                new { request.Severity, incident.Category },
                ct);
            return Results.Created($"/api/v1/pilot/incidents/{incident.Id}", Map(incident));
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["incident"] = [ex.Message] });
        }
    }

    private static async Task<IResult> ResolveIncidentAsync(
        Guid id,
        ResolvePilotIncidentRequest request,
        HttpContext context,
        FleetOpsDbContext db,
        ICurrentTenantAccessor tenants,
        IAuditService audit,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var tenant = tenants.GetRequiredTenant(context.User);
        var incident = await db.PilotSupportIncidents.SingleOrDefaultAsync(
            x => x.OrganizationId == tenant.OrganizationId && x.Id == id,
            ct);
        if (incident is null)
        {
            return Results.NotFound();
        }

        incident.Resolve(request.Workaround, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "pilot.incident_resolved",
            "pilot-incident",
            incident.Id.ToString(),
            null,
            ct);
        return Results.Ok(Map(incident));
    }

    private static async Task<IResult> ListDecisionsAsync(
        HttpContext context,
        FleetOpsDbContext db,
        ICurrentTenantAccessor tenants,
        CancellationToken ct)
    {
        var tenant = tenants.GetRequiredTenant(context.User);
        var decisions = await db.PilotDecisions
            .AsNoTracking()
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderByDescending(x => x.DecidedAtUtc)
            .Select(x => new PilotDecisionResponse(
                x.Id,
                x.Outcome,
                x.PrimarySegment,
                x.Rationale,
                x.DecidedAtUtc))
            .ToListAsync(ct);
        return Results.Ok(decisions);
    }

    private static async Task<IResult> RecordDecisionAsync(
        RecordPilotDecisionRequest request,
        HttpContext context,
        FleetOpsDbContext db,
        ICurrentTenantAccessor tenants,
        IAuditService audit,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var tenant = tenants.GetRequiredTenant(context.User);
        try
        {
            var decision = new PilotDecision(
                tenant.OrganizationId,
                request.Outcome?.Trim().ToUpperInvariant() ?? string.Empty,
                request.PrimarySegment,
                request.Rationale,
                timeProvider.GetUtcNow());
            db.PilotDecisions.Add(decision);
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync(
                tenant.OrganizationId,
                tenant.UserId,
                "pilot.decision_recorded",
                "pilot-decision",
                decision.Id.ToString(),
                new { decision.Outcome, decision.PrimarySegment },
                ct);
            return Results.Created($"/api/v1/pilot/decisions/{decision.Id}", Map(decision));
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["decision"] = [ex.Message] });
        }
    }

    private static async Task<IResult> ExportAsync(
        HttpContext context,
        FleetOpsDbContext db,
        ICurrentTenantAccessor tenants,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var tenant = tenants.GetRequiredTenant(context.User);
        var enrollment = await db.PilotEnrollments
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId, ct);
        var metrics = enrollment?.AnalyticsConsent == true
            ? await db.PilotDailyMetrics
                .AsNoTracking()
                .Where(x => x.OrganizationId == tenant.OrganizationId)
                .OrderBy(x => x.CapturedOnUtc)
                .Select(x => new PilotDailyMetricResponse(
                    x.CapturedOnUtc,
                    x.ActivationEvents,
                    x.ActiveDrivers,
                    x.ReturningDrivers,
                    x.ProcessedSyncCommands,
                    x.CompletedMissions,
                    x.CompleteProofs,
                    x.OpenExceptions,
                    x.RefreshedAtUtc))
                .ToListAsync(ct)
            : [];
        var incidents = await db.PilotSupportIncidents
            .AsNoTracking()
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Select(x => new PilotIncidentResponse(
                x.Id,
                x.Severity,
                x.Status,
                x.Category,
                x.Summary,
                x.Workaround,
                x.OccurredAtUtc,
                x.ResolvedAtUtc))
            .ToListAsync(ct);
        var decisions = await db.PilotDecisions
            .AsNoTracking()
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderByDescending(x => x.DecidedAtUtc)
            .Select(x => new PilotDecisionResponse(
                x.Id,
                x.Outcome,
                x.PrimarySegment,
                x.Rationale,
                x.DecidedAtUtc))
            .ToListAsync(ct);

        return Results.Ok(new PilotEvidenceExportResponse(
            enrollment?.AnalyticsConsent ?? false,
            enrollment?.RecordedAtUtc,
            metrics,
            incidents,
            decisions,
            timeProvider.GetUtcNow()));
    }

    private static PilotDailyMetricResponse Map(PilotDailyMetric metric) => new(
        metric.CapturedOnUtc,
        metric.ActivationEvents,
        metric.ActiveDrivers,
        metric.ReturningDrivers,
        metric.ProcessedSyncCommands,
        metric.CompletedMissions,
        metric.CompleteProofs,
        metric.OpenExceptions,
        metric.RefreshedAtUtc);

    private static PilotIncidentResponse Map(PilotSupportIncident incident) => new(
        incident.Id,
        incident.Severity,
        incident.Status,
        incident.Category,
        incident.Summary,
        incident.Workaround,
        incident.OccurredAtUtc,
        incident.ResolvedAtUtc);

    private static PilotDecisionResponse Map(PilotDecision decision) => new(
        decision.Id,
        decision.Outcome,
        decision.PrimarySegment,
        decision.Rationale,
        decision.DecidedAtUtc);
}
