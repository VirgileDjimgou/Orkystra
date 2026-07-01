using Orkystra.Contracts.Warehouse;

namespace Orkystra.Api.ControlTower;

public sealed class WarehouseWorkbenchService
{
    private readonly WarehouseProjectionService _warehouseProjectionService;

    public WarehouseWorkbenchService(WarehouseProjectionService warehouseProjectionService)
    {
        _warehouseProjectionService = warehouseProjectionService;
    }

    public async ValueTask<WarehouseWorkbenchReadModel> BuildAsync(CancellationToken cancellationToken = default)
    {
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var warehouses = await _warehouseProjectionService.ListAsync(cancellationToken);
        var items = new List<WarehouseWorkbenchItemReadModel>();

        foreach (var warehouse in warehouses)
        {
            var detail = await _warehouseProjectionService.GetByIdAsync(warehouse.WarehouseId, cancellationToken);
            if (detail is null)
            {
                continue;
            }

            var occupancyRatio = detail.SlotCount > 0
                ? (double)detail.StoredPalletCount / detail.SlotCount
                : 0.0;

            if (occupancyRatio > 0.85)
            {
                items.Add(BuildItem(
                    $"warehouse-occupancy-{detail.WarehouseId:D}",
                    "Warning",
                    "Occupancy",
                    $"{detail.Name} occupancy is critical",
                    $"{detail.StoredPalletCount} of {detail.SlotCount} slots occupied ({occupancyRatio:P0}). Consider expanding storage or redistributing inventory.",
                    detail.WarehouseId.ToString(),
                    detail.Name,
                    null,
                    "focus-warehouse",
                    "Focus warehouse",
                    [$"{detail.StoredPalletCount} pallets stored", $"{detail.SlotCount} total slots", $"Occupancy: {occupancyRatio:P0}"]));
            }
            else if (occupancyRatio > 0.70)
            {
                items.Add(BuildItem(
                    $"warehouse-occupancy-{detail.WarehouseId:D}",
                    "Info",
                    "Occupancy",
                    $"{detail.Name} occupancy is elevated",
                    $"{detail.StoredPalletCount} of {detail.SlotCount} slots occupied ({occupancyRatio:P0}). Monitor capacity headroom.",
                    detail.WarehouseId.ToString(),
                    detail.Name,
                    null,
                    "focus-warehouse",
                    "Focus warehouse",
                    [$"{detail.StoredPalletCount} pallets stored", $"{detail.SlotCount} total slots", $"Occupancy: {occupancyRatio:P0}"]));
            }

            foreach (var zone in detail.Zones)
            {
                if (string.Equals(zone.Status, "Critical", StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(BuildItem(
                        $"zone-critical-{detail.WarehouseId:D}-{zone.Code}",
                        "Critical",
                        "Zone",
                        $"Zone {zone.Code} in {detail.Name} is critical",
                        zone.Description,
                        detail.WarehouseId.ToString(),
                        detail.Name,
                        zone.Code,
                        "focus-warehouse",
                        "Focus warehouse",
                        [$"Zone: {zone.Name}", $"Utilization: {zone.UtilizationPercent}%", $"PalletCount: {zone.PalletCount}", zone.ThroughputLabel]));
                }
                else if (string.Equals(zone.Status, "Watch", StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(BuildItem(
                        $"zone-watch-{detail.WarehouseId:D}-{zone.Code}",
                        "Warning",
                        "Zone",
                        $"Zone {zone.Code} in {detail.Name} needs attention",
                        zone.Description,
                        detail.WarehouseId.ToString(),
                        detail.Name,
                        zone.Code,
                        "focus-warehouse",
                        "Focus warehouse",
                        [$"Zone: {zone.Name}", $"Utilization: {zone.UtilizationPercent}%", $"PalletCount: {zone.PalletCount}", zone.ThroughputLabel]));
                }

                if (zone.UtilizationPercent > 85)
                {
                    items.Add(BuildItem(
                        $"zone-pressure-{detail.WarehouseId:D}-{zone.Code}",
                        "Warning",
                        "Pressure",
                        $"Zone {zone.Code} in {detail.Name} is under pressure",
                        $"Zone utilization is at {zone.UtilizationPercent}%, which exceeds the recommended threshold.",
                        detail.WarehouseId.ToString(),
                        detail.Name,
                        zone.Code,
                        "focus-warehouse",
                        "Focus warehouse",
                        [$"Utilization: {zone.UtilizationPercent}%", $"PalletCount: {zone.PalletCount}", zone.ThroughputLabel]));
                }
            }

            var occupiedDockCount = detail.Docks.Count(d => string.Equals(d.Status, "Occupied", StringComparison.OrdinalIgnoreCase));
            if (occupiedDockCount == detail.Docks.Count && detail.Docks.Count > 0)
            {
                items.Add(BuildItem(
                    $"dock-saturation-{detail.WarehouseId:D}",
                    "Warning",
                    "Dock",
                    $"All docks occupied at {detail.Name}",
                    $"All {detail.Docks.Count} dock(s) are currently occupied. No receiving capacity available for new inbound loads.",
                    detail.WarehouseId.ToString(),
                    detail.Name,
                    null,
                    "focus-warehouse",
                    "Focus warehouse",
                    [$"{occupiedDockCount} of {detail.Docks.Count} dock(s) occupied"]));
            }
            else if (occupiedDockCount >= detail.Docks.Count * 0.75 && detail.Docks.Count > 0)
            {
                items.Add(BuildItem(
                    $"dock-pressure-{detail.WarehouseId:D}",
                    "Info",
                    "Dock",
                    $"Dock pressure at {detail.Name}",
                    $"{occupiedDockCount} of {detail.Docks.Count} dock(s) are occupied. Monitor for saturation.",
                    detail.WarehouseId.ToString(),
                    detail.Name,
                    null,
                    "focus-warehouse",
                    "Focus warehouse",
                    [$"{occupiedDockCount} of {detail.Docks.Count} dock(s) occupied"]));
            }
        }

        var orderedItems = items
            .OrderByDescending(item => SeverityRank(item.Severity))
            .ThenBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();

        var groups = orderedItems
            .GroupBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var representative = group
                    .OrderByDescending(item => SeverityRank(item.Severity))
                    .First();
                var count = group.Count();

                return new WarehouseWorkbenchGroupReadModel(
                    group.Key.ToLowerInvariant().Replace(' ', '-'),
                    group.Key,
                    representative.Severity,
                    count,
                    count == 1
                        ? $"1 {group.Key.ToLowerInvariant()} exception is active."
                        : $"{count} {group.Key.ToLowerInvariant()} exceptions are active.",
                    representative.RecommendedAction,
                    representative.ActionLabel);
            })
            .OrderByDescending(group => SeverityRank(group.HighestSeverity))
            .ThenByDescending(group => group.Count)
            .ThenBy(group => group.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var summary = orderedItems.Length == 0
            ? "Warehouse workbench is clear right now."
            : $"{orderedItems.Length} warehouse signal(s) need operator review.";

        return new WarehouseWorkbenchReadModel(
            generatedAtUtc,
            orderedItems.Length,
            summary,
            groups,
            orderedItems);
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "Critical" => 3,
        "Warning" => 2,
        _ => 1
    };

    private static WarehouseWorkbenchItemReadModel BuildItem(
        string exceptionId,
        string severity,
        string category,
        string title,
        string detail,
        string? warehouseId,
        string? warehouseName,
        string? zoneCode,
        string recommendedAction,
        string actionLabel,
        IReadOnlyCollection<string> evidence) =>
        new(exceptionId, severity, category, title, detail, warehouseId, warehouseName, zoneCode, recommendedAction, actionLabel, evidence);
}
