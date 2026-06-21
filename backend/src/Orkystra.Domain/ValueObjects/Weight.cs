using Orkystra.Domain.Common;

namespace Orkystra.Domain.ValueObjects;

public readonly record struct Weight
{
    private Weight(decimal kilograms)
    {
        Kilograms = kilograms;
    }

    public decimal Kilograms { get; }

    public static Result<Weight> Create(decimal kilograms) =>
        kilograms < 0
            ? Result.Failure<Weight>(DomainErrors.InvalidValue(nameof(Weight), "kilograms must be zero or greater"))
            : Result.Success(new Weight(kilograms));

    public override string ToString() => $"{Kilograms:0.###} kg";
}
