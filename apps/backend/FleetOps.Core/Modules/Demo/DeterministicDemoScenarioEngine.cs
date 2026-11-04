namespace FleetOps.Core.Modules.Demo;

public sealed class DeterministicDemoClock(DateTimeOffset initialUtc) : IDemoClock
{
    public DateTimeOffset UtcNow { get; private set; } = initialUtc.ToUniversalTime();
    public void Advance(TimeSpan elapsed) => UtcNow = UtcNow.Add(elapsed);
    public void Reset(DateTimeOffset utcNow) => UtcNow = utcNow.ToUniversalTime();
}

public sealed class DeterministicDemoScenarioEngine(IDemoClock clock) : IDemoScenarioEngine
{
    private DateTimeOffset initialUtc = clock.UtcNow;
    public DemoScenarioState State { get; private set; } = new(DemoScenarioKind.NormalShift, DemoScenarioStatus.Stopped, 0, clock.UtcNow, 1, 0);
    public void Start(DemoScenarioKind scenario, int seed, double speedMultiplier = 1)
    {
        if (speedMultiplier is < 0.25 or > 20) throw new ArgumentOutOfRangeException(nameof(speedMultiplier));
        clock.Reset(initialUtc);
        State = new(scenario, DemoScenarioStatus.Running, seed, clock.UtcNow, speedMultiplier, 0);
    }
    public void Pause() => State = State with { Status = DemoScenarioStatus.Paused };
    public void ContinueScenario() => State = State with { Status = DemoScenarioStatus.Running };
    public void Reset() { clock.Reset(initialUtc); State = State with { Status = DemoScenarioStatus.Stopped, LogicalUtc = clock.UtcNow, Tick = 0, MotionSeconds = 0 }; }
    public void Advance(TimeSpan elapsed)
    {
        if (State.Status != DemoScenarioStatus.Running) return;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(elapsed, TimeSpan.Zero);
        clock.Advance(TimeSpan.FromTicks((long)(elapsed.Ticks * State.SpeedMultiplier)));
        State = State with { LogicalUtc = clock.UtcNow, Tick = State.Tick + 1, MotionSeconds = State.MotionSeconds + elapsed.TotalSeconds * State.SpeedMultiplier };
    }

    public DemoScenarioSnapshot Capture() => new(State, initialUtc, 2);

    public void Restore(DemoScenarioSnapshot snapshot)
    {
        if (snapshot.MotionVersion != 2) throw new ArgumentException("Legacy motion snapshot requires a canonical synthetic tracking reset.", nameof(snapshot));
        if (!double.IsFinite(snapshot.State.MotionSeconds) || snapshot.State.MotionSeconds < 0) throw new ArgumentOutOfRangeException(nameof(snapshot));
        if (snapshot.State.SpeedMultiplier is < 0.25 or > 20) throw new ArgumentOutOfRangeException(nameof(snapshot));
        if (snapshot.State.Tick < 0) throw new ArgumentOutOfRangeException(nameof(snapshot));
        initialUtc = snapshot.InitialUtc.ToUniversalTime();
        clock.Reset(snapshot.State.LogicalUtc);
        State = snapshot.State with { LogicalUtc = clock.UtcNow };
    }
}
