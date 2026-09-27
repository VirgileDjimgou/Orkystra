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

## Sprint 27 implementation

`FleetOps.Worker` hosts the engine, which is disabled by default. Activation fails closed unless `DemoEngine:RuntimeMode` is `Demo`, side effects are sandboxed, the API URL is absolute, and fleet discovery returns 10–20 active vehicle/device assignments. Northwind's development seed contains 12 synthetic assignments. The internal scenario endpoint keeps its historical three-vehicle default and accepts a bounded `maxVehicles=3..20` query for the hosted engine.

Each tick advances one shared logical clock, interpolates the local Stuttgart-area polylines, and posts telemetry to `/api/internal/v1/tracking/events`. Mission transitions use `IDemoMissionEmitter` and the authenticated `/api/v1/dispatch/missions/{id}/status` workflow; the access token must be short-lived configuration and is never stored in source or logs. State is written atomically to `.runtime/demo-engine-state.json`, so a Worker restart resumes at the next deterministic tick.

Minimal local configuration (environment variables):

```powershell
$env:DemoEngine__Enabled = "true"
$env:DemoEngine__RuntimeMode = "Demo"
$env:DemoEngine__ApiBaseUrl = "http://localhost:5080"
$env:DemoEngine__OrganizationSlug = "northwind"
$env:DemoEngine__Scenario = "NORMAL_SHIFT"
$env:DemoEngine__Seed = "2701"
dotnet run --project apps/backend/FleetOps.Worker
```

The API must run in the controlled Development/Demo host for the internal discovery and ingestion endpoints. Public exposure and public session issuance remain Sprint 29 scope.
