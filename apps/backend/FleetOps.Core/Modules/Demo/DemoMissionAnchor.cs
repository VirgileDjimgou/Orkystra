namespace FleetOps.Core.Modules.Demo;

public static class DemoMissionAnchor
{
    public static string Address(DemoRoute route, int anchorIndex) => $"demo-route:{route.Id}:{anchorIndex}";

    public static DemoRoutePoint? Resolve(string address)
    {
        var parts = address.Split(':');
        if (parts.Length != 3 || parts[0] != "demo-route" || !int.TryParse(parts[2], out var index)) return null;
        var route = new DemoScenarioCatalog().GetByKind(DemoScenarioKind.NormalShift).Routes.FirstOrDefault(item => item.Id == parts[1]);
        return route is not null && index >= 0 && index < route.Points.Count - 1 ? route.Points[index] : null;
    }
}
