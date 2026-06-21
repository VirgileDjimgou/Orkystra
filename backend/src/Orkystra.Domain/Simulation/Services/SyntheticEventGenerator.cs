namespace Orkystra.Domain.Simulation.Services;

public sealed class SyntheticEventGenerator
{
    private static readonly (string EventType, string ReasonCode)[] Templates =
    [
        ("TruckBreakdown", "breakdown"),
        ("DockBlockage", "dock_blockage"),
        ("StockShortage", "stock_shortage"),
        ("DemandSpike", "demand_spike"),
        ("WeatherDisruption", "weather"),
        ("DriverUnavailable", "driver_unavailable"),
        ("RouteClosure", "route_closure"),
        ("LateInboundDelivery", "late_inbound")
    ];

    public IReadOnlyCollection<InjectedSimulationEvent> GenerateDisruptions(
        SimulationSeed seed,
        int count,
        IReadOnlyCollection<string> aggregateReferences)
    {
        if (aggregateReferences.Count == 0)
        {
            return [];
        }

        var references = aggregateReferences.ToArray();
        var random = new SeededRandomSource(seed.Value);
        var events = new List<InjectedSimulationEvent>(count);

        for (var index = 0; index < count; index++)
        {
            var template = Templates[random.Next(0, Templates.Length)];
            var aggregateReference = references[random.Next(0, references.Length)];
            var severity = random.Next(1, 6);

            events.Add(new InjectedSimulationEvent(
                template.EventType,
                aggregateReference,
                template.ReasonCode,
                severity));
        }

        return events;
    }
}
