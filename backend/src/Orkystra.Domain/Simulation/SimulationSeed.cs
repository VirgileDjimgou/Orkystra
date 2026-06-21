using Orkystra.Domain.Common;

namespace Orkystra.Domain.Simulation;

public readonly record struct SimulationSeed
{
    private SimulationSeed(int value)
    {
        Value = value;
    }

    public int Value { get; }

    public static Result<SimulationSeed> Create(int value) =>
        value < 0
            ? Result.Failure<SimulationSeed>(DomainErrors.InvalidValue(nameof(SimulationSeed), "seed must be zero or greater"))
            : Result.Success(new SimulationSeed(value));
}
