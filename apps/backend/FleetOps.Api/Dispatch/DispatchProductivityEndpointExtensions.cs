using FleetOps.Api.Auditing;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Compliance;
using FleetOps.Core.Modules.Dispatch;
using FleetOps.Core.Modules.Identity;
using FleetOps.Core.Modules.Maintenance;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.Dispatch;

public static class DispatchProductivityEndpointExtensions
{
    private const string Roles = SystemRoles.Admin + "," + SystemRoles.Operator;
    public static IEndpointRouteBuilder MapDispatchProductivityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/dispatch").RequireAuthorization(new AuthorizeAttribute { Roles = Roles });
        group.MapGet("/board", BoardAsync);
        group.MapGet("/templates", ListTemplatesAsync);
        group.MapPost("/templates", CreateTemplateAsync);
        group.MapPost("/templates/{id:guid}/duplicate", DuplicateTemplateAsync);
        group.MapPost("/imports/preview", PreviewImportAsync);
        group.MapPost("/imports/confirm", ConfirmImportAsync);
        group.MapGet("/suggestions", SuggestAsync);
        group.MapGet("/saved-views", ListViewsAsync);
        group.MapPost("/saved-views", SaveViewAsync);
        group.MapPost("/bulk/assignments", BulkAssignAsync);
        return app;
    }

    private static async Task<IResult> BoardAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtc, int page = 1, int pageSize = 50, HttpContext context = null!, FleetOpsDbContext db = null!, ICurrentTenantAccessor tenants = null!, CancellationToken ct = default)
    {
        var tenant = tenants.GetRequiredTenant(context.User); page = Math.Max(1, page); pageSize = Math.Clamp(pageSize == 0 ? 50 : pageSize, 1, 100);
        var query = db.Missions.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId);
        if (fromUtc is not null) query = query.Where(x => x.ScheduledEndUtc >= fromUtc.Value.ToUniversalTime());
        if (toUtc is not null) query = query.Where(x => x.ScheduledStartUtc <= toUtc.Value.ToUniversalTime());
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.ScheduledStartUtc).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new MissionSummaryResponse(x.Id, x.Reference, x.Title, x.Status, x.ScheduledStartUtc, x.ScheduledEndUtc, x.DriverId, null, x.VehicleId, null, x.Stops.Count, x.SimulatedDelayMinutes, x.RowVersion, null, null)).ToListAsync(ct);
        var drivers = await db.Drivers.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId).ToDictionaryAsync(x => x.Id, x => x.FullName, ct); var vehicles = await db.Vehicles.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId).ToDictionaryAsync(x => x.Id, x => x.RegistrationNumber, ct);
        return Results.Ok(new DispatchBoardResponse(total, items.Select(x => x with { DriverName = x.DriverId is Guid d ? drivers.GetValueOrDefault(d) : null, VehicleRegistrationNumber = x.VehicleId is Guid v ? vehicles.GetValueOrDefault(v) : null }).ToList()));
    }
    private static async Task<IResult> ListTemplatesAsync(HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, CancellationToken ct) { var t = tenants.GetRequiredTenant(context.User); var templates = await db.MissionTemplates.Include(x => x.Stops).Where(x => x.OrganizationId == t.OrganizationId).OrderBy(x => x.Name).ToListAsync(ct); return Results.Ok(templates.Select(Map)); }
    private static async Task<IResult> CreateTemplateAsync(CreateMissionTemplateRequest request, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, IAuditService audit, CancellationToken ct)
    { var t = tenants.GetRequiredTenant(context.User); MissionTemplate template; try { template = new MissionTemplate(t.OrganizationId, request.Name, request.Title, request.Stops.Select(x => (x.Sequence, x.Name, x.Address, x.ArrivalOffsetMinutes))); } catch (ArgumentException ex) { return Validation("template", ex.Message); } db.MissionTemplates.Add(template); db.MissionTemplateStops.AddRange(template.Stops); try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Validation("name", "Template name already exists in this organization."); } await audit.WriteAsync(t.OrganizationId, t.UserId, "dispatch.template_created", "mission-template", template.Id.ToString(), new { template.Name }, ct); return Results.Created($"/api/v1/dispatch/templates/{template.Id}", Map(template)); }
    private static async Task<IResult> DuplicateTemplateAsync(Guid id, DuplicateTemplateRequest request, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, IAuditService audit, CancellationToken ct)
    { var t = tenants.GetRequiredTenant(context.User); var template = await db.MissionTemplates.Include(x => x.Stops).FirstOrDefaultAsync(x => x.OrganizationId == t.OrganizationId && x.Id == id, ct); if (template is null) return Results.NotFound(); if (await db.Missions.AnyAsync(x => x.OrganizationId == t.OrganizationId && x.Reference == request.Reference.Trim(), ct)) return Validation("reference", "Mission reference already exists in this organization."); Mission mission; try { mission = new Mission(t.OrganizationId, request.Reference, template.Title, request.ScheduledStartUtc, request.ScheduledEndUtc); mission.ReplaceStops(template.Stops.OrderBy(x => x.Sequence).Select(x => new MissionStop(t.OrganizationId, mission.Id, x.Sequence, x.Name, x.Address, request.ScheduledStartUtc.AddMinutes(x.ArrivalOffsetMinutes)))); } catch (ArgumentException ex) { return Validation("mission", ex.Message); } db.Missions.Add(mission); db.MissionStops.AddRange(mission.Stops); db.MissionTimelineEvents.AddRange(mission.Timeline); await db.SaveChangesAsync(ct); await audit.WriteAsync(t.OrganizationId, t.UserId, "dispatch.mission_duplicated", "mission", mission.Id.ToString(), new { template.Id, mission.Reference }, ct); return Results.Created($"/api/v1/dispatch/missions/{mission.Id}", new { mission.Id, mission.Reference }); }
    private static async Task<IResult> PreviewImportAsync(DispatchImportPreviewRequest request, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, CancellationToken ct)
    {
        var tenant = tenants.GetRequiredTenant(context.User);
        var errors = GetImportErrors(request);
        if (errors.Count > 0) return Results.Ok(new DispatchImportPreviewResponse(request.ImportKey.Trim(), 0, errors.Count, errors, false));
        var importKey = request.ImportKey.Trim();
        var alreadyImported = await db.DispatchImportReceipts.AnyAsync(x => x.OrganizationId == tenant.OrganizationId && x.ImportKey == importKey, ct);
        return Results.Ok(new DispatchImportPreviewResponse(importKey, request.Rows.Count, 0, [], alreadyImported));
    }

    private static async Task<IResult> ConfirmImportAsync(DispatchImportPreviewRequest request, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, IAuditService audit, TimeProvider timeProvider, CancellationToken ct)
    {
        var tenant = tenants.GetRequiredTenant(context.User);
        var importKey = request.ImportKey.Trim();
        if (GetImportErrors(request).Count > 0) return Validation("import", "Fix import preview errors before confirmation.");
        if (await db.DispatchImportReceipts.AnyAsync(x => x.OrganizationId == tenant.OrganizationId && x.ImportKey == importKey, ct)) return Results.Ok(new { wasDuplicate = true, created = 0 });
        var grouped = request.Rows.GroupBy(x => x.Reference.Trim(), StringComparer.OrdinalIgnoreCase);
        var existing = await db.Missions.Where(x => x.OrganizationId == tenant.OrganizationId).Select(x => x.Reference).ToListAsync(ct);
        if (grouped.Any(x => existing.Contains(x.Key, StringComparer.OrdinalIgnoreCase))) return Validation("reference", "An imported mission reference already exists in this organization.");
        var created = 0;
        foreach (var group in grouped)
        {
            var first = group.First();
            var mission = new Mission(tenant.OrganizationId, first.Reference, first.Title, first.ScheduledStartUtc, first.ScheduledEndUtc);
            mission.ReplaceStops(group.OrderBy(x => x.PlannedArrivalUtc).Select((x, index) => new MissionStop(tenant.OrganizationId, mission.Id, index + 1, x.StopName, x.StopAddress, x.PlannedArrivalUtc)));
            db.Missions.Add(mission); db.MissionStops.AddRange(mission.Stops); db.MissionTimelineEvents.AddRange(mission.Timeline); created++;
        }
        db.DispatchImportReceipts.Add(new DispatchImportReceipt(tenant.OrganizationId, importKey, timeProvider.GetUtcNow()));
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(tenant.OrganizationId, tenant.UserId, "dispatch.import_confirmed", "dispatch-import", importKey, new { created }, ct);
        return Results.Ok(new { wasDuplicate = false, created });
    }
    private static async Task<IResult> SuggestAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, CancellationToken ct) { var t = tenants.GetRequiredTenant(context.User); if (endUtc <= startUtc) return Validation("window", "End must be after start."); var busy = await db.Missions.AsNoTracking().Where(x => x.OrganizationId == t.OrganizationId && x.ScheduledStartUtc < endUtc && startUtc < x.ScheduledEndUtc && x.Status != MissionStatus.Completed && x.Status != MissionStatus.Cancelled).ToListAsync(ct); var driver = await db.Drivers.AsNoTracking().Where(x => x.OrganizationId == t.OrganizationId && x.IsActive && !busy.Select(m => m.DriverId).Contains(x.Id)).OrderBy(x => x.FullName).FirstOrDefaultAsync(ct); var vehicle = await db.Vehicles.AsNoTracking().Where(x => x.OrganizationId == t.OrganizationId && x.IsActive && !busy.Select(m => m.VehicleId).Contains(x.Id)).OrderBy(x => x.RegistrationNumber).FirstOrDefaultAsync(ct); return Results.Ok(new ResourceSuggestionResponse(driver?.Id, driver?.FullName, vehicle?.Id, vehicle?.RegistrationNumber, "First active driver and vehicle with no overlapping non-final mission, ordered by name and registration.")); }
    private static async Task<IResult> ListViewsAsync(HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, CancellationToken ct) { var t = tenants.GetRequiredTenant(context.User); return Results.Ok(await db.DispatchSavedViews.AsNoTracking().Where(x => x.OrganizationId == t.OrganizationId && x.UserId == t.UserId).OrderBy(x => x.Name).Select(x => new DispatchSavedViewResponse(x.Id, x.Name, x.FilterJson)).ToListAsync(ct)); }
    private static async Task<IResult> SaveViewAsync(SaveDispatchViewRequest request, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, CancellationToken ct) { var t = tenants.GetRequiredTenant(context.User); DispatchSavedView view; try { view = new DispatchSavedView(t.OrganizationId, t.UserId, request.Name, request.FilterJson); } catch (ArgumentException ex) { return Validation("view", ex.Message); } db.DispatchSavedViews.Add(view); try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Validation("name", "Saved view name already exists."); } return Results.Ok(new DispatchSavedViewResponse(view.Id, view.Name, view.FilterJson)); }
    private static async Task<IResult> BulkAssignAsync(BulkDispatchAssignmentRequest request, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, IAuditService audit, TimeProvider timeProvider, CancellationToken ct) { var t = tenants.GetRequiredTenant(context.User); if (request.Items.Count is 0 or > 100) return Validation("items", "Select between 1 and 100 missions."); var conflicts = new List<string>(); var loaded = new List<(Mission Mission, BulkDispatchAssignmentItem Item)>(); var policy = await db.CompliancePolicies.AsNoTracking().FirstOrDefaultAsync(x => x.OrganizationId == t.OrganizationId, ct); var blockingTypes = policy?.BlocksAssignments == true ? await db.ComplianceDocumentTypes.AsNoTracking().Where(x => x.OrganizationId == t.OrganizationId && x.IsActive && x.IsBlocking).ToListAsync(ct) : []; foreach (var item in request.Items) { var mission = await db.Missions.Include(x => x.Timeline).FirstOrDefaultAsync(x => x.OrganizationId == t.OrganizationId && x.Id == item.MissionId, ct); var driverOk = await db.Drivers.AnyAsync(x => x.OrganizationId == t.OrganizationId && x.Id == item.DriverId && x.IsActive, ct); var vehicleOk = await db.Vehicles.AnyAsync(x => x.OrganizationId == t.OrganizationId && x.Id == item.VehicleId && x.IsActive, ct); if (mission is null || mission.RowVersion != item.RowVersion || !driverOk || !vehicleOk) { conflicts.Add($"Mission {item.MissionId} changed or has unavailable resources."); continue; } var unavailable = await db.MaintenanceWorkOrders.AnyAsync(x => x.OrganizationId == t.OrganizationId && x.VehicleId == item.VehicleId && x.ImmobilizesVehicle && x.Status != MaintenanceWorkOrderStatus.Completed && x.Status != MaintenanceWorkOrderStatus.Cancelled, ct); if (unavailable) { conflicts.Add($"Mission {mission.Reference} uses an immobilized vehicle."); continue; } var nonCompliant = blockingTypes.Any(type => !db.ComplianceDocuments.Any(document => document.OrganizationId == t.OrganizationId && document.TargetEntityId == (type.SubjectType == ComplianceSubjectType.Vehicle ? item.VehicleId : item.DriverId) && (document.ComplianceDocumentTypeId == type.Id || document.DocumentType == type.Name) && document.ReplacedByDocumentId == null && document.ReviewStatus == ComplianceReviewStatus.Approved && document.ExpiresAtUtc > timeProvider.GetUtcNow())); if (nonCompliant && (string.IsNullOrWhiteSpace(item.ComplianceOverrideReason) || !context.User.IsInRole(SystemRoles.Admin))) { conflicts.Add($"Mission {mission.Reference} is blocked by compliance policy."); continue; } var overlap = await db.Missions.AnyAsync(x => x.OrganizationId == t.OrganizationId && x.Id != mission.Id && x.Status != MissionStatus.Completed && x.Status != MissionStatus.Cancelled && (x.DriverId == item.DriverId || x.VehicleId == item.VehicleId) && x.ScheduledStartUtc < mission.ScheduledEndUtc && mission.ScheduledStartUtc < x.ScheduledEndUtc, ct); if (overlap) { conflicts.Add($"Mission {mission.Reference} conflicts with an existing assignment."); continue; } loaded.Add((mission, item)); } foreach (var candidate in loaded) if (loaded.Any(other => other.Mission.Id != candidate.Mission.Id && (other.Item.DriverId == candidate.Item.DriverId || other.Item.VehicleId == candidate.Item.VehicleId) && other.Mission.ScheduledStartUtc < candidate.Mission.ScheduledEndUtc && candidate.Mission.ScheduledStartUtc < other.Mission.ScheduledEndUtc)) conflicts.Add($"Mission {candidate.Mission.Reference} conflicts with another selected assignment."); if (!request.Confirm || conflicts.Count > 0) return Results.Ok(new BulkDispatchResult(0, conflicts)); foreach (var (mission, item) in loaded) { mission.SetAssignment(item.DriverId, item.VehicleId); db.MissionTimelineEvents.Add(mission.Timeline.OrderByDescending(x => x.OccurredAtUtc).First()); await audit.WriteAsync(t.OrganizationId, t.UserId, "dispatch.bulk_assignment", "mission", mission.Id.ToString(), new { item.DriverId, item.VehicleId, item.ComplianceOverrideReason }, ct); } await db.SaveChangesAsync(ct); return Results.Ok(new BulkDispatchResult(loaded.Count, [])); }
    private static List<string> GetImportErrors(DispatchImportPreviewRequest request) { var errors = new List<string>(); if (string.IsNullOrWhiteSpace(request.ImportKey) || request.Rows.Count == 0) { errors.Add("An import key and at least one row are required."); return errors; } foreach (var (row, index) in request.Rows.Select((row, index) => (row, index))) if (string.IsNullOrWhiteSpace(row.Reference) || string.IsNullOrWhiteSpace(row.Title) || string.IsNullOrWhiteSpace(row.StopName) || string.IsNullOrWhiteSpace(row.StopAddress) || row.ScheduledEndUtc <= row.ScheduledStartUtc || row.PlannedArrivalUtc < row.ScheduledStartUtc || row.PlannedArrivalUtc > row.ScheduledEndUtc) errors.Add($"Row {index + 1} is invalid."); foreach (var group in request.Rows.GroupBy(x => x.Reference.Trim(), StringComparer.OrdinalIgnoreCase)) if (group.Select(x => (x.Title, x.ScheduledStartUtc, x.ScheduledEndUtc)).Distinct().Skip(1).Any()) errors.Add($"Reference {group.Key} has inconsistent mission details."); return errors; }
    private static MissionTemplateResponse Map(MissionTemplate x) => new(x.Id, x.Name, x.Title, x.RowVersion, x.Stops.OrderBy(s => s.Sequence).Select(s => new MissionTemplateStopRequest(s.Sequence, s.Name, s.Address, s.ArrivalOffsetMinutes)).ToList());
    private static IResult Validation(string key, string message) => Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}
