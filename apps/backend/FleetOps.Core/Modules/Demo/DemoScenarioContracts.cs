using FleetOps.Core.Modules.Dispatch;

namespace FleetOps.Core.Modules.Demo;

public enum DemoScenarioKind
{
    NormalShift,
    LateDelivery,
    VehicleIssue,
    DriverConnectivityLoss,
    ComplianceWarning
}

public enum DemoScenarioStatus { Stopped, Running, Paused }

public sealed record DemoScenarioState(
    DemoScenarioKind Scenario,
    DemoScenarioStatus Status,
    int Seed,
    DateTimeOffset LogicalUtc,
    double SpeedMultiplier,
    long Tick = 0);

public sealed record DemoVehicleBinding(Guid VehicleId, string DeviceId);
public sealed record DemoFleetDefinition(Guid OrganizationId, IReadOnlyList<DemoVehicleBinding> Vehicles);

public sealed record DemoScenarioDefinition(
    DemoScenarioKind Kind,
    string Code,
    IReadOnlyList<DemoRoute> Routes,
    double NominalSpeedKph,
    int ConnectivityDropEveryTicks = 0);

public sealed record DemoScenarioSnapshot(DemoScenarioState State, DateTimeOffset InitialUtc);

public interface IDemoClock
{
    DateTimeOffset UtcNow { get; }
    void Advance(TimeSpan elapsed);
    void Reset(DateTimeOffset utcNow);
}

public interface IDemoScenarioEngine
{
    DemoScenarioState State { get; }
    void Start(DemoScenarioKind scenario, int seed, double speedMultiplier = 1);
    void Pause();
    void ContinueScenario();
    void Reset();
    void Advance(TimeSpan elapsed);
    DemoScenarioSnapshot Capture();
    void Restore(DemoScenarioSnapshot snapshot);
}

public interface IDemoScenarioRepository
{
    IReadOnlyList<DemoScenarioDefinition> List();
    DemoScenarioDefinition GetByKind(DemoScenarioKind kind);
}

public sealed record DemoTelemetryEvent(
    Guid OrganizationId,
    Guid VehicleId,
    string DeviceId,
    string EventId,
    DateTimeOffset RecordedAtUtc,
    double Latitude,
    double Longitude,
    double SpeedKph,
    double HeadingDegrees,
    long SequenceNumber,
    double AccuracyMeters = 5,
    string Source = "demo-engine");

public interface IDemoTelemetryEmitter
{
    Task EmitAsync(DemoTelemetryEvent telemetry, CancellationToken cancellationToken);
}

public interface IDemoFleetSource
{
    Task<DemoFleetDefinition> LoadAsync(CancellationToken cancellationToken);
}

public sealed record DemoMissionAction(Guid MissionId, MissionStatus TargetStatus, long RowVersion);
public sealed record DemoMissionActionResult(Guid MissionId, MissionStatus Status, long RowVersion);

public interface IDemoMissionEmitter
{
    Task<DemoMissionActionResult> TransitionAsync(DemoMissionAction action, CancellationToken cancellationToken);
}

public interface IDemoScenarioStateStore
{
    Task<DemoScenarioSnapshot?> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(DemoScenarioSnapshot snapshot, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}
