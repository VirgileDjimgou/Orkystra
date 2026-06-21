using Orkystra.Domain.Common;

namespace Orkystra.Domain.ValueObjects;

public readonly record struct Quantity
{
    private Quantity(decimal value)
    {
        Value = value;
    }

    public decimal Value { get; }

    public static Result<Quantity> Create(decimal value) =>
        value < 0
            ? Result.Failure<Quantity>(DomainErrors.InvalidValue(nameof(Quantity), "value must be zero or greater"))
            : Result.Success(new Quantity(value));

    public static Quantity Zero => new(0m);
}
