namespace FleetOps.Core.Modules.Demo;

public sealed record DemoRoutePoint(double Latitude, double Longitude);

public sealed record DemoRoute(string Id, IReadOnlyList<DemoRoutePoint> Points)
{
    public DemoRoutePoint Interpolate(double progress)
    {
        if (Points.Count < 2) throw new InvalidOperationException("A demo route requires at least two points.");
        var bounded = Math.Clamp(progress, 0, 1);
        var scaled = bounded * (Points.Count - 1);
        var index = Math.Min((int)scaled, Points.Count - 2);
        var fraction = scaled - index;
        var start = Points[index];
        var end = Points[index + 1];
        return new(start.Latitude + (end.Latitude - start.Latitude) * fraction, start.Longitude + (end.Longitude - start.Longitude) * fraction);
    }
}
