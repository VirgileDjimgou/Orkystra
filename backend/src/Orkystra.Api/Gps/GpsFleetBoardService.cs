using Orkystra.Api.ControlTower;
using Orkystra.Contracts.Connectors;
using Orkystra.Contracts.Transport;

namespace Orkystra.Api.Gps;

public sealed class GpsFleetBoardService
{
    private readonly GpsProjectionService _gpsProjectionService;
    private readonly TransportProjectionService _transportProjectionService;

    public GpsFleetBoardService(
        GpsProjectionService gpsProjectionService,
        TransportProjectionService transportProjectionService)
    {
        _gpsProjectionService = gpsProjectionService;
        _transportProjectionService = transportProjectionService;
    }

    public async ValueTask<GpsFleetBoardReadModel> BuildAsync(
        string tenantId = "local-demo-tenant",
        CancellationToken cancellationToken = default)
    {
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var positions = await _gpsProjectionService.ListAsync(cancellationToken);
        var routes = await _transportProjectionService.ListAsync(tenantId, cancellationToken);
        var routesByTruckReference = routes
            .GroupBy(route => route.TruckReference, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var boardPositions = positions
            .Select(position =>
            {
                routesByTruckReference.TryGetValue(position.TruckReference, out var route);
                var minutesSinceReading = Math.Max(
                    0,
                    (int)Math.Floor((generatedAtUtc - position.RecordedAt).TotalMinutes));
                var freshnessPosture = BuildFreshnessPosture(minutesSinceReading);
                var movementPosture = BuildMovementPosture(position.SpeedKph);
                var alertPosture = BuildAlertPosture(freshnessPosture, movementPosture, position.SpeedKph, route?.Status);

                return new GpsFleetPositionReadModel(
                    position.TruckId,
                    position.TruckReference,
                    route?.RouteId,
                    route?.Reference,
                    route?.Status,
                    position.Latitude,
                    position.Longitude,
                    position.SpeedKph,
                    position.RecordedAt,
                    minutesSinceReading,
                    freshnessPosture,
                    movementPosture,
                    alertPosture,
                    BuildAlertSummary(freshnessPosture, movementPosture, position.SpeedKph, route));
            })
            .OrderByDescending(position => AlertRank(position.AlertPosture))
            .ThenByDescending(position => position.MinutesSinceReading)
            .ThenBy(position => position.RouteReference ?? position.TruckReference, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var freshCount = boardPositions.Count(position => string.Equals(position.FreshnessPosture, "Fresh", StringComparison.Ordinal));
        var agingCount = boardPositions.Count(position => string.Equals(position.FreshnessPosture, "Aging", StringComparison.Ordinal));
        var staleCount = boardPositions.Count(position => string.Equals(position.FreshnessPosture, "Stale", StringComparison.Ordinal));
        var movingCount = boardPositions.Count(position => string.Equals(position.MovementPosture, "Moving", StringComparison.Ordinal));
        var idleCount = boardPositions.Count(position => string.Equals(position.MovementPosture, "Idle", StringComparison.Ordinal));
        var speedingCount = boardPositions.Count(position => position.SpeedKph >= 80m);
        var routeLinkedCount = boardPositions.Count(position => position.RouteId is not null);
        var focusPosition = boardPositions.FirstOrDefault();

        return new GpsFleetBoardReadModel(
            generatedAtUtc,
            boardPositions.Length,
            routeLinkedCount,
            freshCount,
            agingCount,
            staleCount,
            movingCount,
            idleCount,
            speedingCount,
            BuildSummary(boardPositions.Length, routeLinkedCount, staleCount, speedingCount, idleCount),
            focusPosition?.TruckReference,
            focusPosition?.RouteReference,
            BuildFocusSummary(focusPosition),
            boardPositions);
    }

    private static string BuildSummary(
        int positionCount,
        int routeLinkedCount,
        int staleCount,
        int speedingCount,
        int idleCount)
    {
        if (positionCount == 0)
        {
            return "No projected GPS positions are available yet. Publish telemetry to build the fleet board.";
        }

        var parts = new List<string>
        {
            $"{positionCount} projected GPS position{(positionCount == 1 ? string.Empty : "s")}",
            $"{routeLinkedCount} route-linked"
        };

        if (staleCount > 0)
        {
            parts.Add($"{staleCount} stale");
        }

        if (speedingCount > 0)
        {
            parts.Add($"{speedingCount} speeding");
        }

        if (idleCount > 0)
        {
            parts.Add($"{idleCount} idle");
        }

        return string.Join(", ", parts) + ".";
    }

    private static string BuildFocusSummary(GpsFleetPositionReadModel? focusPosition)
    {
        if (focusPosition is null)
        {
            return "Publish GPS telemetry to establish the first operator telemetry focus.";
        }

        if (string.Equals(focusPosition.AlertPosture, "Critical", StringComparison.Ordinal))
        {
            return $"{focusPosition.TruckReference} needs telemetry review first because the latest reading is stale or route-critical.";
        }

        if (string.Equals(focusPosition.AlertPosture, "Warning", StringComparison.Ordinal))
        {
            return $"{focusPosition.TruckReference} should be checked next because the latest telemetry suggests elevated route risk.";
        }

        return $"{focusPosition.TruckReference} is the healthiest current telemetry reference for the live fleet board.";
    }

    private static string BuildFreshnessPosture(int minutesSinceReading)
    {
        if (minutesSinceReading <= 5)
        {
            return "Fresh";
        }

        if (minutesSinceReading <= 15)
        {
            return "Aging";
        }

        return "Stale";
    }

    private static string BuildMovementPosture(decimal speedKph)
    {
        if (speedKph >= 15m)
        {
            return "Moving";
        }

        if (speedKph > 0.5m)
        {
            return "Idle";
        }

        return "Stopped";
    }

    private static string BuildAlertPosture(
        string freshnessPosture,
        string movementPosture,
        decimal speedKph,
        string? routeStatus)
    {
        if (string.Equals(freshnessPosture, "Stale", StringComparison.Ordinal))
        {
            return "Critical";
        }

        if (speedKph >= 80m)
        {
            return "Warning";
        }

        if (string.Equals(movementPosture, "Idle", StringComparison.Ordinal) &&
            string.Equals(routeStatus, "Delayed", StringComparison.OrdinalIgnoreCase))
        {
            return "Warning";
        }

        if (string.Equals(freshnessPosture, "Aging", StringComparison.Ordinal) ||
            string.Equals(routeStatus, "At risk", StringComparison.OrdinalIgnoreCase))
        {
            return "Warning";
        }

        return "Healthy";
    }

    private static string BuildAlertSummary(
        string freshnessPosture,
        string movementPosture,
        decimal speedKph,
        RouteSummaryReadModel? route)
    {
        if (string.Equals(freshnessPosture, "Stale", StringComparison.Ordinal))
        {
            return "Telemetry is stale; confirm the latest truck position before trusting the route posture.";
        }

        if (speedKph >= 80m)
        {
            return "High-speed telemetry needs review against the active route plan.";
        }

        if (string.Equals(movementPosture, "Idle", StringComparison.Ordinal) &&
            string.Equals(route?.Status, "Delayed", StringComparison.OrdinalIgnoreCase))
        {
            return "Truck is near-stationary while the linked route is already delayed.";
        }

        if (string.Equals(freshnessPosture, "Aging", StringComparison.Ordinal))
        {
            return "Telemetry is aging; refresh the latest position soon.";
        }

        if (string.Equals(route?.Status, "At risk", StringComparison.OrdinalIgnoreCase))
        {
            return "Telemetry is current, but the linked route is already at risk.";
        }

        return "Telemetry is current and aligned with the linked route posture.";
    }

    private static int AlertRank(string alertPosture)
    {
        return alertPosture switch
        {
            "Critical" => 3,
            "Warning" => 2,
            _ => 1
        };
    }
}
