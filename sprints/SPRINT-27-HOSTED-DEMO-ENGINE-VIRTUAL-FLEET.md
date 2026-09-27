# SPRINT-27 — Hosted Demo Engine & Realistic Virtual Fleet

## Status

`PARTIAL — paused 2026-09-27; resume with Start Next Sprint`

## Goal

Turn development simulation into a deterministic, continuously runnable Demo-mode virtual fleet.

## Why

The current `GpsSimulator` uses mathematical circular routes and `FleetOpsScenarioSimulator` is a bounded development walkthrough. Neither is a credible hosted operational experience.

## Dependencies

Sprints 24–26 complete; Demo runtime security boundary designed but not publicly exposed until Sprint 29.

## Scope

- create an internal Demo module hosted by the existing Worker or established host boundary;
- introduce `IDemoScenarioEngine`, `IDemoClock`, `IDemoTelemetryEmitter`, and `IDemoScenarioRepository` equivalents;
- provide deterministic seed, pause/resume/reset, normal/accelerated speed, and lifecycle status;
- add pre-authored realistic GeoJSON/polyline route fixtures;
- run telemetry and mission actions through existing application/domain contracts;
- supply a small catalog: `NORMAL_SHIFT`, `LATE_DELIVERY`, `VEHICLE_ISSUE`, `DRIVER_CONNECTIVITY_LOSS`, `COMPLIANCE_WARNING`.

## Non-goals

- no external routing API, microservice, direct SQL state fakery, public session, or LLM;
- do not replace the focused development simulators.

## Files/modules likely affected

Demo module under backend/Worker conventions, Core ports where justified, realistic route fixtures under simulator/demo assets, Worker registration, API internal contracts, tests, and engineering docs.

## Required architecture constraints

All actors use a shared logical clock and deterministic seed. Business transitions use typed application services or existing protected flows, never table mutations. Demo data is synthetic and tenant scoped.

## Tasks

1. Define scenario state, clock, persistence boundary, and reset semantics.
2. Add validated route fixtures that render as plausible road movement without an online router.
3. Bridge telemetry through canonical tracking ingestion and missions through supported workflows.
4. Host engine lifecycle in the Worker with cancellation, bounded concurrency, and observable status.
5. Add deterministic scenario tests and reset/replay proof.

## Required tests

- deterministic clock and seed unit tests;
- fixture validation and route interpolation tests;
- integration test proving telemetry and mission events traverse real contracts;
- pause/resume/reset and worker restart tests;
- multi-tenant Demo isolation test.

## Security checks

Demo engine runs only in explicit Demo runtime configuration, uses a synthetic tenant, emits no real e-mail/webhook, and never accepts free-form tenant identifiers.

## Performance checks

Default scenario sustains 10–20 vehicles at configured cadence without unbounded queue/backlog. Reset is bounded, idempotent, and does not impact non-demo tenants.

## Acceptance criteria

- [ ] A selected deterministic scenario starts, pauses, resumes, accelerates, and resets reproducibly.
- [ ] Multiple virtual vehicles follow credible fixture routes on the cockpit map.
- [ ] Telemetry and missions use real FleetOps contracts rather than direct database fakery.
- [ ] Existing development simulators remain usable.
- [ ] Scenario restart and worker restart recover safely.
- [ ] Synthetic tenant isolation and side-effect sandboxing are tested.

## Demo proof

Start `NORMAL_SHIFT`, watch several vehicles move on real map geometry, pause/resume, reset, and replay the same seeded sequence.

## Rollback

Demo engine registration is runtime-mode gated. Disable it and retain the development simulators; reset only the resolved synthetic tenant through the engine.

## Human gates

Stop if a required domain transition has no safe application contract or a fixture license/source is unsuitable for hosted use.

## Definition of Done

All scenario, lifecycle, isolation, and quality proofs pass; architecture/runbook/state are updated and checkpointed.
