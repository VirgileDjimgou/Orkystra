using FleetOps.Api.Auditing;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Fleet;
using FleetOps.Core.Modules.Identity;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.Fleet;

public static class DeviceEndpointExtensions
{
    private const string AdminRole = SystemRoles.Admin;
    private const string ReaderRoles = SystemRoles.Admin + "," + SystemRoles.Operator;

    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/fleet/devices")
            .RequireAuthorization(new AuthorizeAttribute { Roles = ReaderRoles });

        group.MapGet("/", ListDevicesAsync);
        group.MapGet("/{id:guid}", GetDeviceAsync);
        group.MapPost("/", CreateDeviceAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });
        group.MapPut("/{id:guid}", UpdateDeviceAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });
        group.MapPost("/{id:guid}/activate", ActivateDeviceAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });
        group.MapPost("/{id:guid}/deactivate", DeactivateDeviceAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });
        group.MapPost("/import", ImportDevicesAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });

        var assignments = app.MapGroup("/api/v1/fleet/devices/{deviceId:guid}/assignments")
            .RequireAuthorization(new AuthorizeAttribute { Roles = ReaderRoles });
        assignments.MapGet("/", ListDeviceAssignmentsAsync);
        assignments.MapPost("/active", AssignDeviceAsync)
            .RequireAuthorization(new AuthorizeAttribute { Roles = ReaderRoles });
        assignments.MapPost("/active/close", CloseActiveAssignmentAsync)
            .RequireAuthorization(new AuthorizeAttribute { Roles = ReaderRoles });

        return app;
    }

    private static async Task<IResult> ListDevicesAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var devices = await dbContext.GpsDevices
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderBy(x => x.SerialNumber)
            .Select(x => new GpsDeviceResponse(
                x.Id,
                x.SerialNumber,
                x.DisplayName,
                x.IsActive,
                x.RowVersion,
                null))
            .ToListAsync(cancellationToken);

        var activeAssignments = await dbContext.DeviceAssignments
            .Where(x => x.OrganizationId == tenant.OrganizationId && x.UnassignedAtUtc == null)
            .ToListAsync(cancellationToken);
        var vehicles = await dbContext.Vehicles
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .ToDictionaryAsync(x => x.Id, x => x.RegistrationNumber, cancellationToken);

        for (var i = 0; i < devices.Count; i++)
        {
            var current = devices[i];
            var active = activeAssignments.FirstOrDefault(x => x.DeviceId == current.Id);
            if (active is not null && vehicles.TryGetValue(active.VehicleId, out var registration))
            {
                devices[i] = current with
                {
                    ActiveAssignment = new ActiveAssignmentResponse(
                    active.Id,
                    active.VehicleId,
                    registration,
                    active.AssignedAtUtc)
                };
            }
        }

        return Results.Ok(devices);
    }

    private static async Task<IResult> GetDeviceAsync(
        Guid id,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var device = await dbContext.GpsDevices
            .Where(x => x.OrganizationId == tenant.OrganizationId && x.Id == id)
            .Select(x => new GpsDeviceResponse(
                x.Id,
                x.SerialNumber,
                x.DisplayName,
                x.IsActive,
                x.RowVersion,
                null))
            .FirstOrDefaultAsync(cancellationToken);

        if (device is null)
        {
            return Results.NotFound();
        }

        var active = await dbContext.DeviceAssignments
            .Where(x => x.OrganizationId == tenant.OrganizationId && x.DeviceId == id && x.UnassignedAtUtc == null)
            .FirstOrDefaultAsync(cancellationToken);
        if (active is not null)
        {
            var vehicleRegistration = await dbContext.Vehicles
                .Where(x => x.OrganizationId == tenant.OrganizationId && x.Id == active.VehicleId)
                .Select(x => x.RegistrationNumber)
                .FirstOrDefaultAsync(cancellationToken);
            device = device with
            {
                ActiveAssignment = new ActiveAssignmentResponse(
                active.Id,
                active.VehicleId,
                vehicleRegistration ?? string.Empty,
                active.AssignedAtUtc)
            };
        }

        return Results.Ok(device);
    }

    private static async Task<IResult> CreateDeviceAsync(
        CreateGpsDeviceRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var serialNumber = request.SerialNumber?.Trim() ?? string.Empty;

        if (await dbContext.GpsDevices.AnyAsync(
                x => x.OrganizationId == tenant.OrganizationId && x.SerialNumber == serialNumber,
                cancellationToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["serialNumber"] = ["Serial number already exists in this organization."]
            });
        }

        GpsDevice device;
        try
        {
            device = new GpsDevice(tenant.OrganizationId, serialNumber, request.DisplayName);
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "body"] = [ex.Message]
            });
        }

        dbContext.GpsDevices.Add(device);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["serialNumber"] = ["Serial number already exists in this organization."]
            });
        }

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "fleet.device_created",
            "gps_device",
            device.Id.ToString(),
            new { device.SerialNumber },
            cancellationToken);

        return Results.Created($"/api/v1/fleet/devices/{device.Id}", new GpsDeviceResponse(
            device.Id,
            device.SerialNumber,
            device.DisplayName,
            device.IsActive,
            device.RowVersion,
            null));
    }

    private static async Task<IResult> UpdateDeviceAsync(
        Guid id,
        UpdateGpsDeviceRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var device = await dbContext.GpsDevices
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == id, cancellationToken);

        if (device is null)
        {
            return Results.NotFound();
        }

        if (device.RowVersion != request.RowVersion)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["Device was modified by another request. Reload and try again."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        device.Rename(request.DisplayName);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "fleet.device_updated",
            "gps_device",
            device.Id.ToString(),
            new { device.DisplayName },
            cancellationToken);

        return Results.Ok(new GpsDeviceResponse(
            device.Id,
            device.SerialNumber,
            device.DisplayName,
            device.IsActive,
            device.RowVersion,
            null));
    }

    private static async Task<IResult> ImportDevicesAsync(
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
            rows = CsvFleetImporter.Parse(csv, minimumColumns: 1);
        }
        catch (InvalidDataException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["content"] = [ex.Message]
            });
        }

        var errors = new List<string>();
        var created = 0;
        var updated = 0;
        var skipped = 0;

        foreach (var row in rows)
        {
            var serial = row[0];
            var displayName = row.Length >= 2 ? row[1] : null;

            var existing = await dbContext.GpsDevices
                .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.SerialNumber == serial, cancellationToken);

            try
            {
                if (existing is null)
                {
                    dbContext.GpsDevices.Add(new GpsDevice(tenant.OrganizationId, serial, displayName));
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
                errors.Add($"Row '{serial}': {ex.Message}");
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
                ["serialNumber"] = ["One or more serial numbers already exist in the organization."]
            });
        }

        var summary = new ImportSummary(created, updated, skipped, errors);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "fleet.device_imported",
            "gps_device",
            null,
            new { created = summary.Created, updated = summary.Updated, skipped = summary.Skipped },
            cancellationToken);

        return Results.Ok(summary);
    }

    private static async Task<IResult> ActivateDeviceAsync(
        Guid id,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        return await SetDeviceStatusAsync(id, activate: true, httpContext, dbContext, currentTenantAccessor, auditService, cancellationToken);
    }

    private static async Task<IResult> DeactivateDeviceAsync(
        Guid id,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        return await SetDeviceStatusAsync(id, activate: false, httpContext, dbContext, currentTenantAccessor, auditService, cancellationToken);
    }

    private static async Task<IResult> SetDeviceStatusAsync(
        Guid id,
        bool activate,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var device = await dbContext.GpsDevices
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == id, cancellationToken);

        if (device is null)
        {
            return Results.NotFound();
        }

        try
        {
            if (activate)
            {
                device.Activate();
            }
            else
            {
                device.Deactivate();
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
            activate ? "fleet.device_activated" : "fleet.device_deactivated",
            "gps_device",
            device.Id.ToString(),
            null,
            cancellationToken);

        return Results.Ok(new GpsDeviceResponse(
            device.Id,
            device.SerialNumber,
            device.DisplayName,
            device.IsActive,
            device.RowVersion,
            null));
    }

    private static async Task<IResult> ListDeviceAssignmentsAsync(
        Guid deviceId,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var device = await dbContext.GpsDevices
            .AnyAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == deviceId, cancellationToken);

        if (!device)
        {
            return Results.NotFound();
        }

        var assignments = await dbContext.DeviceAssignments
            .Where(x => x.OrganizationId == tenant.OrganizationId && x.DeviceId == deviceId)
            .OrderByDescending(x => x.AssignedAtUtc)
            .Select(x => new AssignmentResponse(
                x.Id,
                x.DeviceId,
                x.VehicleId,
                x.AssignedAtUtc,
                x.UnassignedAtUtc,
                x.UnassignedAtUtc == null))
            .ToListAsync(cancellationToken);

        return Results.Ok(assignments);
    }

    private static async Task<IResult> AssignDeviceAsync(
        Guid deviceId,
        AssignDeviceRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);

        var device = await dbContext.GpsDevices
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == deviceId, cancellationToken);
        if (device is null)
        {
            return Results.NotFound();
        }

        if (!device.IsActive)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["device"] = ["Cannot assign an inactive device."]
            });
        }

        var vehicle = await dbContext.Vehicles
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == request.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["vehicleId"] = ["Target vehicle does not exist in this organization."]
            });
        }

        if (!vehicle.IsActive)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["vehicleId"] = ["Cannot assign a device to an inactive vehicle."]
            });
        }

        var previousActive = await dbContext.DeviceAssignments
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId
                && x.DeviceId == deviceId
                && x.UnassignedAtUtc == null, cancellationToken);

        if (previousActive is not null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignment"] = ["Device already has an active assignment. Close it first."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        var assignment = new DeviceAssignment(tenant.OrganizationId, deviceId, request.VehicleId, DateTimeOffset.UtcNow);
        dbContext.DeviceAssignments.Add(assignment);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignment"] = ["Device already has an active assignment."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "fleet.assignment_created",
            "device_assignment",
            assignment.Id.ToString(),
            new { assignment.DeviceId, assignment.VehicleId },
            cancellationToken);

        return Results.Created($"/api/v1/fleet/devices/{deviceId}/assignments", new AssignmentResponse(
            assignment.Id,
            assignment.DeviceId,
            assignment.VehicleId,
            assignment.AssignedAtUtc,
            assignment.UnassignedAtUtc,
            assignment.IsActive));
    }

    private static async Task<IResult> CloseActiveAssignmentAsync(
        Guid deviceId,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var device = await dbContext.GpsDevices
            .AnyAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == deviceId, cancellationToken);

        if (!device)
        {
            return Results.NotFound();
        }

        var active = await dbContext.DeviceAssignments
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId
                && x.DeviceId == deviceId
                && x.UnassignedAtUtc == null, cancellationToken);

        if (active is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignment"] = ["Device has no active assignment to close."]
            });
        }

        active.Close(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "fleet.assignment_closed",
            "device_assignment",
            active.Id.ToString(),
            new { active.DeviceId, active.VehicleId },
            cancellationToken);

        return Results.Ok(new AssignmentResponse(
            active.Id,
            active.DeviceId,
            active.VehicleId,
            active.AssignedAtUtc,
            active.UnassignedAtUtc,
            active.IsActive));
    }
}
