using Orkystra.Domain.ValueObjects;

namespace Orkystra.Domain.Tests;

public sealed class ValueObjectTests
{
    [Fact]
    public void Quantity_CreateFailsForNegativeValue()
    {
        var result = Quantity.Create(-1m);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Money_CreateNormalizesCurrencyCode()
    {
        var result = Money.Create(125.50m, "eur");

        Assert.True(result.IsSuccess);
        Assert.Equal("EUR", result.Value.Currency);
    }

    [Fact]
    public void Money_CreateFailsForInvalidCurrency()
    {
        var result = Money.Create(10m, "EURO");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void GeoCoordinate_CreateFailsWhenLatitudeIsOutOfRange()
    {
        var result = GeoCoordinate.Create(91m, 2m);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void TimeWindow_CreateFailsWhenEndIsBeforeStart()
    {
        var start = new DateTimeOffset(2026, 6, 20, 8, 0, 0, TimeSpan.Zero);
        var end = start.AddMinutes(-1);

        var result = TimeWindow.Create(start, end);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void RiskScore_CreateSucceedsAtUpperBound()
    {
        var result = RiskScore.Create(1m);

        Assert.True(result.IsSuccess);
        Assert.Equal(1m, result.Value.Value);
    }
}
