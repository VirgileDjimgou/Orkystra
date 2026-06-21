using Orkystra.Domain.Common;

namespace Orkystra.Domain.ValueObjects;

public readonly record struct Money
{
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Result<Money> Create(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return Result.Failure<Money>(DomainErrors.Required(nameof(currency)));
        }

        if (currency.Length != 3 || !currency.All(char.IsLetter))
        {
            return Result.Failure<Money>(DomainErrors.InvalidValue(nameof(currency), "currency must be a 3-letter ISO code"));
        }

        return Result.Success(new Money(amount, currency.ToUpperInvariant()));
    }

    public override string ToString() => $"{Currency} {Amount:0.00}";
}
