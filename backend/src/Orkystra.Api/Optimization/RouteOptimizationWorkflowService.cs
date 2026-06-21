using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Orkystra.Contracts.Optimization;
using Orkystra.Contracts.Transport;

namespace Orkystra.Api.Optimization;

public sealed class RouteOptimizationWorkflowService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RouteOptimizationWorkflowService> _logger;

    public RouteOptimizationWorkflowService(HttpClient httpClient, ILogger<RouteOptimizationWorkflowService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<RouteOptimizationWorkflowEnvelope> BuildOptimizationAsync(
        string tenantId,
        RouteDetailReadModel route,
        RouteOptimizationRunRequest request,
        CancellationToken cancellationToken)
    {
        var workflowRequest = BuildOptimizationRequest(tenantId, route, request);

        try
        {
            using var response = await _httpClient.PostAsJsonAsync("optimize", workflowRequest, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var optimizationResponse = await response.Content.ReadFromJsonAsync<OptimizationServiceResponse>(cancellationToken);
                if (optimizationResponse is not null)
                {
                    return new RouteOptimizationWorkflowEnvelope(
                        MapOptimization(route, optimizationResponse),
                        "api",
                        null);
                }
            }

            var errorMessage = await ReadErrorMessageAsync(response, cancellationToken);
            _logger.LogWarning(
                "Optimization service request for route {RouteReference} returned {StatusCode}: {ErrorMessage}",
                route.Reference,
                (int)response.StatusCode,
                errorMessage);

            return new RouteOptimizationWorkflowEnvelope(
                BuildFallbackOptimization(route),
                "fallback",
                errorMessage);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException or JsonException)
        {
            _logger.LogWarning(
                exception,
                "Optimization workflow fallback triggered for route {RouteReference}",
                route.Reference);

            return new RouteOptimizationWorkflowEnvelope(
                BuildFallbackOptimization(route),
                "fallback",
                exception.Message);
        }
    }

    private static OptimizationServiceRequest BuildOptimizationRequest(
        string tenantId,
        RouteDetailReadModel route,
        RouteOptimizationRunRequest request)
    {
        var pendingStops = BuildPendingStops(route);
        if (pendingStops.Count == 0)
        {
            pendingStops = route.Stops
                .OrderBy(stop => stop.Sequence)
                .Select(stop => new PendingStop(stop.Sequence, stop.Name, stop.TimeWindowLabel, 1, 1))
                .ToArray();
        }

        var depot = ResolveDepot(route);
        var travelTimeMatrix = BuildTravelTimeMatrix(route, depot, pendingStops);
        var distanceMatrix = BuildDistanceMatrix(route, depot, pendingStops);
        var totalDemand = pendingStops.Sum(stop => stop.Demand);

        return new OptimizationServiceRequest(
            tenantId,
            request.ScenarioId,
            new OptimizationDepot("route-depot", depot.Name),
            new OptimizationVehicle(
                route.TruckId.ToString(),
                route.TruckReference,
                Math.Max(1, (int)Math.Ceiling(route.TruckCapacityKilograms / 10m)),
                840,
                1.8m),
            pendingStops.Select(stop => new OptimizationStop(
                $"stop-{stop.Sequence}",
                stop.Name,
                stop.Demand,
                12,
                stop.Priority,
                ParseTimeWindow(stop.TimeWindowLabel)))
                .ToArray(),
            travelTimeMatrix,
            distanceMatrix,
            new OptimizationConstraintSet(
                Math.Max(360, travelTimeMatrix.SelectMany(row => row).Sum() + pendingStops.Count * 20),
                false));
    }

    private static RouteOptimizationResultReadModel MapOptimization(RouteDetailReadModel route, OptimizationServiceResponse response)
    {
        return new RouteOptimizationResultReadModel(
            route.RouteId,
            route.Reference,
            response.Status,
            response.ObjectiveScore,
            response.OrderedStopReferences.ToArray(),
            new Dictionary<string, int>(response.EtaMinutes, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(response.LoadDistribution, StringComparer.OrdinalIgnoreCase),
            response.ConstraintViolations.ToArray(),
            new RouteOptimizationExplanationReadModel(
                response.Explanation.SelectedVehicleReason,
                response.Explanation.PrioritizationReason,
                response.Explanation.TightConstraints.ToArray(),
                response.Explanation.InfeasibilityReason,
                response.Explanation.TradeOffs.ToArray()),
            response.Alternatives
                .Select(alternative => new RouteOptimizationAlternativeReadModel(
                    alternative.Label,
                    alternative.OrderedStopReferences.ToArray(),
                    alternative.ObjectiveScore,
                    alternative.Summary))
                .ToArray(),
            response.SolverBackend);
    }

    private static RouteOptimizationResultReadModel BuildFallbackOptimization(RouteDetailReadModel route)
    {
        var pendingStops = BuildPendingStops(route);
        var currentOrder = pendingStops.Select(stop => stop.Name).ToArray();
        var etaMinutes = BuildFallbackEtaMinutes(pendingStops);
        var loadDistribution = pendingStops.ToDictionary(stop => stop.Name, stop => stop.Demand, StringComparer.OrdinalIgnoreCase);

        var priorityOrder = pendingStops
            .OrderByDescending(stop => stop.Priority)
            .ThenBy(stop => ParseTimeWindow(stop.TimeWindowLabel).EndMinute)
            .Select(stop => stop.Name)
            .ToArray();
        var conservativeOrder = pendingStops
            .OrderBy(stop => ParseTimeWindow(stop.TimeWindowLabel).EndMinute)
            .ThenByDescending(stop => stop.Priority)
            .Select(stop => stop.Name)
            .ToArray();

        return new RouteOptimizationResultReadModel(
            route.RouteId,
            route.Reference,
            "optimized",
            Math.Round((decimal)(pendingStops.Count * 18 + pendingStops.Sum(stop => stop.Priority) * 1.1), 2),
            currentOrder,
            etaMinutes,
            loadDistribution,
            [],
            new RouteOptimizationExplanationReadModel(
                $"Vehicle {route.TruckReference} stays assigned because the fallback workflow only evaluates the current route posture.",
                "The local fallback keeps the current remaining stop order and surfaces comparison plans for dispatcher review.",
                pendingStops.Count >= 4 ? ["route duration"] : [],
                null,
                [
                    "The optimization service was unavailable, so no solver-backed resequencing was attempted.",
                    "Alternative plans are still shown to support manual review while the service recovers."
                ]),
            BuildFallbackAlternatives(currentOrder, priorityOrder, conservativeOrder),
            "api-local-fallback");
    }

    private static IReadOnlyCollection<RouteOptimizationAlternativeReadModel> BuildFallbackAlternatives(
        IReadOnlyCollection<string> currentOrder,
        IReadOnlyCollection<string> priorityOrder,
        IReadOnlyCollection<string> conservativeOrder)
    {
        var alternatives = new List<RouteOptimizationAlternativeReadModel>();

        if (!currentOrder.SequenceEqual(priorityOrder))
        {
            alternatives.Add(new RouteOptimizationAlternativeReadModel(
                "priority-plan",
                priorityOrder.ToArray(),
                92.4m,
                "Higher-priority stops are moved earlier to protect the most sensitive deliveries."));
        }

        if (!currentOrder.SequenceEqual(conservativeOrder) && !priorityOrder.SequenceEqual(conservativeOrder))
        {
            alternatives.Add(new RouteOptimizationAlternativeReadModel(
                "conservative-plan",
                conservativeOrder.ToArray(),
                96.8m,
                "Earlier-closing windows are visited sooner to reduce lateness risk."));
        }

        return alternatives;
    }

    private static IReadOnlyDictionary<string, int> BuildFallbackEtaMinutes(IReadOnlyCollection<PendingStop> pendingStops)
    {
        var etaMinutes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var currentMinute = 0;

        foreach (var stop in pendingStops.OrderBy(stop => stop.Sequence))
        {
            var window = ParseTimeWindow(stop.TimeWindowLabel);
            currentMinute = Math.Max(currentMinute + 25, window.StartMinute);
            etaMinutes[stop.Name] = currentMinute;
            currentMinute += 12;
        }

        return etaMinutes;
    }

    private static PendingDepot ResolveDepot(RouteDetailReadModel route)
    {
        var completedStops = route.Deliveries
            .Where(delivery => string.Equals(delivery.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            .Select(delivery => delivery.StopSequence)
            .DefaultIfEmpty(0)
            .Max();

        var depotStop = route.Stops
            .OrderBy(stop => stop.Sequence)
            .LastOrDefault(stop => stop.Sequence <= completedStops)
            ?? route.Stops.OrderBy(stop => stop.Sequence).First();

        return new PendingDepot(depotStop.Name, depotStop.CoordinateLabel);
    }

    private static IReadOnlyCollection<PendingStop> BuildPendingStops(RouteDetailReadModel route)
    {
        var shipmentWeights = route.Shipments.ToDictionary(
            shipment => shipment.Reference,
            shipment => shipment.LoadWeightKilograms,
            StringComparer.OrdinalIgnoreCase);

        var pendingStops = route.Stops
            .Select(stop =>
            {
                var deliveries = route.Deliveries
                    .Where(delivery => delivery.StopSequence == stop.Sequence && !string.Equals(delivery.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                var weight = deliveries
                    .Sum(delivery => shipmentWeights.TryGetValue(delivery.ShipmentReference, out var shipmentWeight) ? shipmentWeight : 0m);
                var demand = Math.Max(1, (int)Math.Ceiling(weight / 10m));
                var priority = Math.Clamp(deliveries.Length == 0 ? 4 : deliveries.Length + 5, 1, 10);
                return new PendingStop(stop.Sequence, stop.Name, stop.TimeWindowLabel, demand, priority)
                {
                    CoordinateLabel = stop.CoordinateLabel
                };
            })
            .Where(stop => route.Deliveries.Any(delivery => delivery.StopSequence == stop.Sequence && !string.Equals(delivery.Status, "Completed", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(stop => stop.Sequence)
            .ToArray();

        return pendingStops;
    }

    private static int[][] BuildTravelTimeMatrix(RouteDetailReadModel route, PendingDepot depot, IReadOnlyCollection<PendingStop> pendingStops)
    {
        var coordinates = new List<(double Latitude, double Longitude)>
        {
            ParseCoordinate(depot.CoordinateLabel)
        };
        coordinates.AddRange(pendingStops.Select(stop => ParseCoordinate(stop.CoordinateLabel)));

        var matrix = new int[coordinates.Count][];
        for (var row = 0; row < coordinates.Count; row += 1)
        {
            matrix[row] = new int[coordinates.Count];
            for (var column = 0; column < coordinates.Count; column += 1)
            {
                if (row == column)
                {
                    continue;
                }

                var distance = CalculateDistanceKilometers(coordinates[row], coordinates[column]);
                matrix[row][column] = Math.Max(8, (int)Math.Round(distance * 2.4));
            }
        }

        return matrix;
    }

    private static int[][] BuildDistanceMatrix(RouteDetailReadModel route, PendingDepot depot, IReadOnlyCollection<PendingStop> pendingStops)
    {
        var coordinates = new List<(double Latitude, double Longitude)>
        {
            ParseCoordinate(depot.CoordinateLabel)
        };
        coordinates.AddRange(pendingStops.Select(stop => ParseCoordinate(stop.CoordinateLabel)));

        var matrix = new int[coordinates.Count][];
        for (var row = 0; row < coordinates.Count; row += 1)
        {
            matrix[row] = new int[coordinates.Count];
            for (var column = 0; column < coordinates.Count; column += 1)
            {
                if (row == column)
                {
                    continue;
                }

                matrix[row][column] = Math.Max(4, (int)Math.Round(CalculateDistanceKilometers(coordinates[row], coordinates[column])));
            }
        }

        return matrix;
    }

    private static OptimizationTimeWindow ParseTimeWindow(string timeWindowLabel)
    {
        var parts = timeWindowLabel.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return new OptimizationTimeWindow(0, 240);
        }

        return new OptimizationTimeWindow(ParseClock(parts[0]), ParseClock(parts[1]));
    }

    private static int ParseClock(string time)
    {
        var segments = time.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2)
        {
            return 0;
        }

        return int.Parse(segments[0]) * 60 + int.Parse(segments[1]);
    }

    private static (double Latitude, double Longitude) ParseCoordinate(string coordinateLabel)
    {
        var parts = coordinateLabel.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return (0, 0);
        }

        return (double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
            double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
    }

    private static double CalculateDistanceKilometers((double Latitude, double Longitude) from, (double Latitude, double Longitude) to)
    {
        var latitudeDelta = (from.Latitude - to.Latitude) * 111d;
        var longitudeDelta = (from.Longitude - to.Longitude) * 73d;
        return Math.Sqrt((latitudeDelta * latitudeDelta) + (longitudeDelta * longitudeDelta));
    }

    private static async Task<string?> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(payload) ? response.ReasonPhrase : payload;
    }

    private sealed record PendingDepot(string Name, string CoordinateLabel);

    private sealed record PendingStop(int Sequence, string Name, string TimeWindowLabel, int Demand, int Priority)
    {
        public string CoordinateLabel { get; init; } = "0, 0";
    }

    private sealed record OptimizationServiceRequest(
        [property: JsonPropertyName("tenant_id")] string TenantId,
        [property: JsonPropertyName("scenario_id")] string? ScenarioId,
        [property: JsonPropertyName("depot")] OptimizationDepot Depot,
        [property: JsonPropertyName("vehicle")] OptimizationVehicle Vehicle,
        [property: JsonPropertyName("stops")] IReadOnlyCollection<OptimizationStop> Stops,
        [property: JsonPropertyName("travel_time_matrix")] IReadOnlyCollection<IReadOnlyCollection<int>> TravelTimeMatrix,
        [property: JsonPropertyName("distance_matrix")] IReadOnlyCollection<IReadOnlyCollection<int>> DistanceMatrix,
        [property: JsonPropertyName("constraints")] OptimizationConstraintSet Constraints);

    private sealed record OptimizationDepot(
        [property: JsonPropertyName("depot_id")] string DepotId,
        [property: JsonPropertyName("name")] string Name);

    private sealed record OptimizationVehicle(
        [property: JsonPropertyName("vehicle_id")] string VehicleId,
        [property: JsonPropertyName("reference")] string Reference,
        [property: JsonPropertyName("capacity")] int Capacity,
        [property: JsonPropertyName("shift_end_minute")] int ShiftEndMinute,
        [property: JsonPropertyName("cost_per_km")] decimal CostPerKilometer);

    private sealed record OptimizationStop(
        [property: JsonPropertyName("stop_id")] string StopId,
        [property: JsonPropertyName("reference")] string Reference,
        [property: JsonPropertyName("demand")] int Demand,
        [property: JsonPropertyName("service_minutes")] int ServiceMinutes,
        [property: JsonPropertyName("priority")] int Priority,
        [property: JsonPropertyName("time_window")] OptimizationTimeWindow TimeWindow);

    private sealed record OptimizationTimeWindow(
        [property: JsonPropertyName("start_minute")] int StartMinute,
        [property: JsonPropertyName("end_minute")] int EndMinute);

    private sealed record OptimizationConstraintSet(
        [property: JsonPropertyName("max_route_minutes")] int MaxRouteMinutes,
        [property: JsonPropertyName("allow_late_service")] bool AllowLateService);

    private sealed record OptimizationServiceResponse(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("objective_score")] decimal? ObjectiveScore,
        [property: JsonPropertyName("ordered_stop_references")] IReadOnlyCollection<string> OrderedStopReferences,
        [property: JsonPropertyName("eta_minutes")] IReadOnlyDictionary<string, int> EtaMinutes,
        [property: JsonPropertyName("load_distribution")] IReadOnlyDictionary<string, int> LoadDistribution,
        [property: JsonPropertyName("constraint_violations")] IReadOnlyCollection<string> ConstraintViolations,
        [property: JsonPropertyName("explanation")] OptimizationServiceExplanation Explanation,
        [property: JsonPropertyName("alternatives")] IReadOnlyCollection<OptimizationServiceAlternative> Alternatives,
        [property: JsonPropertyName("solver_backend")] string SolverBackend);

    private sealed record OptimizationServiceExplanation(
        [property: JsonPropertyName("selected_vehicle_reason")] string SelectedVehicleReason,
        [property: JsonPropertyName("prioritization_reason")] string PrioritizationReason,
        [property: JsonPropertyName("tight_constraints")] IReadOnlyCollection<string> TightConstraints,
        [property: JsonPropertyName("infeasibility_reason")] string? InfeasibilityReason,
        [property: JsonPropertyName("trade_offs")] IReadOnlyCollection<string> TradeOffs);

    private sealed record OptimizationServiceAlternative(
        [property: JsonPropertyName("label")] string Label,
        [property: JsonPropertyName("ordered_stop_references")] IReadOnlyCollection<string> OrderedStopReferences,
        [property: JsonPropertyName("objective_score")] decimal ObjectiveScore,
        [property: JsonPropertyName("summary")] string Summary);
}
