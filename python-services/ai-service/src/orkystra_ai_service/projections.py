from __future__ import annotations

from .models import ProjectionSnapshot, RouteSummaryProjection, ScenarioSummaryProjection, WarehouseSummaryProjection


def build_demo_projection_snapshot() -> ProjectionSnapshot:
    return ProjectionSnapshot(
        warehouse_summaries=[
            WarehouseSummaryProjection(
                warehouse_id="db9a789f-9df8-45ff-a252-96d4319c2f12",
                name="North Hub A",
                zone_count=4,
                rack_count=18,
                slot_count=820,
                occupied_dock_count=3,
                stored_pallet_count=612,
            ),
            WarehouseSummaryProjection(
                warehouse_id="3f224c42-00a5-49a6-955c-c8114d0a6b81",
                name="West Flow Center",
                zone_count=3,
                rack_count=14,
                slot_count=640,
                occupied_dock_count=2,
                stored_pallet_count=401,
            ),
        ],
        route_summaries=[
            RouteSummaryProjection(
                route_id="5024fa82-f658-46c8-88bf-aece07d56f09",
                reference="RT-204",
                truck_id="0d91dc2f-3a74-4562-96a6-c8de611f699d",
                truck_reference="TRK-11",
                status="On time",
                stop_count=5,
                shipment_count=22,
                completed_delivery_count=2,
            ),
            RouteSummaryProjection(
                route_id="528c1588-40fd-451b-8c86-2caa625602de",
                reference="RT-318",
                truck_id="2a398a30-61cf-4fc3-a18d-e491530b4f24",
                truck_reference="TRK-07",
                status="At risk",
                stop_count=4,
                shipment_count=15,
                completed_delivery_count=1,
            ),
            RouteSummaryProjection(
                route_id="9f91e82e-226a-48f7-a94c-907b79431739",
                reference="RT-412",
                truck_id="cf7c6cc8-7b55-49d4-94ff-a5ee9e340856",
                truck_reference="TRK-19",
                status="Delayed",
                stop_count=6,
                shipment_count=27,
                completed_delivery_count=3,
            ),
        ],
        scenario_summaries=[
            ScenarioSummaryProjection(
                scenario_id="9d4e8f09-cf15-48d8-90a6-e96c833fd741",
                name="Baseline day shift",
                seed=42,
                status="Running",
                current_time="2026-06-20T10:15:00Z",
                injected_event_count=2,
            )
        ],
    )
