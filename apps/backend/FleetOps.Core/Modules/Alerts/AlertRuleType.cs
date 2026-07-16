namespace FleetOps.Core.Modules.Alerts;

public enum AlertRuleType
{
    VehicleDocumentExpiry = 1,
    DriverDocumentExpiry = 2,
    VehicleMaintenanceByDate = 3,
    VehicleMaintenanceByMileage = 4,
    VehicleInactive = 5,
}
