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

`FleetOps.Worker` hosts the engine, which is disabled by default. Activation fails closed unless `DemoEngine:RuntimeMode` is `Demo`, side effects are sandboxed, the API URL is absolute, and fleet discovery returns 10–20 active vehicle/device assignments. Northwind's development seed contains 20 synthetic assignments (Sprint 31 extended it from 12 so the engine can run at its 20-vehicle maximum). The internal scenario endpoint keeps its historical three-vehicle default and accepts a bounded `maxVehicles=3..20` query for the hosted engine.

Each tick advances one shared logical clock and posts telemetry to `/api/internal/v1/tracking/events`. Sprint 34 uses the analytical route model below. Mission transitions use `IDemoMissionEmitter` and the authenticated `/api/v1/dispatch/missions/{id}/status` workflow. State is written atomically to `.runtime/demo-engine-state.json`, so a Worker restart resumes at the next deterministic tick.

## Sprint 34 route and restart model

Neckar, airport, and west-industrial routes return over their outbound vertices; Fellbach retains its closed loop. All five scenarios use these fixtures. No external road service is involved; connecting lines are synthetic corridors, not turn-by-turn road routing.

`DemoRouteMotion` samples accumulated logical seconds rather than tick count. For a segment of length L and seeded target speed V, travel duration is `π L / (2 V)` (with consistent units), position progress is `(1-cos(πt/T))/2`, and speed is `V sin(πt/T)`. Thus the reported speed is the derivative of distance travelled, starts/ends at zero, and never exceeds the scenario target. The seed and vehicle index choose 90–99% of nominal speed. Each vertex has a 20-logical-second dwell, retaining incoming bearing; the outgoing bearing is selected on departure. Geometry, timestamps, motion phase and identifiers replay deterministically, including variable tick intervals.

Synthetic mission addresses `demo-route:<route-id>:<vertex-index>` identify fixture anchors without changing the business stop or telemetry schema. Fresh public-demo missions use Neckar vertices 0 (depot) and 1 (customer). The virtual-driver arrival tool waits for a tenant-filtered position within 25 m, speed at most 1 km/h, and time difference at most 60 seconds. Unknown synthetic anchors fail explicitly. Ordinary mission addresses retain the existing workflow. Arrival, proof and completion still go through real application contracts.

Snapshot v2 persists `MotionSeconds`. A v1 snapshot cannot faithfully resume the former discontinuous geometry. Before invalidating it, the hosted Worker calls the existing bounded tracking reset for its configured synthetic tenant; failure leaves the snapshot intact and startup fails. It then starts v2 and logs event 2705. Normal v2 restarts preserve the phase without reset. This rollout deliberately removes legacy synthetic tracking history through the canonical reset, not business missions or media (decision D-025). Back up legacy evidence before upgrading; rollback to the old motion requires another isolated tracking reset.

Evidence and captured wrap trace: `docs/02-engineering/SPRINT34_SIMULATION_EVIDENCE.md`.

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

## Hosted public profile (Sprint 32)

The `Demo` runtime mode can now animate the public fleet without granting visitors any write capability:

- the internal engine endpoints (`/api/internal/v1/tracking/events`, `/scenarios/{slug}`, `/scenarios/{slug}/reset`) accept `Development`, `Demo`, and `DemoTesting` hosts and remain `404` in Production;
- in `Demo`/`DemoTesting`, `DemoSessionGuardMiddleware` still refuses anonymous callers and blocks public Demo sessions; the Worker authenticates with the bounded service channel `InternalApi:Key`, sent as `X-FleetOps-Internal-Key`;
- the `public-demo` seed contains twelve synthetic vehicle/device assignments, one synthetic driver, and one assigned mission with a controlled 15-minute delay so the public cockpit always has a consistent vehicle/mission/exception context;
- the engine state is persisted under `/var/opt/fleetops/demo-engine-state.json` in the `docker-compose.demo.yml` profile;
- per-agent credentials are not provisioned in the hosted profile: virtual-driver execution remains a development/test capability recorded through the canonical activity contract. See `docs/02-engineering/RELEASE_CHECKLIST.md`.

Operational commands:

```powershell
pwsh -File scripts/demo-up.ps1
pwsh -File scripts/demo-smoke.ps1 -SkipBuild
pwsh -File scripts/demo-down.ps1
```
