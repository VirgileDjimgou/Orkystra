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
            var variation = 0.9 + ((state.Seed + (long)index) % 10 + 10) % 10 / 100d;
            var movement = DemoRouteMotion.Sample(route, state.MotionSeconds, scenario.NominalSpeedKph * variation);
            var point = movement.Position;
            var eventId = $"demo-{state.Seed}-{index:D2}-{state.Tick:D10}";
            await telemetryEmitter.EmitAsync(new(
                organizationId,
                binding.VehicleId,
                binding.DeviceId,
                eventId,
                state.LogicalUtc,
                point.Latitude,
                point.Longitude,
                movement.SpeedKph,
                movement.HeadingDegrees,
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

}
