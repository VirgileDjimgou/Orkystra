using System.Globalization;
using System.Text;
using FleetOps.Api.Auditing;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Alerts;
using FleetOps.Core.Modules.Compliance;
using FleetOps.Core.Modules.Identity;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.Compliance;

public static class ComplianceEndpointExtensions
{
    private const string ReaderRoles = SystemRoles.Admin + "," + SystemRoles.Operator;
    public static IEndpointRouteBuilder MapComplianceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/compliance").RequireAuthorization(new AuthorizeAttribute { Roles = ReaderRoles });
        group.MapGet("/document-types", ListTypesAsync);
        group.MapPost("/document-types", SaveTypeAsync).RequireAuthorization(new AuthorizeAttribute { Roles = SystemRoles.Admin });
        group.MapGet("/policy", GetPolicyAsync);
        group.MapPut("/policy", UpdatePolicyAsync).RequireAuthorization(new AuthorizeAttribute { Roles = SystemRoles.Admin });
        group.MapPost("/documents", CreateDocumentAsync).RequireAuthorization(new AuthorizeAttribute { Roles = SystemRoles.Admin });
        group.MapPost("/documents/{id:guid}/review", ReviewDocumentAsync).RequireAuthorization(new AuthorizeAttribute { Roles = SystemRoles.Admin });
        group.MapGet("/matrix", MatrixAsync);
        group.MapGet("/audit-export", ExportAsync).RequireAuthorization(new AuthorizeAttribute { Roles = SystemRoles.Admin });
        group.MapGet("/campaigns", ListCampaignsAsync);
        group.MapPost("/campaigns", CreateCampaignAsync).RequireAuthorization(new AuthorizeAttribute { Roles = SystemRoles.Admin });
        group.MapPost("/campaigns/{id:guid}/activate", ActivateCampaignAsync).RequireAuthorization(new AuthorizeAttribute { Roles = SystemRoles.Admin });
        return app;
    }

    private static async Task<IResult> ListTypesAsync(HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenantAccessor, CancellationToken ct)
    {
        var tenant = tenantAccessor.GetRequiredTenant(context.User);
        return Results.Ok(await db.ComplianceDocumentTypes.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId).OrderBy(x => x.Name)
            .Select(x => new ComplianceDocumentTypeResponse(x.Id, x.Name, x.SubjectType, x.IsBlocking, x.RequiresReview, x.IsActive, x.RowVersion)).ToListAsync(ct));
    }
    private static async Task<IResult> SaveTypeAsync(SaveComplianceDocumentTypeRequest request, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, IAuditService audit, CancellationToken ct)
    {
        var tenant = tenants.GetRequiredTenant(context.User); ComplianceDocumentType type;
        try
        {
            var existing = await db.ComplianceDocumentTypes.FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Name == request.Name.Trim() && x.SubjectType == request.SubjectType, ct);
            if (existing is null)
            {
                type = new ComplianceDocumentType(tenant.OrganizationId, request.Name, request.SubjectType, request.IsBlocking, request.RequiresReview);
                db.ComplianceDocumentTypes.Add(type);
            }
            else
            {
                type = existing;
                if (request.RowVersion is not null && request.RowVersion != type.RowVersion) return Conflict("Document type was modified. Reload and retry.");
                type.Update(request.Name, request.IsBlocking, request.RequiresReview, request.IsActive);
            }
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is ArgumentException or DbUpdateException) { return Validation("documentType", ex.Message); }
        await audit.WriteAsync(tenant.OrganizationId, tenant.UserId, "compliance.document_type_saved", "compliance-document-type", type.Id.ToString(), new { type.Name, type.SubjectType, type.IsBlocking, type.RequiresReview }, ct);
        return Results.Ok(new ComplianceDocumentTypeResponse(type.Id, type.Name, type.SubjectType, type.IsBlocking, type.RequiresReview, type.IsActive, type.RowVersion));
    }
    private static async Task<IResult> GetPolicyAsync(HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, CancellationToken ct)
    {
        var tenant = tenants.GetRequiredTenant(context.User); var policy = await db.CompliancePolicies.AsNoTracking().FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId, ct);
        return Results.Ok(new CompliancePolicyResponse(policy?.BlocksAssignments ?? false, policy?.RowVersion ?? 0, "Compliance configuration is set by your organization and is not legal advice."));
    }
    private static async Task<IResult> UpdatePolicyAsync(UpdateCompliancePolicyRequest request, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, IAuditService audit, CancellationToken ct)
    {
        var tenant = tenants.GetRequiredTenant(context.User); var policy = await db.CompliancePolicies.FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId, ct);
        if (policy is null) { policy = new CompliancePolicy(tenant.OrganizationId, request.BlocksAssignments); db.CompliancePolicies.Add(policy); }
        else { if (request.RowVersion != policy.RowVersion) return Conflict("Compliance policy was modified. Reload and retry."); policy.SetBlocksAssignments(request.BlocksAssignments); }
        await db.SaveChangesAsync(ct); await audit.WriteAsync(tenant.OrganizationId, tenant.UserId, "compliance.policy_updated", "compliance-policy", policy.Id.ToString(), new { policy.BlocksAssignments }, ct);
        return Results.Ok(new CompliancePolicyResponse(policy.BlocksAssignments, policy.RowVersion, "Compliance configuration is set by your organization and is not legal advice."));
    }
    private static async Task<IResult> CreateDocumentAsync(CreateComplianceDocumentV2Request request, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, IAuditService audit, CancellationToken ct)
    {
        var tenant = tenants.GetRequiredTenant(context.User);
        var previous = request.ReplacesDocumentId is Guid previousId ? await db.ComplianceDocuments.FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == previousId, ct) : null;
        if (request.ReplacesDocumentId.HasValue && previous is null) return Results.NotFound();
        var type = request.DocumentTypeId is Guid typeId ? await db.ComplianceDocumentTypes.FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == typeId && x.IsActive, ct) : null;
        if (request.DocumentTypeId.HasValue && type is null) return Validation("documentTypeId", "Document type does not exist in this organization.");
        var subjectType = type?.SubjectType ?? request.SubjectType ?? (previous?.TargetType == ComplianceDocumentTargetType.Driver ? ComplianceSubjectType.Driver : ComplianceSubjectType.Vehicle);
        var targetId = previous?.TargetEntityId ?? request.TargetEntityId ?? Guid.Empty;
        if (targetId == Guid.Empty) return Validation("targetEntityId", "A vehicle or driver target is required.");
        var exists = subjectType == ComplianceSubjectType.Vehicle
            ? await db.Vehicles.AnyAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == targetId, ct)
            : await db.Drivers.AnyAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == targetId, ct);
        if (!exists) return Validation("targetEntityId", "Target does not exist in this organization.");
        if (request.MediaAssetId is Guid assetId && !await db.MediaAssets.AnyAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == assetId, ct)) return Validation("mediaAssetId", "Media asset does not exist in this organization.");
        var document = new ComplianceDocument(tenant.OrganizationId, subjectType == ComplianceSubjectType.Driver ? ComplianceDocumentTargetType.Driver : ComplianceDocumentTargetType.Vehicle, targetId, type?.Name ?? request.DocumentType, request.DocumentNumber, request.ExpiresAtUtc, request.Notes);
        document.Configure(type?.Id, request.MediaAssetId, type?.RequiresReview ?? false);
        if (previous is not null) previous.Replace(document.Id);
        db.ComplianceDocuments.Add(document); await db.SaveChangesAsync(ct);
        await audit.WriteAsync(tenant.OrganizationId, tenant.UserId, "compliance.document_uploaded", "compliance-document", document.Id.ToString(), new { document.DocumentType, document.TargetType, document.TargetEntityId, document.ReviewStatus }, ct);
        return Results.Created($"/api/v1/compliance/documents/{document.Id}", new { document.Id, document.ReviewStatus, document.RowVersion });
    }
    private static async Task<IResult> ReviewDocumentAsync(Guid id, ReviewComplianceDocumentRequest request, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, IAuditService audit, TimeProvider clock, CancellationToken ct)
    {
        var tenant = tenants.GetRequiredTenant(context.User); var document = await db.ComplianceDocuments.FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == id, ct); if (document is null) return Results.NotFound();
        if (document.RowVersion != request.RowVersion) return Conflict("Document was modified. Reload and retry.");
        try { document.Review(tenant.UserId, request.Approved, clock.GetUtcNow()); } catch (InvalidOperationException ex) { return Validation("state", ex.Message); }
        await db.SaveChangesAsync(ct); await audit.WriteAsync(tenant.OrganizationId, tenant.UserId, "compliance.document_reviewed", "compliance-document", document.Id.ToString(), new { request.Approved }, ct); return Results.Ok(new { document.Id, document.ReviewStatus, document.RowVersion });
    }
    private static async Task<IResult> MatrixAsync(HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, TimeProvider clock, CancellationToken ct)
    {
        var tenant = tenants.GetRequiredTenant(context.User); var now = clock.GetUtcNow(); var types = await db.ComplianceDocumentTypes.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId && x.IsActive).ToListAsync(ct);
        var docs = await db.ComplianceDocuments.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId && x.ReplacedByDocumentId == null).ToListAsync(ct);
        var vehicles = await db.Vehicles.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId).Select(x => new { x.Id, Label = x.RegistrationNumber }).ToListAsync(ct);
        var drivers = await db.Drivers.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId).Select(x => new { x.Id, Label = x.FullName }).ToListAsync(ct);
        var rows = new List<ComplianceMatrixRowResponse>();
        foreach (var type in types) foreach (var subject in type.SubjectType == ComplianceSubjectType.Vehicle ? vehicles.Select(x => (x.Id, x.Label)) : drivers.Select(x => (x.Id, x.Label))) { var d = docs.FirstOrDefault(x => x.TargetEntityId == subject.Id && (x.ComplianceDocumentTypeId == type.Id || x.DocumentType == type.Name)); var risk = d is null || d.ExpiresAtUtc <= now.AddDays(30) || d.ReviewStatus != ComplianceReviewStatus.Approved; rows.Add(new(type.SubjectType, subject.Id, subject.Label, type.Name, d?.Id, d?.ExpiresAtUtc, d?.ReviewStatus.ToString() ?? "Missing", type.IsBlocking, risk)); }
        return Results.Ok(rows);
    }
    private static async Task<IResult> ExportAsync(HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, CancellationToken ct)
    { var tenant = tenants.GetRequiredTenant(context.User); var docs = await db.ComplianceDocuments.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId).OrderBy(x => x.CreatedAtUtc).ToListAsync(ct); var csv = new StringBuilder("documentId,targetType,targetId,type,expiresAtUtc,status,replacedBy\n"); foreach (var d in docs) csv.AppendLine(CultureInfo.InvariantCulture, $"{d.Id},{d.TargetType},{d.TargetEntityId},\"{d.DocumentType.Replace("\"", "\"\"")}\",{d.ExpiresAtUtc:O},{d.ReviewStatus},{d.ReplacedByDocumentId}"); return Results.File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "compliance-audit.csv"); }
    private static async Task<IResult> CreateCampaignAsync(CreateCampaignRequest request, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, IAuditService audit, CancellationToken ct)
    { var tenant = tenants.GetRequiredTenant(context.User); if (request.VehicleIds.Count == 0) return Validation("vehicleIds", "At least one vehicle is required."); ComplianceInspectionCampaign campaign; try { campaign = new(tenant.OrganizationId, request.Name, request.OpensAtUtc, request.ClosesAtUtc); } catch (ArgumentException ex) { return Validation("campaign", ex.Message); } var assignments = await db.Missions.Where(x => x.OrganizationId == tenant.OrganizationId && request.VehicleIds.Contains(x.VehicleId ?? Guid.Empty) && x.DriverId != null).OrderByDescending(x => x.ScheduledStartUtc).ToListAsync(ct); foreach (var id in request.VehicleIds.Distinct()) { var assignment = assignments.FirstOrDefault(x => x.VehicleId == id); if (assignment?.DriverId is not Guid driverId) return Validation("vehicleIds", "Each selected vehicle must have an assigned driver."); campaign.AddTask(new(tenant.OrganizationId, campaign.Id, id, driverId, request.TemplateCode)); } db.ComplianceInspectionCampaigns.Add(campaign); db.ComplianceInspectionCampaignTasks.AddRange(campaign.Tasks); await db.SaveChangesAsync(ct); await audit.WriteAsync(tenant.OrganizationId, tenant.UserId, "compliance.campaign_created", "compliance-campaign", campaign.Id.ToString(), new { campaign.Name, count = campaign.Tasks.Count }, ct); return Results.Created($"/api/v1/compliance/campaigns/{campaign.Id}", await ToCampaignAsync(campaign, db, ct)); }
    private static async Task<IResult> ActivateCampaignAsync(Guid id, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, IAuditService audit, CancellationToken ct)
    { var tenant = tenants.GetRequiredTenant(context.User); var campaign = await db.ComplianceInspectionCampaigns.Include(x => x.Tasks).FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == id, ct); if (campaign is null) return Results.NotFound(); try { campaign.Activate(); } catch (InvalidOperationException ex) { return Validation("state", ex.Message); } await db.SaveChangesAsync(ct); await audit.WriteAsync(tenant.OrganizationId, tenant.UserId, "compliance.campaign_activated", "compliance-campaign", id.ToString(), null, ct); return Results.Ok(await ToCampaignAsync(campaign, db, ct)); }
    private static async Task<IResult> ListCampaignsAsync(HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor tenants, CancellationToken ct) { var tenant = tenants.GetRequiredTenant(context.User); var list = await db.ComplianceInspectionCampaigns.Include(x => x.Tasks).Where(x => x.OrganizationId == tenant.OrganizationId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct); var result = new List<CampaignResponse>(); foreach (var campaign in list) result.Add(await ToCampaignAsync(campaign, db, ct)); return Results.Ok(result); }
    internal static async Task<CampaignResponse> ToCampaignAsync(ComplianceInspectionCampaign campaign, FleetOpsDbContext db, CancellationToken ct) { var vehicleIds = campaign.Tasks.Select(x => x.VehicleId).ToList(); var driverIds = campaign.Tasks.Select(x => x.DriverId).ToList(); var vehicles = await db.Vehicles.Where(x => x.OrganizationId == campaign.OrganizationId && vehicleIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.RegistrationNumber, ct); var drivers = await db.Drivers.Where(x => x.OrganizationId == campaign.OrganizationId && driverIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct); return new(campaign.Id, campaign.Name, campaign.OpensAtUtc, campaign.ClosesAtUtc, campaign.Status, campaign.RowVersion, campaign.Tasks.Select(x => new CampaignTaskResponse(x.Id, x.VehicleId, vehicles.GetValueOrDefault(x.VehicleId, "Unknown"), x.DriverId, drivers.GetValueOrDefault(x.DriverId, "Unknown"), x.TemplateCode, x.Status, x.SubmittedAtUtc, x.Notes)).ToList()); }
    internal static IResult Validation(string key, string message) => Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
    internal static IResult Conflict(string message) => Results.ValidationProblem(new Dictionary<string, string[]> { ["rowVersion"] = [message] }, statusCode: StatusCodes.Status409Conflict);
}
