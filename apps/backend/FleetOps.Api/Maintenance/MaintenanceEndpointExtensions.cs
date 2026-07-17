using FleetOps.Api.Auditing;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Identity;
using FleetOps.Core.Modules.Maintenance;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.Maintenance;

public static class MaintenanceEndpointExtensions
{
    private const string Roles = SystemRoles.Admin + "," + SystemRoles.Operator;
    public static IEndpointRouteBuilder MapMaintenanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/maintenance/work-orders").RequireAuthorization(new AuthorizeAttribute { Roles = Roles });
        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPost("/{id:guid}/schedule", ScheduleAsync);
        group.MapPost("/{id:guid}/complete", CompleteAsync);
        group.MapPut("/{id:guid}/cost", SetCostAsync);
        return app;
    }
    private static async Task<IResult> ListAsync(HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor current, CancellationToken ct)
    {
        var tenant = current.GetRequiredTenant(context.User);
        var orders = await db.MaintenanceWorkOrders.Where(x => x.OrganizationId == tenant.OrganizationId).OrderBy(x => x.Status).ThenBy(x => x.DueAtUtc).ToListAsync(ct);
        var vehicles = await db.Vehicles.Where(x => x.OrganizationId == tenant.OrganizationId).ToDictionaryAsync(x => x.Id, x => x.RegistrationNumber, ct);
        return Results.Ok(orders.Select(x => Map(x, vehicles.GetValueOrDefault(x.VehicleId, "Unknown"))));
    }
    private static async Task<IResult> CreateAsync(CreateMaintenanceWorkOrderRequest request, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor current, IAuditService audit, CancellationToken ct)
    {
        var tenant = current.GetRequiredTenant(context.User);
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == request.VehicleId, ct);
        if (vehicle is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["vehicleId"] = ["Vehicle does not exist in this organization."] });
        var existing = await db.MaintenanceWorkOrders.FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.SourceKey == request.SourceKey && x.Status != MaintenanceWorkOrderStatus.Completed && x.Status != MaintenanceWorkOrderStatus.Cancelled, ct);
        if (existing is not null) return Results.Conflict(Map(existing, vehicle.RegistrationNumber));
        try
        {
            var order = new MaintenanceWorkOrder(tenant.OrganizationId, request.VehicleId, request.Title, request.SourceKey, request.Priority, request.DueAtUtc, request.ImmobilizesVehicle);
            db.MaintenanceWorkOrders.Add(order); await db.SaveChangesAsync(ct);
            await audit.WriteAsync(tenant.OrganizationId, tenant.UserId, "maintenance.work_order_created", "maintenance_work_order", order.Id.ToString(), new { order.SourceKey, order.VehicleId }, ct);
            return Results.Created($"/api/v1/maintenance/work-orders/{order.Id}", Map(order, vehicle.RegistrationNumber));
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["workOrder"] = [ex.Message] }); }
    }
    private static Task<IResult> ScheduleAsync(Guid id, ScheduleMaintenanceWorkOrderRequest request, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor current, IAuditService audit, CancellationToken ct) => UpdateAsync(id, request.RowVersion, context, db, current, audit, (o) => o.Schedule(request.ScheduledStartUtc, request.ScheduledEndUtc, request.Reason), "scheduled", ct);
    private static Task<IResult> CompleteAsync(Guid id, CompleteMaintenanceWorkOrderRequest request, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor current, IAuditService audit, CancellationToken ct) => UpdateAsync(id, request.RowVersion, context, db, current, audit, (o) => o.Complete(request.Reason, DateTimeOffset.UtcNow), "completed", ct);
    private static async Task<IResult> SetCostAsync(Guid id, SetMaintenanceCostRequest request, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor current, IAuditService audit, CancellationToken ct)
    {
        var tenant = current.GetRequiredTenant(context.User); var order = await db.MaintenanceWorkOrders.FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == id, ct); if (order is null) return Results.NotFound(); if (order.RowVersion != request.RowVersion) return Conflict();
        if (request.AttachmentMediaAssetId is Guid asset && !await db.MediaAssets.AnyAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == asset, ct)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["attachmentMediaAssetId"] = ["Media asset does not belong to this organization."] });
        try { order.SetCost(request.LaborCost, request.PartsCost, request.CurrencyCode, request.SupplierName, request.PartsDescription, request.AttachmentMediaAssetId); await db.SaveChangesAsync(ct); await audit.WriteAsync(tenant.OrganizationId, tenant.UserId, "maintenance.work_order_cost_updated", "maintenance_work_order", order.Id.ToString(), new { order.TotalCost, order.CurrencyCode }, ct); return Results.Ok(Map(order, await VehicleNameAsync(order, db, ct))); } catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["cost"] = [ex.Message] }); }
    }
    private static async Task<IResult> UpdateAsync(Guid id, long rowVersion, HttpContext context, FleetOpsDbContext db, ICurrentTenantAccessor current, IAuditService audit, Action<MaintenanceWorkOrder> action, string eventName, CancellationToken ct)
    { var tenant = current.GetRequiredTenant(context.User); var order = await db.MaintenanceWorkOrders.FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == id, ct); if (order is null) return Results.NotFound(); if (order.RowVersion != rowVersion) return Conflict(); try { action(order); await db.SaveChangesAsync(ct); await audit.WriteAsync(tenant.OrganizationId, tenant.UserId, $"maintenance.work_order_{eventName}", "maintenance_work_order", order.Id.ToString(), new { order.Status, order.TransitionReason }, ct); return Results.Ok(Map(order, await VehicleNameAsync(order, db, ct))); } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["workOrder"] = [ex.Message] }, statusCode: StatusCodes.Status409Conflict); } }
    private static IResult Conflict() => Results.ValidationProblem(new Dictionary<string, string[]> { ["rowVersion"] = ["Work order was modified by another request. Reload and try again."] }, statusCode: StatusCodes.Status409Conflict);
    private static async Task<string> VehicleNameAsync(MaintenanceWorkOrder order, FleetOpsDbContext db, CancellationToken ct) => await db.Vehicles.Where(x => x.OrganizationId == order.OrganizationId && x.Id == order.VehicleId).Select(x => x.RegistrationNumber).FirstOrDefaultAsync(ct) ?? "Unknown";
    private static MaintenanceWorkOrderResponse Map(MaintenanceWorkOrder x, string vehicle) => new(x.Id, x.VehicleId, vehicle, x.Title, x.SourceKey, x.Priority, x.DueAtUtc, x.ScheduledStartUtc, x.ScheduledEndUtc, x.ImmobilizesVehicle, x.IsVehicleUnavailable, x.Status, x.LaborCost, x.PartsCost, x.TotalCost, x.CurrencyCode, x.SupplierName, x.PartsDescription, x.AttachmentMediaAssetId, x.CompletedAtUtc, x.TransitionReason, x.RowVersion);
}
