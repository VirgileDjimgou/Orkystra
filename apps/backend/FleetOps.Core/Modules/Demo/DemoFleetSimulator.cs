using System.Security.Cryptography;
using System.Text;

namespace FleetOps.Core.Modules.Demo;

public sealed class DemoFleetSimulator(
    IDemoScenarioEngine engine,
    IDemoScenarioRepository scenarios,
    IDemoTelemetryEmitter telemetryEmitter)
{
    public async Task<int> TickAsync(
        Guid organizationId,
        IReadOnlyList<DemoVehicleBinding> vehicles,
        TimeSpan elapsed,
        CancellationToken cancellationToken)
    {
        ValidateBindings(organizationId, vehicles);
        engine.Advance(elapsed);
        var state = engine.State;
        if (state.Status != DemoScenarioStatus.Running)
        {
            return 0;
        }

        var scenario = scenarios.GetByKind(state.Scenario);
        var emitted = 0;
        for (var index = 0; index < vehicles.Count; index++)
        {
            if (scenario.ConnectivityDropEveryTicks > 0
                && index == vehicles.Count - 1
                && state.Tick % scenario.ConnectivityDropEveryTicks == 0)
            {
                continue;
            }

            var binding = vehicles[index];
            var route = scenario.Routes[index % scenario.Routes.Count];
            var seedOffset = PositiveModulo(state.Seed * 31L + index * 97L, 10_000) / 10_000d;
            var progress = (seedOffset + state.Tick * (0.004 + index * 0.0001)) % 1d;
            var point = route.Interpolate(progress);
            var next = route.Interpolate(Math.Min(1, progress + 0.001));
            var heading = CalculateHeading(point, next);
            var eventId = $"demo-{state.Seed}-{index:D2}-{state.Tick:D10}";
            await telemetryEmitter.EmitAsync(new(
                organizationId,
                binding.VehicleId,
                binding.DeviceId,
                eventId,
                state.LogicalUtc,
                point.Latitude,
                point.Longitude,
                scenario.NominalSpeedKph + DeterministicJitter(state.Seed, index, state.Tick),
                heading,
                state.Tick), cancellationToken);
            emitted++;
        }

        return emitted;
    }

    public static void ValidateBindings(Guid organizationId, IReadOnlyList<DemoVehicleBinding> vehicles)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("A synthetic organization is required.", nameof(organizationId));
        if (vehicles.Count is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(vehicles), "A simulation batch must contain 1 to 20 vehicles.");
        if (vehicles.Any(vehicle => vehicle.VehicleId == Guid.Empty || string.IsNullOrWhiteSpace(vehicle.DeviceId)))
            throw new ArgumentException("Every demo vehicle requires a vehicle and device identifier.", nameof(vehicles));
        if (vehicles.Select(vehicle => vehicle.VehicleId).Distinct().Count() != vehicles.Count
            || vehicles.Select(vehicle => vehicle.DeviceId).Distinct(StringComparer.Ordinal).Count() != vehicles.Count)
            throw new ArgumentException("Demo vehicle and device identifiers must be unique.", nameof(vehicles));
    }

    private static int PositiveModulo(long value, int divisor) => (int)((value % divisor + divisor) % divisor);

    private static double DeterministicJitter(int seed, int vehicleIndex, long tick)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}:{vehicleIndex}:{tick}"));
        return (bytes[0] / 255d - 0.5d) * 4d;
    }

    private static double CalculateHeading(DemoRoutePoint from, DemoRoutePoint to)
    {
        var latitude1 = DegreesToRadians(from.Latitude);
        var latitude2 = DegreesToRadians(to.Latitude);
        var longitudeDelta = DegreesToRadians(to.Longitude - from.Longitude);
        var y = Math.Sin(longitudeDelta) * Math.Cos(latitude2);
        var x = Math.Cos(latitude1) * Math.Sin(latitude2) - Math.Sin(latitude1) * Math.Cos(latitude2) * Math.Cos(longitudeDelta);
        return (RadiansToDegrees(Math.Atan2(y, x)) + 360d) % 360d;
    }

    private static double DegreesToRadians(double value) => value * Math.PI / 180d;
    private static double RadiansToDegrees(double value) => value * 180d / Math.PI;
}
