using FleetOps.Api.Auditing;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Alerts;
using FleetOps.Core.Modules.Fleet;
using FleetOps.Core.Modules.Identity;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.Alerts;

public static class FleetAlertConfigurationEndpointExtensions
{
    private const string AdminRole = SystemRoles.Admin;
    private const string ReaderRoles = SystemRoles.Admin + "," + SystemRoles.Operator;

    public static IEndpointRouteBuilder MapFleetAlertConfigurationEndpoints(this IEndpointRouteBuilder app)
    {
        var vehicleDocuments = app.MapGroup("/api/v1/fleet/vehicles/{vehicleId:guid}/documents")
            .RequireAuthorization(new AuthorizeAttribute { Roles = ReaderRoles });
        vehicleDocuments.MapGet("/", ListVehicleDocumentsAsync);
        vehicleDocuments.MapPost("/", CreateVehicleDocumentAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });
        vehicleDocuments.MapPut("/{documentId:guid}", UpdateVehicleDocumentAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });

        var driverDocuments = app.MapGroup("/api/v1/fleet/drivers/{driverId:guid}/documents")
            .RequireAuthorization(new AuthorizeAttribute { Roles = ReaderRoles });
        driverDocuments.MapGet("/", ListDriverDocumentsAsync);
        driverDocuments.MapPost("/", CreateDriverDocumentAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });
        driverDocuments.MapPut("/{documentId:guid}", UpdateDriverDocumentAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });

        var maintenancePlans = app.MapGroup("/api/v1/fleet/vehicles/{vehicleId:guid}/maintenance-plans")
            .RequireAuthorization(new AuthorizeAttribute { Roles = ReaderRoles });
        maintenancePlans.MapGet("/", ListVehicleMaintenancePlansAsync);
        maintenancePlans.MapPost("/", CreateVehicleMaintenancePlanAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });
        maintenancePlans.MapPost("/{planId:guid}/complete", CompleteVehicleMaintenancePlanAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });
        maintenancePlans.MapPost("/{planId:guid}/deactivate", DeactivateVehicleMaintenancePlanAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });

        app.MapPost("/api/v1/fleet/vehicles/{vehicleId:guid}/odometer", UpdateVehicleOdometerAsync)
            .RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole + "," + SystemRoles.Operator });

        return app;
    }

    private static async Task<IResult> ListVehicleDocumentsAsync(
        Guid vehicleId,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        return await ListDocumentsAsync(vehicleId, ComplianceDocumentTargetType.Vehicle, httpContext, dbContext, currentTenantAccessor, cancellationToken);
    }

    private static async Task<IResult> ListDriverDocumentsAsync(
        Guid driverId,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        return await ListDocumentsAsync(driverId, ComplianceDocumentTargetType.Driver, httpContext, dbContext, currentTenantAccessor, cancellationToken);
    }

    private static async Task<IResult> CreateVehicleDocumentAsync(
        Guid vehicleId,
        CreateComplianceDocumentRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        return await CreateDocumentAsync(
            vehicleId,
            ComplianceDocumentTargetType.Vehicle,
            request,
            httpContext,
            dbContext,
            currentTenantAccessor,
            auditService,
            cancellationToken);
    }

    private static async Task<IResult> CreateDriverDocumentAsync(
        Guid driverId,
        CreateComplianceDocumentRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        return await CreateDocumentAsync(
            driverId,
            ComplianceDocumentTargetType.Driver,
            request,
            httpContext,
            dbContext,
            currentTenantAccessor,
            auditService,
            cancellationToken);
    }

    private static async Task<IResult> UpdateVehicleDocumentAsync(
        Guid vehicleId,
        Guid documentId,
        UpdateComplianceDocumentRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        return await UpdateDocumentAsync(
            vehicleId,
            documentId,
            ComplianceDocumentTargetType.Vehicle,
            request,
            httpContext,
            dbContext,
            currentTenantAccessor,
            auditService,
            cancellationToken);
    }

    private static async Task<IResult> UpdateDriverDocumentAsync(
        Guid driverId,
        Guid documentId,
        UpdateComplianceDocumentRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        return await UpdateDocumentAsync(
            driverId,
            documentId,
            ComplianceDocumentTargetType.Driver,
            request,
            httpContext,
            dbContext,
            currentTenantAccessor,
            auditService,
            cancellationToken);
    }

    private static async Task<IResult> ListVehicleMaintenancePlansAsync(
        Guid vehicleId,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var vehicleExists = await dbContext.Vehicles.AnyAsync(
            x => x.OrganizationId == tenant.OrganizationId && x.Id == vehicleId,
            cancellationToken);
        if (!vehicleExists)
        {
            return Results.NotFound();
        }

        var plans = await dbContext.VehicleMaintenancePlans
            .AsNoTracking()
            .Where(x => x.OrganizationId == tenant.OrganizationId && x.VehicleId == vehicleId)
            .OrderBy(x => x.Title)
            .Select(x => new VehicleMaintenancePlanResponse(
                x.Id,
                x.VehicleId,
                x.Title,
                x.IntervalKilometers,
                x.IntervalDays,
                x.LastCompletedOdometerKm,
                x.LastCompletedAtUtc,
                x.IsActive,
                x.RowVersion))
            .ToListAsync(cancellationToken);

        return Results.Ok(plans);
    }

    private static async Task<IResult> CreateVehicleMaintenancePlanAsync(
        Guid vehicleId,
        CreateVehicleMaintenancePlanRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var vehicle = await dbContext.Vehicles
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == vehicleId, cancellationToken);
        if (vehicle is null)
        {
            return Results.NotFound();
        }

        if (await dbContext.VehicleMaintenancePlans.AnyAsync(
                x => x.OrganizationId == tenant.OrganizationId && x.VehicleId == vehicleId && x.Title == request.Title.Trim(),
                cancellationToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["title"] = ["A maintenance plan with the same title already exists for this vehicle."]
            });
        }

        VehicleMaintenancePlan plan;
        try
        {
            plan = new VehicleMaintenancePlan(
                tenant.OrganizationId,
                vehicleId,
                request.Title,
                request.IntervalKilometers,
                request.IntervalDays,
                request.LastCompletedOdometerKm,
                request.LastCompletedAtUtc);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            var parameterName = ex is ArgumentException argumentException
                ? argumentException.ParamName
                : "body";
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [parameterName ?? "body"] = [ex.Message]
            });
        }

        dbContext.VehicleMaintenancePlans.Add(plan);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "alerts.maintenance_plan_created",
            "vehicle",
            vehicleId.ToString(),
            new { plan.Title },
            cancellationToken);

        return Results.Created(
            $"/api/v1/fleet/vehicles/{vehicleId}/maintenance-plans/{plan.Id}",
            new VehicleMaintenancePlanResponse(
                plan.Id,
                plan.VehicleId,
                plan.Title,
                plan.IntervalKilometers,
                plan.IntervalDays,
                plan.LastCompletedOdometerKm,
                plan.LastCompletedAtUtc,
                plan.IsActive,
                plan.RowVersion));
    }

    private static async Task<IResult> CompleteVehicleMaintenancePlanAsync(
        Guid vehicleId,
        Guid planId,
        CompleteVehicleMaintenancePlanRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var plan = await dbContext.VehicleMaintenancePlans
            .FirstOrDefaultAsync(
                x => x.OrganizationId == tenant.OrganizationId && x.VehicleId == vehicleId && x.Id == planId,
                cancellationToken);
        if (plan is null)
        {
            return Results.NotFound();
        }

        if (plan.RowVersion != request.RowVersion)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["Maintenance plan was modified by another request. Reload and try again."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        try
        {
            plan.MarkCompleted(request.CompletedOdometerKm, request.CompletedAtUtc);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "body"] = [ex.Message]
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "alerts.maintenance_completed",
            "maintenance-plan",
            plan.Id.ToString(),
            new { request.CompletedOdometerKm, request.CompletedAtUtc },
            cancellationToken);

        return Results.Ok(new VehicleMaintenancePlanResponse(
            plan.Id,
            plan.VehicleId,
            plan.Title,
            plan.IntervalKilometers,
            plan.IntervalDays,
            plan.LastCompletedOdometerKm,
            plan.LastCompletedAtUtc,
            plan.IsActive,
            plan.RowVersion));
    }

    private static async Task<IResult> DeactivateVehicleMaintenancePlanAsync(
        Guid vehicleId,
        Guid planId,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var plan = await dbContext.VehicleMaintenancePlans
            .FirstOrDefaultAsync(
                x => x.OrganizationId == tenant.OrganizationId && x.VehicleId == vehicleId && x.Id == planId,
                cancellationToken);
        if (plan is null)
        {
            return Results.NotFound();
        }

        try
        {
            plan.Deactivate();
        }
        catch (InvalidOperationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["state"] = [ex.Message]
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "alerts.maintenance_plan_deactivated",
            "maintenance-plan",
            plan.Id.ToString(),
            null,
            cancellationToken);

        return Results.Ok(new VehicleMaintenancePlanResponse(
            plan.Id,
            plan.VehicleId,
            plan.Title,
            plan.IntervalKilometers,
            plan.IntervalDays,
            plan.LastCompletedOdometerKm,
            plan.LastCompletedAtUtc,
            plan.IsActive,
            plan.RowVersion));
    }

    private static async Task<IResult> UpdateVehicleOdometerAsync(
        Guid vehicleId,
        UpdateVehicleOdometerRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var vehicle = await dbContext.Vehicles
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == vehicleId, cancellationToken);
        if (vehicle is null)
        {
            return Results.NotFound();
        }

        if (vehicle.RowVersion != request.RowVersion)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["Vehicle was modified by another request. Reload and try again."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        try
        {
            vehicle.UpdateCurrentOdometer(request.CurrentOdometerKm);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "body"] = [ex.Message]
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "fleet.vehicle_odometer_updated",
            "vehicle",
            vehicle.Id.ToString(),
            new { vehicle.CurrentOdometerKm },
            cancellationToken);

        return Results.Ok(new Fleet.VehicleResponse(
            vehicle.Id,
            vehicle.RegistrationNumber,
            vehicle.DisplayName,
            vehicle.IsActive,
            vehicle.CurrentOdometerKm,
            vehicle.RowVersion));
    }

    private static async Task<IResult> ListDocumentsAsync(
        Guid targetEntityId,
        ComplianceDocumentTargetType targetType,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var targetExists = await TargetExistsAsync(dbContext, tenant.OrganizationId, targetEntityId, targetType, cancellationToken);
        if (!targetExists)
        {
            return Results.NotFound();
        }

        var documents = await dbContext.ComplianceDocuments
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId == tenant.OrganizationId
                && x.TargetEntityId == targetEntityId
                && x.TargetType == targetType)
            .OrderBy(x => x.ExpiresAtUtc)
            .Select(x => new ComplianceDocumentResponse(
                x.Id,
                x.TargetEntityId,
                x.TargetType,
                x.DocumentType,
                x.DocumentNumber,
                x.ExpiresAtUtc,
                x.Notes,
                x.RowVersion))
            .ToListAsync(cancellationToken);

        return Results.Ok(documents);
    }

    private static async Task<IResult> CreateDocumentAsync(
        Guid targetEntityId,
        ComplianceDocumentTargetType targetType,
        CreateComplianceDocumentRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var targetExists = await TargetExistsAsync(dbContext, tenant.OrganizationId, targetEntityId, targetType, cancellationToken);
        if (!targetExists)
        {
            return Results.NotFound();
        }

        var documentType = request.DocumentType?.Trim() ?? string.Empty;
        if (await dbContext.ComplianceDocuments.AnyAsync(
                x => x.OrganizationId == tenant.OrganizationId
                    && x.TargetEntityId == targetEntityId
                    && x.TargetType == targetType
                    && x.DocumentType == documentType,
                cancellationToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["documentType"] = ["A document of this type already exists for the selected target."]
            });
        }

        ComplianceDocument document;
        try
        {
            document = new ComplianceDocument(
                tenant.OrganizationId,
                targetType,
                targetEntityId,
                documentType,
                request.DocumentNumber,
                request.ExpiresAtUtc,
                request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            var parameterName = ex is ArgumentException argumentException
                ? argumentException.ParamName
                : "body";
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [parameterName ?? "body"] = [ex.Message]
            });
        }

        dbContext.ComplianceDocuments.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "alerts.document_created",
            targetType == ComplianceDocumentTargetType.Vehicle ? "vehicle" : "driver",
            targetEntityId.ToString(),
            new { document.DocumentType, document.ExpiresAtUtc },
            cancellationToken);

        return Results.Created(
            $"/api/v1/{(targetType == ComplianceDocumentTargetType.Vehicle ? "fleet/vehicles" : "fleet/drivers")}/{targetEntityId}/documents/{document.Id}",
            new ComplianceDocumentResponse(
                document.Id,
                document.TargetEntityId,
                document.TargetType,
                document.DocumentType,
                document.DocumentNumber,
                document.ExpiresAtUtc,
                document.Notes,
                document.RowVersion));
    }

    private static async Task<IResult> UpdateDocumentAsync(
        Guid targetEntityId,
        Guid documentId,
        ComplianceDocumentTargetType targetType,
        UpdateComplianceDocumentRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var document = await dbContext.ComplianceDocuments
            .FirstOrDefaultAsync(
                x => x.OrganizationId == tenant.OrganizationId
                    && x.Id == documentId
                    && x.TargetEntityId == targetEntityId
                    && x.TargetType == targetType,
                cancellationToken);
        if (document is null)
        {
            return Results.NotFound();
        }

        if (document.RowVersion != request.RowVersion)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["Document was modified by another request. Reload and try again."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        document.UpdateExpiry(request.ExpiresAtUtc, request.Notes);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "alerts.document_updated",
            targetType == ComplianceDocumentTargetType.Vehicle ? "vehicle" : "driver",
            targetEntityId.ToString(),
            new { document.DocumentType, document.ExpiresAtUtc },
            cancellationToken);

        return Results.Ok(new ComplianceDocumentResponse(
            document.Id,
            document.TargetEntityId,
            document.TargetType,
            document.DocumentType,
            document.DocumentNumber,
            document.ExpiresAtUtc,
            document.Notes,
            document.RowVersion));
    }

    private static Task<bool> TargetExistsAsync(
        FleetOpsDbContext dbContext,
        Guid organizationId,
        Guid targetEntityId,
        ComplianceDocumentTargetType targetType,
        CancellationToken cancellationToken)
    {
        return targetType == ComplianceDocumentTargetType.Vehicle
            ? dbContext.Vehicles.AnyAsync(x => x.OrganizationId == organizationId && x.Id == targetEntityId, cancellationToken)
            : dbContext.Drivers.AnyAsync(x => x.OrganizationId == organizationId && x.Id == targetEntityId, cancellationToken);
    }
}
