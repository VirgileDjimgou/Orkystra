namespace Orkystra.Domain.Simulation.Services;

public sealed class SyntheticWorldGenerator
{
    public SyntheticWorldState Generate(SimulationSeed seed, int warehouseCount, int orderCount, int truckCount)
    {
        var random = new SeededRandomSource(seed.Value);

        var warehouses = Enumerable.Range(1, warehouseCount)
            .Select(index => new SyntheticWarehouseDefinition(
                WarehouseReference: $"WH-{index:D2}",
                Name: $"Warehouse {index}",
                ZoneCount: random.Next(2, 6),
                DockCount: random.Next(2, 10)))
            .ToArray();

        var orders = Enumerable.Range(1, orderCount)
            .Select(index => new SyntheticOrderDefinition(
                OrderReference: $"ORD-{index:D4}",
                Priority: random.Next(1, 4),
                TotalQuantity: Math.Round(random.NextDecimal(1m, 50m), 2)))
            .ToArray();

        var trucks = Enumerable.Range(1, truckCount)
            .Select(index => new SyntheticTruckDefinition(
                TruckReference: $"TRK-{index:D3}",
                CapacityKilograms: Math.Round(random.NextDecimal(500m, 5000m), 2),
                HomeDepot: $"DEPOT-{random.Next(1, 4):D2}"))
            .ToArray();

        return new SyntheticWorldState(warehouses, orders, trucks);
    }
}
