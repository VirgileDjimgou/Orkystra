namespace FleetOps.Core.Modules.Demo;

public sealed class DemoScenarioCatalog : IDemoScenarioRepository
{
    private static readonly IReadOnlyList<DemoRoute> StuttgartRoutes =
    [
        new("neckar-logistics", [new(48.7758, 9.1829), new(48.7795, 9.1968), new(48.7839, 9.2134), new(48.7931, 9.2218), new(48.8012, 9.2258)]),
        new("airport-corridor", [new(48.6899, 9.2219), new(48.7048, 9.2075), new(48.7215, 9.1935), new(48.7422, 9.1850), new(48.7647, 9.1782)]),
        new("west-industrial", [new(48.7741, 9.1757), new(48.7702, 9.1519), new(48.7757, 9.1285), new(48.7884, 9.1138), new(48.8033, 9.1041)]),
        new("fellbach-loop", [new(48.8066, 9.2761), new(48.8179, 9.2654), new(48.8257, 9.2470), new(48.8184, 9.2293), new(48.8066, 9.2761)])
    ];

    private static readonly IReadOnlyList<DemoScenarioDefinition> Definitions =
    [
        new(DemoScenarioKind.NormalShift, "NORMAL_SHIFT", StuttgartRoutes, 42),
        new(DemoScenarioKind.LateDelivery, "LATE_DELIVERY", StuttgartRoutes, 28),
        new(DemoScenarioKind.VehicleIssue, "VEHICLE_ISSUE", StuttgartRoutes, 18),
        new(DemoScenarioKind.DriverConnectivityLoss, "DRIVER_CONNECTIVITY_LOSS", StuttgartRoutes, 38, 5),
        new(DemoScenarioKind.ComplianceWarning, "COMPLIANCE_WARNING", StuttgartRoutes, 34)
    ];

    public IReadOnlyList<DemoScenarioDefinition> List() => Definitions;

    public DemoScenarioDefinition GetByKind(DemoScenarioKind kind) =>
        Definitions.Single(definition => definition.Kind == kind);
}
