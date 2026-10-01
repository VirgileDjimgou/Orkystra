# SPRINT-34 — Realistic Fleet Simulation

## Status

`PLANNED`

## 1. Objective

Make the synthetic fleet physically credible and permanently watchable: no teleportation, continuous movement, realistic speed/heading/stops, mission stops aligned with trajectories, and no normal trajectory rejected as an anomaly by the tracking-quality engine — while remaining deterministic and reproducible.

## 2. Context

- The hosted Demo engine runs in `FleetOps.Worker` (`DemoScenarioHostedService`, `DemoFleetSimulator`, `DeterministicDemoScenarioEngine`, `DemoScenarioCatalog`).
- Current polyline fixtures interpolate between points; when a route ends the vehicle loops back, producing a visible jump (teleportation) and potentially an out-of-order/unrealistic displacement that the tracking-quality analyzer can flag.
- Scenario codes: `NORMAL_SHIFT`, `LATE_DELIVERY`, `VEHICLE_ISSUE`, `DRIVER_CONNECTIVITY_LOSS`, `COMPLIANCE_WARNING`.
- Determinism is a hard requirement: seed + logical clock produce replayable telemetry; Worker restart resumes from `.runtime`/volume state.
- Demo fleet bound is 10–20 vehicles; the public-demo seed provides 12 assignments and Northwind provides 20.
- Telemetry must always go through the canonical ingestion path (`POST /api/internal/v1/tracking/events`), never through direct SQL writes.

## 3. In Scope

- Route model: closed loops or continuous out-and-back routes so no route ever jumps from end to start.
- Regular interpolation of positions between route vertices with bounded step length.
- Heading derived from the actual movement bearing (no zero/default heading on normal movement).
- Realistic speed profiles with simple acceleration/deceleration and dwell at stops.
- Realistic stops at mission stops; mission stops aligned with route geometry.
- All five scenarios keep deterministic behavior.
- Tracking-quality compatibility: normal movement must not be classified as impossible displacement.
- Worker restart and snapshot resume remain coherent.

## 4. Out of Scope

- No map provider change; Leaflet and the tile boundary stay as-is.
- No real road-network routing or third-party routing service.
- No traffic model, no weather, no advanced physics.
- No fleet-size increase beyond the current bounded maximum.
- No change to business workflows or telemetry contracts.

## 5. Architecture Constraints

- Simulation stays inside `FleetOps.Worker`/`FleetOps.Core`; telemetry uses the canonical API.
- Deterministic clock and seed are preserved; no wall-clock randomness.
- No direct SQL mutation to fabricate positions, trips, or quality.
- Multi-tenant scoping is unchanged.

## 6. Implementation Tasks

1. Audit `DemoScenarioCatalog` routes and `DemoFleetSimulator` tick math; identify every end-of-route wrap and every interpolation discontinuity.
2. Replace open polylines with closed loops or ping-pong routes with turn-around dwell; document the chosen model per scenario.
3. Implement a speed profile: target speed by segment, bounded acceleration/deceleration, stop dwell, deterministic rounding.
4. Compute heading from the interpolated bearing; keep heading stable during dwell.
5. Align mission stops to route anchors so arrival events and proof placement are coherent.
6. Verify the tracking-quality analyzer thresholds against the new displacement/heading statistics; tune only the simulation, never the analyzer, unless a concrete false-positive rule is proven.
7. Keep restart state compatible; migrate or invalidate old snapshots explicitly.
8. Update `docs/01-architecture/HOSTED_DEMO_ENGINE.md` and `docs/01-architecture/VIRTUAL_DRIVER_AGENTS.md` with the route/speed/heading model.

## 7. Required tests

- Unit tests: interpolation continuity, heading derivation, speed profile bounds, no wrap discontinuity, deterministic replay of N ticks.
- Integration tests: 20-vehicle scenario does not produce out-of-order/physically-impossible quality rejections.
- Restart test: Worker restart resumes at the next deterministic tick without teleportation.
- Harness evidence: bounded load run through `simulators/FleetOpsReliabilityHarness` with quality counters.
- Full quality gate.

## 8. Security / Tenant Validation

- Telemetry remains tenant-scoped through the canonical ingestion endpoint.
- No new endpoint; no credential change.
- Simulation cannot write business tables directly.
- Cross-tenant ingestion remains rejected (existing tests).

## 9. Definition of Done

- 20-vehicle scenario reproducible from a fresh start and after restart.
- No GPS jump caused by route wrapping; no normal telemetry rejected as impossible displacement.
- SignalR stream, current positions, history, trips, and quality diagnostics remain coherent.
- Deterministic replay verified by test.
- Documentation updated; acceptance criteria checked; local checkpoint created.

## 10. Evidence to Record

- Before/after harness report with quality counters.
- A short recorded or captured route trace proving continuity (no jump).
- Unit/integration test list.
- `sprint34-quality-gate.log`.

## 11. Stop Conditions

- A credible model requires real road-network routing or an external provider → human gate.
- Quality thresholds must be weakened to pass → stop; fix the simulation instead.
- Restart determinism cannot be preserved without breaking the state format → decide with a recorded migration plan.

## 12. Dependencies

- `SPRINT-33` complete (green CI) so simulation evidence lands on a trustworthy pipeline.
- Demo engine and reliability harness from Sprints 27/31.

## Acceptance criteria

- [ ] Closed/continuous routes remove every end-of-route teleportation.
- [ ] Interpolated positions, heading, and speed are physically coherent in normal operation.
- [ ] Mission stops align with route geometry and arrival events.
- [ ] The five scenarios remain deterministic and replayable.
- [ ] A >= 20-vehicle scenario runs without false impossible-displacement rejections.
- [ ] Worker restart resumes coherently with no teleportation.
- [ ] SignalR, current positions, and history remain consistent.
- [ ] Unit/integration tests and the full quality gate pass.
