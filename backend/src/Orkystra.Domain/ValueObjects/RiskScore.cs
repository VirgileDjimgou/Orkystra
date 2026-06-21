using Orkystra.Domain.Common;

namespace Orkystra.Domain.ValueObjects;

public readonly record struct RiskScore
{
    private RiskScore(decimal value)
    {
        Value = value;
    }

    public decimal Value { get; }

    public static Result<RiskScore> Create(decimal value) =>
        value is < 0 or > 1
            ? Result.Failure<RiskScore>(DomainErrors.InvalidValue(nameof(RiskScore), "value must be between 0 and 1"))
            : Result.Success(new RiskScore(value));
}
