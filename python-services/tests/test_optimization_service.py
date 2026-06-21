from __future__ import annotations

import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
OPT_SRC = ROOT / "optimization-service" / "src"

if str(OPT_SRC) not in sys.path:
    sys.path.insert(0, str(OPT_SRC))

from orkystra_optimization_service.demo_data import build_demo_route_request
from orkystra_optimization_service.models import OptimizationStatus
from orkystra_optimization_service.solver import solve_route_optimization


class OptimizationServiceTests(unittest.TestCase):
    def test_demo_request_produces_optimized_solution(self) -> None:
        response = solve_route_optimization(build_demo_route_request())

        self.assertEqual(response.status, OptimizationStatus.OPTIMIZED)
        self.assertGreater(len(response.ordered_stop_references), 0)
        self.assertGreaterEqual(len(response.explanation.trade_offs), 1)
        self.assertIn(response.solver_backend, {"ortools", "deterministic-fallback"})

    def test_solution_contains_alternative_route_when_available(self) -> None:
        response = solve_route_optimization(build_demo_route_request())

        self.assertGreaterEqual(len(response.alternatives), 1)
        self.assertTrue(all(alternative.ordered_stop_references for alternative in response.alternatives))

    def test_infeasible_request_is_reported_explicitly(self) -> None:
        request = build_demo_route_request()
        request.stops[0].time_window.start_minute = 10
        request.stops[0].time_window.end_minute = 20
        request.stops[1].time_window.start_minute = 15
        request.stops[1].time_window.end_minute = 25
        request.stops[2].time_window.start_minute = 20
        request.stops[2].time_window.end_minute = 30

        response = solve_route_optimization(request)

        self.assertEqual(response.status, OptimizationStatus.INFEASIBLE)
        self.assertGreaterEqual(len(response.constraint_violations), 1)
        self.assertIsNotNone(response.explanation.infeasibility_reason)

    def test_capacity_violation_yields_infeasible_solution(self) -> None:
        request = build_demo_route_request()
        request.vehicle.capacity = 5

        response = solve_route_optimization(request)

        self.assertEqual(response.status, OptimizationStatus.INFEASIBLE)
        self.assertIn("mandatory constraint", response.explanation.infeasibility_reason or "")


if __name__ == "__main__":
    unittest.main()
