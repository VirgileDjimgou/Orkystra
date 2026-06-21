using Orkystra.Domain.Common;

namespace Orkystra.Domain.Simulation;

public sealed class SimulationClock
{
    private SimulationClock(DateTimeOffset startsAt, decimal speedMultiplier)
    {
        CurrentTime = startsAt;
        SpeedMultiplier = speedMultiplier;
    }

    public DateTimeOffset CurrentTime { get; private set; }

    public decimal SpeedMultiplier { get; private set; }

    public static Result<SimulationClock> Create(DateTimeOffset startsAt, decimal speedMultiplier = 1m)
    {
        if (speedMultiplier <= 0m)
        {
            return Result.Failure<SimulationClock>(DomainErrors.InvalidValue(nameof(speedMultiplier), "speed multiplier must be greater than zero"));
        }

        return Result.Success(new SimulationClock(startsAt, speedMultiplier));
    }

    public Result Advance(TimeSpan step)
    {
        if (step <= TimeSpan.Zero)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(step), "step must be greater than zero"));
        }

        var scaledTicks = (long)(step.Ticks * SpeedMultiplier);
        CurrentTime = CurrentTime.AddTicks(scaledTicks);

        return Result.Success();
    }

    public Result SetSpeed(decimal speedMultiplier)
    {
        if (speedMultiplier <= 0m)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(speedMultiplier), "speed multiplier must be greater than zero"));
        }

        SpeedMultiplier = speedMultiplier;
        return Result.Success();
    }
}
