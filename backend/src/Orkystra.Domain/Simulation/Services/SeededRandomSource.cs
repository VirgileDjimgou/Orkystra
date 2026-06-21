namespace Orkystra.Domain.Simulation.Services;

public sealed class SeededRandomSource
{
    private readonly Random _random;

    public SeededRandomSource(int seed)
    {
        _random = new Random(seed);
    }

    public int Next(int minValueInclusive, int maxValueExclusive) =>
        _random.Next(minValueInclusive, maxValueExclusive);

    public decimal NextDecimal(decimal minValueInclusive, decimal maxValueExclusive)
    {
        var sample = (decimal)_random.NextDouble();
        return minValueInclusive + ((maxValueExclusive - minValueInclusive) * sample);
    }
}
