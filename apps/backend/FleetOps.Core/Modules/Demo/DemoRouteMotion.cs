using FleetOps.Core.Modules.Tracking;

namespace FleetOps.Core.Modules.Demo;

public sealed record DemoMotionSample(DemoRoutePoint Position, double SpeedKph, double HeadingDegrees, int AnchorIndex, bool IsDwelling);

/// <summary>Stateless trajectory evaluated from logical time; restart and cadence do not change its path.</summary>
public static class DemoRouteMotion
{
    public const double DwellSeconds = 20;

    public static DemoMotionSample Sample(DemoRoute route, double seconds, double targetSpeedKph)
    {
        if (route.Points.Count < 3 || route.Points[0] != route.Points[^1])
            throw new ArgumentException("Motion requires a closed route.", nameof(route));
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (!double.IsFinite(targetSpeedKph) || targetSpeedKph <= 0) throw new ArgumentOutOfRangeException(nameof(targetSpeedKph));
        var lengths = Enumerable.Range(0, route.Points.Count - 1)
            .Select(i => TrackingGeometry.DistanceKm(route.Points[i].Latitude, route.Points[i].Longitude, route.Points[i + 1].Latitude, route.Points[i + 1].Longitude)).ToArray();
        if (lengths.Any(length => length <= 0)) throw new ArgumentException("Route segments must have positive length.", nameof(route));
        var durations = lengths.Select(length => Math.PI * length * 3600 / (2 * targetSpeedKph)).ToArray();
        var phase = seconds % durations.Sum(duration => duration + DwellSeconds);
        for (var i = 0; i < durations.Length; i++)
        {
            var incoming = i == 0 ? route.Points.Count - 2 : i - 1;
            if (phase < DwellSeconds)
                return new(route.Points[i], 0, Bearing(route.Points[incoming], route.Points[i]), i, true);
            phase -= DwellSeconds;
            if (phase < durations[i])
            {
                var fraction = phase / durations[i];
                var progress = (1 - Math.Cos(Math.PI * fraction)) / 2;
                var from = route.Points[i];
                var to = route.Points[i + 1];
                return new(new(from.Latitude + (to.Latitude - from.Latitude) * progress, from.Longitude + (to.Longitude - from.Longitude) * progress),
                    targetSpeedKph * Math.Sin(Math.PI * fraction), Bearing(from, to), i, false);
            }
            phase -= durations[i];
        }
        throw new InvalidOperationException("Unable to sample route phase.");
    }

    private static double Bearing(DemoRoutePoint from, DemoRoutePoint to)
    {
        var a = from.Latitude * Math.PI / 180;
        var b = to.Latitude * Math.PI / 180;
        var delta = (to.Longitude - from.Longitude) * Math.PI / 180;
        return (Math.Atan2(Math.Sin(delta) * Math.Cos(b), Math.Cos(a) * Math.Sin(b) - Math.Sin(a) * Math.Cos(b) * Math.Cos(delta)) * 180 / Math.PI + 360) % 360;
    }
}
