using Orkystra.Domain.Identities;
using Orkystra.Domain.Simulation;
using Orkystra.Domain.Simulation.Events;
using Orkystra.Domain.Simulation.Services;

namespace Orkystra.Domain.Tests;

public sealed class SimulationTests
{
    [Fact]
    public void Scenario_StartPauseResumeAdvanceComplete_Succeeds()
    {
        var scenario = CreateScenario();

        var start = scenario.Start();
        var pause = scenario.Pause();
        var resume = scenario.Resume();
        var advance = scenario.Advance(TimeSpan.FromMinutes(5));
        var complete = scenario.Complete();

        Assert.True(start.IsSuccess);
        Assert.True(pause.IsSuccess);
        Assert.True(resume.IsSuccess);
        Assert.True(advance.IsSuccess);
        Assert.True(complete.IsSuccess);
        Assert.Equal(ScenarioStatus.Completed, scenario.Status);
        Assert.Contains(scenario.DomainEvents, domainEvent => domainEvent is ScenarioStarted);
        Assert.Contains(scenario.DomainEvents, domainEvent => domainEvent is ScenarioPaused);
        Assert.Contains(scenario.DomainEvents, domainEvent => domainEvent is ScenarioResumed);
        Assert.Contains(scenario.DomainEvents, domainEvent => domainEvent is TimeAdvanced);
        Assert.Contains(scenario.DomainEvents, domainEvent => domainEvent is ScenarioCompleted);
    }

    [Fact]
    public void Scenario_AdvanceFailsWhenNotRunning()
    {
        var scenario = CreateScenario();

        var result = scenario.Advance(TimeSpan.FromMinutes(1));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Scenario_InjectRandomEventAddsTraceableEvent()
    {
        var scenario = CreateScenario();
        scenario.Start();

        var result = scenario.InjectRandomEvent(new InjectedSimulationEvent("DockBlockage", "D-01", "dock_blockage", 3));

        Assert.True(result.IsSuccess);
        Assert.Single(scenario.InjectedEvents);
        Assert.Contains(scenario.DomainEvents, domainEvent => domainEvent is RandomEventInjected injected && injected.InjectedEventType == "DockBlockage");
    }

    [Fact]
    public void SyntheticWorldGenerator_IsDeterministicForSameSeed()
    {
        var generator = new SyntheticWorldGenerator();
        var seed = SimulationSeed.Create(42).Value;

        var first = generator.Generate(seed, warehouseCount: 2, orderCount: 3, truckCount: 2);
        var second = generator.Generate(seed, warehouseCount: 2, orderCount: 3, truckCount: 2);

        Assert.Equal(first.Warehouses, second.Warehouses);
        Assert.Equal(first.Orders, second.Orders);
        Assert.Equal(first.Trucks, second.Trucks);
    }

    [Fact]
    public void SyntheticWorldGenerator_DiffersForDifferentSeeds()
    {
        var generator = new SyntheticWorldGenerator();

        var first = generator.Generate(SimulationSeed.Create(42).Value, warehouseCount: 1, orderCount: 2, truckCount: 1);
        var second = generator.Generate(SimulationSeed.Create(43).Value, warehouseCount: 1, orderCount: 2, truckCount: 1);

        Assert.False(
            first.Warehouses.SequenceEqual(second.Warehouses) &&
            first.Orders.SequenceEqual(second.Orders) &&
            first.Trucks.SequenceEqual(second.Trucks));
    }

    [Fact]
    public void SyntheticEventGenerator_IsDeterministicForSameSeed()
    {
        var generator = new SyntheticEventGenerator();
        var references = new[] { "WH-01", "TRK-001", "D-01" };

        var first = generator.GenerateDisruptions(SimulationSeed.Create(7).Value, 5, references);
        var second = generator.GenerateDisruptions(SimulationSeed.Create(7).Value, 5, references);

        Assert.Equal(first, second);
    }

    [Fact]
    public void SimulationClock_AppliesSpeedMultiplierDuringAdvance()
    {
        var start = new DateTimeOffset(2026, 6, 20, 8, 0, 0, TimeSpan.Zero);
        var clock = SimulationClock.Create(start, 10m).Value;

        var result = clock.Advance(TimeSpan.FromMinutes(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(start.AddMinutes(10), clock.CurrentTime);
    }

    private static Scenario CreateScenario()
    {
        return Scenario.Create(
            ScenarioId.New(),
            TenantId.New(),
            "Morning Shift",
            SimulationSeed.Create(42).Value,
            new DateTimeOffset(2026, 6, 20, 8, 0, 0, TimeSpan.Zero),
            speedMultiplier: 1m).Value;
    }
}
