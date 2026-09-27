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
    double SpeedMultiplier);

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
    long SequenceNumber);

public interface IDemoTelemetryEmitter
{
    Task EmitAsync(DemoTelemetryEvent telemetry, CancellationToken cancellationToken);
}
