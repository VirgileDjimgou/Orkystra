using FleetOps.Api.Auditing;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Fleet;
using FleetOps.Core.Modules.Identity;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.Fleet;

public static class DriverEndpointExtensions
{
    private const string AdminRole = SystemRoles.Admin;
    private const string ReaderRoles = SystemRoles.Admin + "," + SystemRoles.Operator;

    public static IEndpointRouteBuilder MapDriverEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/fleet/drivers")
            .RequireAuthorization(new AuthorizeAttribute { Roles = ReaderRoles });

        group.MapGet("/", ListDriversAsync);
        group.MapGet("/{id:guid}", GetDriverAsync);
        group.MapPost("/", CreateDriverAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });
        group.MapPut("/{id:guid}", UpdateDriverAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });
        group.MapPost("/{id:guid}/activate", ActivateDriverAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });
        group.MapPost("/{id:guid}/deactivate", DeactivateDriverAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });
        group.MapPost("/import", ImportDriversAsync).RequireAuthorization(new AuthorizeAttribute { Roles = AdminRole });

        return app;
    }

    private static async Task<IResult> ListDriversAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var drivers = await dbContext.Drivers
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderBy(x => x.FullName)
            .Select(x => new DriverResponse(
                x.Id,
                x.FullName,
                x.LicenseNumber,
                x.PhoneNumber,
                x.IsActive,
                x.RowVersion))
            .ToListAsync(cancellationToken);

        return Results.Ok(drivers);
    }

    private static async Task<IResult> GetDriverAsync(
        Guid id,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var driver = await dbContext.Drivers
            .Where(x => x.OrganizationId == tenant.OrganizationId && x.Id == id)
            .Select(x => new DriverResponse(
                x.Id,
                x.FullName,
                x.LicenseNumber,
                x.PhoneNumber,
                x.IsActive,
                x.RowVersion))
            .FirstOrDefaultAsync(cancellationToken);

        return driver is null ? Results.NotFound() : Results.Ok(driver);
    }

    private static async Task<IResult> CreateDriverAsync(
        CreateDriverRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var licenseNumber = request.LicenseNumber?.Trim() ?? string.Empty;

        if (await dbContext.Drivers.AnyAsync(
                x => x.OrganizationId == tenant.OrganizationId && x.LicenseNumber == licenseNumber,
                cancellationToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["licenseNumber"] = ["License number already exists in this organization."]
            });
        }

        Driver driver;
        try
        {
            driver = new Driver(tenant.OrganizationId, request.FullName, licenseNumber, request.PhoneNumber);
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "body"] = [ex.Message]
            });
        }

        dbContext.Drivers.Add(driver);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["licenseNumber"] = ["License number already exists in this organization."]
            });
        }

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "fleet.driver_created",
            "driver",
            driver.Id.ToString(),
            new { driver.FullName, driver.LicenseNumber },
            cancellationToken);

        return Results.Created($"/api/v1/fleet/drivers/{driver.Id}", new DriverResponse(
            driver.Id,
            driver.FullName,
            driver.LicenseNumber,
            driver.PhoneNumber,
            driver.IsActive,
            driver.RowVersion));
    }

    private static async Task<IResult> UpdateDriverAsync(
        Guid id,
        UpdateDriverRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var driver = await dbContext.Drivers
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == id, cancellationToken);

        if (driver is null)
        {
            return Results.NotFound();
        }

        if (driver.RowVersion != request.RowVersion)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["Driver was modified by another request. Reload and try again."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        try
        {
            driver.Update(request.FullName, request.PhoneNumber);
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
            "fleet.driver_updated",
            "driver",
            driver.Id.ToString(),
            new { driver.FullName },
            cancellationToken);

        return Results.Ok(new DriverResponse(
            driver.Id,
            driver.FullName,
            driver.LicenseNumber,
            driver.PhoneNumber,
            driver.IsActive,
            driver.RowVersion));
    }

    private static async Task<IResult> ActivateDriverAsync(
        Guid id,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        return await SetDriverStatusAsync(id, activate: true, httpContext, dbContext, currentTenantAccessor, auditService, cancellationToken);
    }

    private static async Task<IResult> DeactivateDriverAsync(
        Guid id,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        return await SetDriverStatusAsync(id, activate: false, httpContext, dbContext, currentTenantAccessor, auditService, cancellationToken);
    }

    private static async Task<IResult> SetDriverStatusAsync(
        Guid id,
        bool activate,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var driver = await dbContext.Drivers
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == id, cancellationToken);

        if (driver is null)
        {
            return Results.NotFound();
        }

        try
        {
            if (activate)
            {
                driver.Activate();
            }
            else
            {
                driver.Deactivate();
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
            activate ? "fleet.driver_activated" : "fleet.driver_deactivated",
            "driver",
            driver.Id.ToString(),
            null,
            cancellationToken);

        return Results.Ok(new DriverResponse(
            driver.Id,
            driver.FullName,
            driver.LicenseNumber,
            driver.PhoneNumber,
            driver.IsActive,
            driver.RowVersion));
    }

    private static async Task<IResult> ImportDriversAsync(
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

        var errors = new List<string>();
        var created = 0;
        var updated = 0;
        var skipped = 0;

        foreach (var row in rows)
        {
            var name = row[0];
            var license = row[1];
            var phone = row.Length >= 3 ? row[2] : null;

            var existing = await dbContext.Drivers
                .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.LicenseNumber == license, cancellationToken);

            try
            {
                if (existing is null)
                {
                    dbContext.Drivers.Add(new Driver(tenant.OrganizationId, name, license, phone));
                    created++;
                }
                else
                {
                    existing.Update(name, phone);
                    updated++;
                }
            }
            catch (ArgumentException ex)
            {
                errors.Add($"Row '{license}': {ex.Message}");
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
                ["licenseNumber"] = ["One or more license numbers already exist in this organization."]
            });
        }

        var summary = new ImportSummary(created, updated, skipped, errors);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "fleet.driver_imported",
            "driver",
            null,
            new { created = summary.Created, updated = summary.Updated, skipped = summary.Skipped },
            cancellationToken);

        return Results.Ok(summary);
    }
}
