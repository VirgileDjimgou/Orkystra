namespace FleetOps.Infrastructure.Alerts;

public sealed record AlertScanResult(
    int CreatedAlerts,
    int RefreshedAlerts,
    int ResolvedAlerts,
    int InAppNotifications,
    int EmailNotifications,
    int EmailFailures);
