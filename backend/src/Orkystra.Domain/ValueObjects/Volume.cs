using Orkystra.Domain.Common;

namespace Orkystra.Domain.ValueObjects;

public readonly record struct Volume
{
    private Volume(decimal cubicMeters)
    {
        CubicMeters = cubicMeters;
    }

    public decimal CubicMeters { get; }

    public static Result<Volume> Create(decimal cubicMeters) =>
        cubicMeters < 0
            ? Result.Failure<Volume>(DomainErrors.InvalidValue(nameof(Volume), "cubic meters must be zero or greater"))
            : Result.Success(new Volume(cubicMeters));

    public override string ToString() => $"{CubicMeters:0.###} m3";
}
