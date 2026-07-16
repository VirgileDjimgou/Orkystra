using System.Net.Mime;
using FleetOps.Api.Auditing;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Fleet;
using FleetOps.Core.Modules.Identity;
using FleetOps.Core.Modules.Integrations;
using FleetOps.Infrastructure.Integrations;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.Fleet;

public static class VehicleEndpointExtensions
{
    private const string AdminRole = SystemRoles.Admin;
    private const string ReaderRoles = SystemRoles.Admin + "," + SystemRoles.Operator;

    public static IEndpointRouteBuilder MapVehicleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/fleet/vehicles")
            .RequireAuthorization(new AuthorizeAttribute { Roles = ReaderRoles });

        group.MapGet("/", ListVehiclesAsync);
        group.MapGet("/export", ExportVehiclesAsync);
        group.MapGet("/{id:guid}", GetVehicleAsync);
        group.MapPost("/", CreateVehicleAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });
        group.MapPut("/{id:guid}", UpdateVehicleAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });
        group.MapPost("/{id:guid}/activate", ActivateVehicleAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });
        group.MapPost("/{id:guid}/deactivate", DeactivateVehicleAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });
        group.MapPost("/import", ImportVehiclesAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });

        return app;
    }

    private static async Task<IResult> ListVehiclesAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var vehicles = await dbContext.Vehicles
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderBy(x => x.RegistrationNumber)
            .Select(x => new VehicleResponse(
                x.Id,
                x.RegistrationNumber,
                x.DisplayName,
                x.IsActive,
                x.CurrentOdometerKm,
                x.RowVersion))
            .ToListAsync(cancellationToken);

        return Results.Ok(vehicles);
    }

    private static async Task<IResult> GetVehicleAsync(
        Guid id,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var vehicle = await dbContext.Vehicles
            .Where(x => x.OrganizationId == tenant.OrganizationId && x.Id == id)
            .Select(x => new VehicleResponse(
                x.Id,
                x.RegistrationNumber,
                x.DisplayName,
                x.IsActive,
                x.CurrentOdometerKm,
                x.RowVersion))
            .FirstOrDefaultAsync(cancellationToken);

        return vehicle is null ? Results.NotFound() : Results.Ok(vehicle);
    }

    private static async Task<IResult> CreateVehicleAsync(
        CreateVehicleRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        IIntegrationOutboxService integrationOutboxService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);

        var registration = request.RegistrationNumber?.Trim() ?? string.Empty;
        if (await dbContext.Vehicles.AnyAsync(
                x => x.OrganizationId == tenant.OrganizationId && x.RegistrationNumber == registration,
                cancellationToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["registrationNumber"] = ["Registration number already exists in this organization."]
            });
        }

        Vehicle vehicle;
        try
        {
            vehicle = new Vehicle(tenant.OrganizationId, registration, request.DisplayName);
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "body"] = [ex.Message]
            });
        }

        dbContext.Vehicles.Add(vehicle);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["registrationNumber"] = ["Registration number already exists in this organization."]
            });
        }

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "fleet.vehicle_created",
            "vehicle",
            vehicle.Id.ToString(),
            new { vehicle.RegistrationNumber, vehicle.DisplayName },
            cancellationToken);

        await integrationOutboxService.PublishAsync(
            tenant.OrganizationId,
            IntegrationEventType.FleetVehicleCreated,
            "vehicle",
            vehicle.Id.ToString(),
            new
            {
                vehicleId = vehicle.Id,
                vehicle.RegistrationNumber,
                vehicle.DisplayName,
                vehicle.IsActive,
                vehicle.CurrentOdometerKm
            },
            cancellationToken);

        return Results.Created($"/api/v1/fleet/vehicles/{vehicle.Id}", new VehicleResponse(
            vehicle.Id,
            vehicle.RegistrationNumber,
            vehicle.DisplayName,
            vehicle.IsActive,
            vehicle.CurrentOdometerKm,
            vehicle.RowVersion));
    }

    private static async Task<IResult> ExportVehiclesAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var vehicles = await dbContext.Vehicles
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderBy(x => x.RegistrationNumber)
            .Select(x => new
            {
                x.RegistrationNumber,
                x.DisplayName,
                x.IsActive,
                x.CurrentOdometerKm
            })
            .ToListAsync(cancellationToken);

        var lines = new List<string> { "registrationNumber,displayName,isActive,currentOdometerKm" };
        lines.AddRange(vehicles.Select(x =>
            $"{Escape(x.RegistrationNumber)},{Escape(x.DisplayName)},{x.IsActive.ToString().ToLowerInvariant()},{x.CurrentOdometerKm}"));

        return Results.Text(string.Join('\n', lines), "text/csv");
    }

    private static async Task<IResult> UpdateVehicleAsync(
        Guid id,
        UpdateVehicleRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var vehicle = await dbContext.Vehicles
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == id, cancellationToken);

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
            vehicle.Rename(request.DisplayName);
        }
        catch (ArgumentException ex)
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
            "fleet.vehicle_updated",
            "vehicle",
            vehicle.Id.ToString(),
            new { vehicle.DisplayName },
            cancellationToken);

        return Results.Ok(new VehicleResponse(
            vehicle.Id,
            vehicle.RegistrationNumber,
            vehicle.DisplayName,
            vehicle.IsActive,
            vehicle.CurrentOdometerKm,
            vehicle.RowVersion));
    }

    private static async Task<IResult> ActivateVehicleAsync(
        Guid id,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        return await SetVehicleStatusAsync(id, activate: true, httpContext, dbContext, currentTenantAccessor, auditService, cancellationToken);
    }

    private static async Task<IResult> DeactivateVehicleAsync(
        Guid id,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        return await SetVehicleStatusAsync(id, activate: false, httpContext, dbContext, currentTenantAccessor, auditService, cancellationToken);
    }

    private static async Task<IResult> SetVehicleStatusAsync(
        Guid id,
        bool activate,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var vehicle = await dbContext.Vehicles
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == id, cancellationToken);

        if (vehicle is null)
        {
            return Results.NotFound();
        }

        try
        {
            if (activate)
            {
                vehicle.Activate();
            }
            else
            {
                vehicle.Deactivate();
            }
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
            activate ? "fleet.vehicle_activated" : "fleet.vehicle_deactivated",
            "vehicle",
            vehicle.Id.ToString(),
            null,
            cancellationToken);

        return Results.Ok(new VehicleResponse(
            vehicle.Id,
            vehicle.RegistrationNumber,
            vehicle.DisplayName,
            vehicle.IsActive,
            vehicle.CurrentOdometerKm,
            vehicle.RowVersion));
    }

    private static async Task<IResult> ImportVehiclesAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var contentType = httpContext.Request.ContentType ?? string.Empty;
        if (!contentType.Contains("text/csv", StringComparison.OrdinalIgnoreCase)
            && !contentType.Contains("text/plain", StringComparison.OrdinalIgnoreCase))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["content"] = ["Expected text/csv body."]
            });
        }

        using var reader = new StreamReader(httpContext.Request.Body);
        var csv = await reader.ReadToEndAsync(cancellationToken);
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);

        IReadOnlyList<string[]> rows;
        try
        {
            rows = CsvFleetImporter.Parse(csv, minimumColumns: 2);
        }
        catch (InvalidDataException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["content"] = [ex.Message]
            });
        }

        var created = 0;
        var updated = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var row in rows)
        {
            var registration = row[0];
            var displayName = row[1];

            var existing = await dbContext.Vehicles
                .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.RegistrationNumber == registration, cancellationToken);

            try
            {
                if (existing is null)
                {
                    dbContext.Vehicles.Add(new Vehicle(tenant.OrganizationId, registration, displayName));
                    created++;
                }
                else
                {
                    existing.Rename(displayName);
                    updated++;
                }
            }
            catch (ArgumentException ex)
            {
                errors.Add($"Row '{registration}': {ex.Message}");
                skipped++;
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["registrationNumber"] = ["One or more registration numbers already exist in the organization."]
            });
        }

        var finalSummary = new ImportSummary(created, updated, skipped, errors);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "fleet.vehicle_imported",
            "vehicle",
            null,
            new { created = finalSummary.Created, updated = finalSummary.Updated, skipped = finalSummary.Skipped },
            cancellationToken);

        return Results.Ok(finalSummary);
    }

    private static string Escape(string value)
    {
        return value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }
}
