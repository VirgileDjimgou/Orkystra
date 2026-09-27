namespace FleetOps.Core.Modules.Demo;

public sealed class DeterministicDemoClock(DateTimeOffset initialUtc) : IDemoClock
{
    public DateTimeOffset UtcNow { get; private set; } = initialUtc.ToUniversalTime();
    public void Advance(TimeSpan elapsed) => UtcNow = UtcNow.Add(elapsed);
    public void Reset(DateTimeOffset utcNow) => UtcNow = utcNow.ToUniversalTime();
}

public sealed class DeterministicDemoScenarioEngine(IDemoClock clock) : IDemoScenarioEngine
{
    private readonly DateTimeOffset initialUtc = clock.UtcNow;
    public DemoScenarioState State { get; private set; } = new(DemoScenarioKind.NormalShift, DemoScenarioStatus.Stopped, 0, clock.UtcNow, 1);
    public void Start(DemoScenarioKind scenario, int seed, double speedMultiplier = 1)
    {
        if (speedMultiplier is < 0.25 or > 20) throw new ArgumentOutOfRangeException(nameof(speedMultiplier));
        State = new(scenario, DemoScenarioStatus.Running, seed, clock.UtcNow, speedMultiplier);
    }
    public void Pause() => State = State with { Status = DemoScenarioStatus.Paused };
    public void ContinueScenario() => State = State with { Status = DemoScenarioStatus.Running };
    public void Reset() { clock.Reset(initialUtc); State = State with { Status = DemoScenarioStatus.Stopped, LogicalUtc = clock.UtcNow }; }
    public void Advance(TimeSpan elapsed)
    {
        if (State.Status != DemoScenarioStatus.Running) return;
        clock.Advance(TimeSpan.FromTicks((long)(elapsed.Ticks * State.SpeedMultiplier)));
        State = State with { LogicalUtc = clock.UtcNow };
    }
}
