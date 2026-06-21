using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Common;
using Orkystra.Domain.Identities;
using Orkystra.Domain.Simulation.Events;

namespace Orkystra.Domain.Simulation;

public sealed class Scenario : AggregateRoot<ScenarioId>
{
    private readonly List<InjectedSimulationEvent> _injectedEvents = [];

    private Scenario(
        ScenarioId id,
        TenantId tenantId,
        string name,
        SimulationSeed seed,
        SimulationClock clock)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Seed = seed;
        Clock = clock;
    }

    public TenantId TenantId { get; }

    public string Name { get; }

    public SimulationSeed Seed { get; }

    public SimulationClock Clock { get; }

    public ScenarioStatus Status { get; private set; } = ScenarioStatus.Draft;

    public IReadOnlyCollection<InjectedSimulationEvent> InjectedEvents => _injectedEvents.AsReadOnly();

    public static Result<Scenario> Create(
        ScenarioId scenarioId,
        TenantId tenantId,
        string name,
        SimulationSeed seed,
        DateTimeOffset startsAt,
        decimal speedMultiplier = 1m)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Scenario>(DomainErrors.Required(nameof(name)));
        }

        var clockResult = SimulationClock.Create(startsAt, speedMultiplier);
        if (clockResult.IsFailure)
        {
            return Result.Failure<Scenario>(clockResult.Error);
        }

        return Result.Success(new Scenario(scenarioId, tenantId, name.Trim(), seed, clockResult.Value));
    }

    public Result Start()
    {
        if (Status != ScenarioStatus.Draft)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(Status), "scenario can only start from draft"));
        }

        Status = ScenarioStatus.Running;
        RaiseDomainEvent(new ScenarioStarted(Id, Name, Seed.Value));

        return Result.Success();
    }

    public Result Pause()
    {
        if (Status != ScenarioStatus.Running)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(Status), "scenario can only pause while running"));
        }

        Status = ScenarioStatus.Paused;
        RaiseDomainEvent(new ScenarioPaused(Id, Clock.CurrentTime));

        return Result.Success();
    }

    public Result Resume()
    {
        if (Status != ScenarioStatus.Paused)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(Status), "scenario can only resume while paused"));
        }

        Status = ScenarioStatus.Running;
        RaiseDomainEvent(new ScenarioResumed(Id, Clock.CurrentTime));

        return Result.Success();
    }

    public Result Advance(TimeSpan step)
    {
        if (Status != ScenarioStatus.Running)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(Status), "scenario can only advance while running"));
        }

        var advanceResult = Clock.Advance(step);
        if (advanceResult.IsFailure)
        {
            return advanceResult;
        }

        RaiseDomainEvent(new TimeAdvanced(Id, Clock.CurrentTime, step.Ticks));
        return Result.Success();
    }

    public Result InjectRandomEvent(InjectedSimulationEvent injectedEvent)
    {
        if (Status is ScenarioStatus.Completed or ScenarioStatus.Draft)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(Status), "scenario must be active before random events can be injected"));
        }

        _injectedEvents.Add(injectedEvent);
        RaiseDomainEvent(new RandomEventInjected(
            Id,
            injectedEvent.EventType,
            injectedEvent.AggregateReference,
            injectedEvent.ReasonCode,
            injectedEvent.Severity));

        return Result.Success();
    }

    public Result Complete()
    {
        if (Status == ScenarioStatus.Completed)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(Status), "scenario is already completed"));
        }

        Status = ScenarioStatus.Completed;
        RaiseDomainEvent(new ScenarioCompleted(Id, Clock.CurrentTime));

        return Result.Success();
    }
}
