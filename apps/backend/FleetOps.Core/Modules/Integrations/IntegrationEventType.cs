namespace FleetOps.Core.Modules.Integrations;

public static class IntegrationEventType
{
    public const string FleetVehicleCreated = "fleet.vehicle.created";
    public const string AlertOpened = "alerts.opened";
    public const string MissionStatusChanged = "dispatch.mission.status-changed";

    public static readonly string[] All =
    [
        FleetVehicleCreated,
        AlertOpened,
        MissionStatusChanged,
    ];
}
