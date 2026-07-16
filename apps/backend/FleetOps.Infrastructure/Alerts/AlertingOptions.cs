namespace FleetOps.Infrastructure.Alerts;

public sealed class AlertingOptions
{
    public const string SectionName = "Alerting";

    public int DocumentDueSoonDays { get; set; } = 30;
    public int MaintenanceDueSoonDays { get; set; } = 14;
    public int MaintenanceDueSoonKilometers { get; set; } = 1_000;
    public int InactiveVehicleAfterHours { get; set; } = 24;
    public int WorkerScanIntervalMinutes { get; set; } = 15;
}
