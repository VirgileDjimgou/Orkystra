using FleetOps.Core.Modules.Maintenance;
namespace FleetOps.Api.Maintenance;

public sealed record CreateMaintenanceWorkOrderRequest(Guid VehicleId, string Title, string SourceKey, int Priority, DateTimeOffset DueAtUtc, bool ImmobilizesVehicle);
public sealed record ScheduleMaintenanceWorkOrderRequest(DateTimeOffset ScheduledStartUtc, DateTimeOffset ScheduledEndUtc, string Reason, long RowVersion);
public sealed record CompleteMaintenanceWorkOrderRequest(string Reason, long RowVersion);
public sealed record SetMaintenanceCostRequest(decimal LaborCost, decimal PartsCost, string CurrencyCode, string? SupplierName, string? PartsDescription, Guid? AttachmentMediaAssetId, long RowVersion);
public sealed record MaintenanceWorkOrderResponse(Guid Id, Guid VehicleId, string VehicleRegistrationNumber, string Title, string SourceKey, int Priority, DateTimeOffset DueAtUtc, DateTimeOffset? ScheduledStartUtc, DateTimeOffset? ScheduledEndUtc, bool ImmobilizesVehicle, bool IsVehicleUnavailable, MaintenanceWorkOrderStatus Status, decimal LaborCost, decimal PartsCost, decimal TotalCost, string CurrencyCode, string? SupplierName, string? PartsDescription, Guid? AttachmentMediaAssetId, DateTimeOffset? CompletedAtUtc, string? TransitionReason, long RowVersion);
