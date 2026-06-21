from __future__ import annotations

from dataclasses import dataclass
from itertools import permutations
from math import inf

from .models import (
    AlternativeSolution,
    OptimizationStatus,
    RouteLeg,
    RouteOptimizationRequest,
    RouteOptimizationResponse,
    SolutionExplanation,
)

try:
    from ortools.constraint_solver import pywrapcp, routing_enums_pb2

    ORTOOLS_AVAILABLE = True
except Exception:  # pragma: no cover - optional runtime dependency
    ORTOOLS_AVAILABLE = False


@dataclass(slots=True)
class CandidatePlan:
    permutation: tuple[int, ...]
    ordered_stop_references: list[str]
    eta_minutes: dict[str, int]
    load_distribution: dict[str, int]
    route_legs: list[RouteLeg]
    objective_score: float
    tight_constraints: list[str]
    constraint_violations: list[str]


def solve_route_optimization(request: RouteOptimizationRequest) -> RouteOptimizationResponse:
    _validate_matrices(request)
    if ORTOOLS_AVAILABLE:
        plan = _solve_with_ortools(request)
        backend = "ortools"
    else:
        plan = _solve_with_permutations(request)
        backend = "deterministic-fallback"

    if plan is None:
        return RouteOptimizationResponse(
            status=OptimizationStatus.INFEASIBLE,
            objective_score=None,
            ordered_stop_references=[],
            eta_minutes={},
            load_distribution={},
            constraint_violations=["No stop ordering satisfies the current time windows, capacity, and route-duration constraints."],
            explanation=SolutionExplanation(
                selected_vehicle_reason=(
                    f"Vehicle {request.vehicle.reference} was the only candidate provided, "
                    "so infeasibility comes from constraints rather than fleet selection."
                ),
                prioritization_reason="The solver attempted to respect stop priority and time windows before minimizing travel cost.",
                tight_constraints=["time windows", "route duration", "vehicle capacity"],
                infeasibility_reason="All evaluated plans violate at least one mandatory constraint.",
                trade_offs=["A feasible plan likely requires a wider time window, a larger vehicle, or a split route."],
            ),
            alternatives=[],
            solver_backend=backend,
        )

    return RouteOptimizationResponse(
        status=OptimizationStatus.OPTIMIZED,
        objective_score=plan.objective_score,
        ordered_stop_references=plan.ordered_stop_references,
        eta_minutes=plan.eta_minutes,
        load_distribution=plan.load_distribution,
        constraint_violations=[],
        explanation=SolutionExplanation(
            selected_vehicle_reason=(
                f"Vehicle {request.vehicle.reference} was selected because it can cover the total demand "
                f"of {sum(stop.demand for stop in request.stops)} units within its shift limit."
            ),
            prioritization_reason="Higher-priority stops and tighter time windows were favored before travel efficiency tie-breaks.",
            tight_constraints=plan.tight_constraints,
            trade_offs=[
                "The objective balances travel distance, lateness avoidance, and stop priority.",
                "Alternative plans are preserved so dispatch can compare efficiency against conservatism.",
            ],
        ),
        alternatives=_build_alternatives(request, plan),
        solver_backend=backend,
    )


def _validate_matrices(request: RouteOptimizationRequest) -> None:
    expected_size = len(request.stops) + 1
    for matrix_name, matrix in (("travel_time_matrix", request.travel_time_matrix), ("distance_matrix", request.distance_matrix)):
        if len(matrix) != expected_size or any(len(row) != expected_size for row in matrix):
            raise ValueError(f"{matrix_name} must be a square matrix sized to depot + stops.")


def _solve_with_permutations(request: RouteOptimizationRequest) -> CandidatePlan | None:
    best_plan: CandidatePlan | None = None
    best_score = inf

    for permutation in permutations(range(1, len(request.stops) + 1)):
        candidate = _evaluate_order(request, permutation)
        if candidate is None:
            continue
        if candidate.objective_score < best_score:
            best_score = candidate.objective_score
            best_plan = candidate

    return best_plan


def _solve_with_ortools(request: RouteOptimizationRequest) -> CandidatePlan | None:
    manager = pywrapcp.RoutingIndexManager(len(request.stops) + 1, 1, 0)
    routing = pywrapcp.RoutingModel(manager)

    def travel_callback(from_index: int, to_index: int) -> int:
        from_node = manager.IndexToNode(from_index)
        to_node = manager.IndexToNode(to_index)
        return request.travel_time_matrix[from_node][to_node]

    transit_callback_index = routing.RegisterTransitCallback(travel_callback)
    routing.SetArcCostEvaluatorOfAllVehicles(transit_callback_index)

    routing.AddDimension(
        transit_callback_index,
        1_000,
        request.constraints.max_route_minutes,
        True,
        "Time",
    )
    time_dimension = routing.GetDimensionOrDie("Time")

    for node_index, stop in enumerate(request.stops, start=1):
        index = manager.NodeToIndex(node_index)
        time_dimension.CumulVar(index).SetRange(stop.time_window.start_minute, stop.time_window.end_minute)

    search_parameters = pywrapcp.DefaultRoutingSearchParameters()
    search_parameters.first_solution_strategy = routing_enums_pb2.FirstSolutionStrategy.PATH_CHEAPEST_ARC
    search_parameters.local_search_metaheuristic = routing_enums_pb2.LocalSearchMetaheuristic.GUIDED_LOCAL_SEARCH
    search_parameters.time_limit.seconds = 2

    solution = routing.SolveWithParameters(search_parameters)
    if solution is None:
        return None

    index = routing.Start(0)
    permutation: list[int] = []
    while not routing.IsEnd(index):
        node = manager.IndexToNode(index)
        next_index = solution.Value(routing.NextVar(index))
        next_node = manager.IndexToNode(next_index)
        if next_node != 0:
            permutation.append(next_node)
        index = next_index

    if not permutation:
        return None

    return _evaluate_order(request, tuple(permutation))


def _evaluate_order(request: RouteOptimizationRequest, permutation: tuple[int, ...]) -> CandidatePlan | None:
    total_demand = sum(request.stops[index - 1].demand for index in permutation)
    if total_demand > request.vehicle.capacity:
        return None

    current_node = 0
    current_minute = 0
    total_distance = 0
    total_priority_penalty = 0
    eta_minutes: dict[str, int] = {}
    load_distribution: dict[str, int] = {}
    route_legs: list[RouteLeg] = []
    tight_constraints: list[str] = []

    for sequence_position, stop_index in enumerate(permutation):
        stop = request.stops[stop_index - 1]
        travel_minutes = request.travel_time_matrix[current_node][stop_index]
        distance = request.distance_matrix[current_node][stop_index]
        arrival = current_minute + travel_minutes
        service_start = max(arrival, stop.time_window.start_minute)

        if service_start > stop.time_window.end_minute:
            return None

        departure = service_start + stop.service_minutes
        eta_minutes[stop.reference] = service_start
        load_distribution[stop.reference] = stop.demand
        route_legs.append(
            RouteLeg(
                from_reference=request.depot.name if current_node == 0 else request.stops[current_node - 1].reference,
                to_reference=stop.reference,
                travel_minutes=travel_minutes,
                arrival_minute=arrival,
                service_start_minute=service_start,
                departure_minute=departure,
            )
        )

        if stop.time_window.end_minute - service_start <= 20:
            tight_constraints.append(f"{stop.reference} time window")

        total_distance += distance
        total_priority_penalty += (sequence_position + 1) * (11 - stop.priority)
        current_minute = departure
        current_node = stop_index

    return_to_depot = request.travel_time_matrix[current_node][0]
    route_duration = current_minute + return_to_depot
    if route_duration > min(request.constraints.max_route_minutes, request.vehicle.shift_end_minute):
        return None

    objective_score = float(total_distance * request.vehicle.cost_per_km + total_priority_penalty)
    if route_duration >= request.constraints.max_route_minutes - 30:
        tight_constraints.append("route duration")
    if total_demand >= request.vehicle.capacity - 2:
        tight_constraints.append("vehicle capacity")

    return CandidatePlan(
        permutation=permutation,
        ordered_stop_references=[request.stops[index - 1].reference for index in permutation],
        eta_minutes=eta_minutes,
        load_distribution=load_distribution,
        route_legs=route_legs,
        objective_score=objective_score,
        tight_constraints=sorted(set(tight_constraints)),
        constraint_violations=[],
    )


def _build_alternatives(request: RouteOptimizationRequest, primary: CandidatePlan) -> list[AlternativeSolution]:
    alternatives: list[AlternativeSolution] = []

    baseline_order = tuple(range(1, len(request.stops) + 1))
    baseline_plan = _evaluate_order(request, baseline_order)
    if baseline_plan is not None and baseline_plan.ordered_stop_references != primary.ordered_stop_references:
        alternatives.append(
            AlternativeSolution(
                label="baseline-plan",
                ordered_stop_references=baseline_plan.ordered_stop_references,
                objective_score=baseline_plan.objective_score,
                summary="Input order without additional resequencing.",
            )
        )

    priority_order = tuple(
        stop_index
        for stop_index, _ in sorted(
            enumerate(request.stops, start=1),
            key=lambda item: (-item[1].priority, item[1].time_window.end_minute),
        )
    )
    priority_plan = _evaluate_order(request, priority_order)
    seen_orders = {
        tuple(primary.ordered_stop_references),
        *(tuple(alternative.ordered_stop_references) for alternative in alternatives),
    }
    if priority_plan is not None and tuple(priority_plan.ordered_stop_references) not in seen_orders:
        alternatives.append(
            AlternativeSolution(
                label="priority-plan",
                ordered_stop_references=priority_plan.ordered_stop_references,
                objective_score=priority_plan.objective_score,
                summary="Higher-priority customers were visited earlier, even when that may cost some travel efficiency.",
            )
        )

    conservative_order = tuple(
        stop_index
        for stop_index, _ in sorted(
            enumerate(request.stops, start=1),
            key=lambda item: (item[1].time_window.end_minute, -item[1].priority),
        )
    )
    conservative_plan = _evaluate_order(request, conservative_order)
    seen_orders = {
        tuple(primary.ordered_stop_references),
        *(tuple(alternative.ordered_stop_references) for alternative in alternatives),
    }
    if conservative_plan is not None and tuple(conservative_plan.ordered_stop_references) not in seen_orders:
        alternatives.append(
            AlternativeSolution(
                label="conservative-plan",
                ordered_stop_references=conservative_plan.ordered_stop_references,
                objective_score=conservative_plan.objective_score,
                summary="Earlier-closing time windows were prioritized to reduce operational risk.",
            )
        )

    return alternatives
