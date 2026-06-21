from __future__ import annotations

from .models import ConstraintSet, Depot, RouteOptimizationRequest, Stop, TimeWindow, Vehicle


def build_demo_route_request() -> RouteOptimizationRequest:
    return RouteOptimizationRequest(
        tenant_id="north-hub-demo",
        scenario_id="9d4e8f09-cf15-48d8-90a6-e96c833fd741",
        depot=Depot(depot_id="depot-1", name="North Hub A"),
        vehicle=Vehicle(
            vehicle_id="veh-1",
            reference="TRK-19",
            capacity=30,
            shift_end_minute=720,
            cost_per_km=1.8,
        ),
        stops=[
            Stop(
                stop_id="stop-1",
                reference="CUST-A",
                demand=10,
                service_minutes=15,
                priority=8,
                time_window=TimeWindow(start_minute=60, end_minute=180),
            ),
            Stop(
                stop_id="stop-2",
                reference="CUST-B",
                demand=8,
                service_minutes=10,
                priority=10,
                time_window=TimeWindow(start_minute=90, end_minute=210),
            ),
            Stop(
                stop_id="stop-3",
                reference="CUST-C",
                demand=6,
                service_minutes=12,
                priority=6,
                time_window=TimeWindow(start_minute=150, end_minute=300),
            ),
        ],
        travel_time_matrix=[
            [0, 40, 55, 70],
            [40, 0, 20, 35],
            [55, 20, 0, 25],
            [70, 35, 25, 0],
        ],
        distance_matrix=[
            [0, 24, 31, 45],
            [24, 0, 12, 20],
            [31, 12, 0, 14],
            [45, 20, 14, 0],
        ],
        constraints=ConstraintSet(max_route_minutes=420, allow_late_service=False),
    )
