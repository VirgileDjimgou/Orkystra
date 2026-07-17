using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Maintenance;

public sealed class MaintenanceWorkOrder : TenantEntity
{
    private MaintenanceWorkOrder() { }

    public MaintenanceWorkOrder(Guid organizationId, Guid vehicleId, string title, string sourceKey, int priority, DateTimeOffset dueAtUtc, bool immobilizesVehicle)
    {
        if (organizationId == Guid.Empty || vehicleId == Guid.Empty) throw new ArgumentException("Organization and vehicle are required.");
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 160) throw new ArgumentException("A title of up to 160 characters is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(sourceKey) || sourceKey.Trim().Length > 160) throw new ArgumentException("A source key is required.", nameof(sourceKey));
        if (priority is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(priority));
        OrganizationId = organizationId; VehicleId = vehicleId; Title = title.Trim(); SourceKey = sourceKey.Trim(); Priority = priority;
        DueAtUtc = dueAtUtc.ToUniversalTime(); ImmobilizesVehicle = immobilizesVehicle; Status = MaintenanceWorkOrderStatus.Open;
    }
    public Guid VehicleId { get; private init; }
    public string Title { get; private set; } = string.Empty;
    public string SourceKey { get; private init; } = string.Empty;
    public int Priority { get; private set; }
    public DateTimeOffset DueAtUtc { get; private set; }
    public DateTimeOffset? ScheduledStartUtc { get; private set; }
    public DateTimeOffset? ScheduledEndUtc { get; private set; }
    public bool ImmobilizesVehicle { get; private set; }
    public MaintenanceWorkOrderStatus Status { get; private set; }
    public decimal LaborCost { get; private set; }
    public decimal PartsCost { get; private set; }
    public string CurrencyCode { get; private set; } = "EUR";
    public string? SupplierName { get; private set; }
    public string? PartsDescription { get; private set; }
    public string? TransitionReason { get; private set; }
    public Guid? AttachmentMediaAssetId { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public long RowVersion { get; private set; }
    public decimal TotalCost => LaborCost + PartsCost;
    public bool IsVehicleUnavailable => ImmobilizesVehicle && Status is not MaintenanceWorkOrderStatus.Completed and not MaintenanceWorkOrderStatus.Cancelled;

    public void Schedule(DateTimeOffset startUtc, DateTimeOffset endUtc, string reason)
    {
        if (endUtc <= startUtc) throw new ArgumentException("Scheduled end must be later than start.", nameof(endUtc));
        if (Status is MaintenanceWorkOrderStatus.Completed or MaintenanceWorkOrderStatus.Cancelled) throw new InvalidOperationException("Closed work orders cannot be scheduled.");
        ScheduledStartUtc = startUtc.ToUniversalTime(); ScheduledEndUtc = endUtc.ToUniversalTime(); Status = MaintenanceWorkOrderStatus.Scheduled; TransitionReason = Required(reason); RowVersion++;
    }
    public void Complete(string reason, DateTimeOffset completedAtUtc)
    {
        if (Status is MaintenanceWorkOrderStatus.Completed or MaintenanceWorkOrderStatus.Cancelled) throw new InvalidOperationException("Work order is already closed.");
        Status = MaintenanceWorkOrderStatus.Completed; CompletedAtUtc = completedAtUtc.ToUniversalTime(); TransitionReason = Required(reason); RowVersion++;
    }
    public void SetCost(decimal laborCost, decimal partsCost, string currencyCode, string? supplierName, string? partsDescription, Guid? attachmentMediaAssetId)
    {
        if (laborCost < 0 || partsCost < 0) throw new ArgumentOutOfRangeException(nameof(laborCost));
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Trim().Length != 3) throw new ArgumentException("Currency must be an ISO 4217 code.", nameof(currencyCode));
        LaborCost = decimal.Round(laborCost, 2, MidpointRounding.AwayFromZero); PartsCost = decimal.Round(partsCost, 2, MidpointRounding.AwayFromZero); CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        SupplierName = Trim(supplierName, 160); PartsDescription = Trim(partsDescription, 500); AttachmentMediaAssetId = attachmentMediaAssetId; RowVersion++;
    }
    private static string Required(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A transition reason is required.") : value.Trim();
    private static string? Trim(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > max ? throw new ArgumentException("Value is too long.") : value.Trim();
}
