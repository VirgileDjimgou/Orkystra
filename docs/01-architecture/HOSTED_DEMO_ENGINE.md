# Hosted Demo Engine

The hosted Demo engine is an internal module, not a microservice. It runs only in explicit Demo mode and integrates with the existing Worker/API/Core/Infrastructure boundaries.

Core responsibilities:

- `IDemoScenarioEngine`: start, pause, resume, reset, and report scenario state;
- `IDemoClock`: shared normal/accelerated logical time;
- `IDemoTelemetryEmitter`: calls canonical telemetry ingestion;
- `IDemoScenarioRepository`: persists only controlled synthetic scenario state;
- realistic local GeoJSON/polyline fixtures with deterministic interpolation.

Scenario data is synthetic and repeatable by seed. `NORMAL_SHIFT`, `LATE_DELIVERY`, `VEHICLE_ISSUE`, `DRIVER_CONNECTIVITY_LOSS`, and `COMPLIANCE_WARNING` are sufficient; scenarios must demonstrate existing FleetOps modules rather than add new business modules.

The engine must use real application/domain contracts for missions, inspections, proof, tracking, alerts, maintenance, and compliance. No direct SQL mutation is permitted to fabricate workflow results. Reset must be bounded, idempotent, observable, and limited to the resolved synthetic tenant.
