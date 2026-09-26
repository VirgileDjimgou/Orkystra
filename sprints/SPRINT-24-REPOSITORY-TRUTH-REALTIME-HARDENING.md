# SPRINT-24 — Repository Truth & Real-Time Hardening

## Status

`NOT_STARTED`

## Goal

Establish a trustworthy baseline before cockpit, demo-engine, or agent work.

## Why

The audit confirmed that the live client globally coalesces updates, that SignalR emits default quality metadata, that the Pinia store retains stale metadata, and that ingestion materializes expired telemetry on every event. These defects undermine a multi-vehicle demo.

## Dependencies

Sprint 23 must remain `DONE` with its migration, tests, and quality-gate evidence recorded. No later sprint may begin while a P0/P1 regression remains open.

## Scope

- reconcile Roadmap, sprint files, migrations, tests, and project-state evidence;
- fix tracking live batching per `vehicleId` or via a keyed short buffer;
- emit and persist actual sequence, accuracy, source, score, status, and reason;
- update every mutable live field in the tracking store;
- verify duplicate, out-of-order, stale, reconnect, catch-up, and server-restart behavior;
- move retention cleanup out of the telemetry hot path or use a bounded database-side deletion;
- resolve existing build, lint, test, or format regressions within the sprint scope.

## Non-goals

- no cockpit redesign, map-provider abstraction, demo runtime, agent, or new business module;
- no change to tenant or authorization model;
- no deletion or reduction of existing tests.

## Files/modules likely affected

`apps/web/src/features/tracking/`, `FleetOps.Api/Tracking/`, `FleetOps.Worker/`, tracking tests, quality/load documentation, and state/report files.

## Required architecture constraints

Keep Leaflet, SignalR, the canonical tracking ingestion path, and tenant-scoped hub groups. Retention must not change business semantics. All server time remains `TimeProvider`-based.

## Tasks

1. Record the factual Sprint-23 and tracking audit in the project evidence.
2. Replace the singleton pending browser position with a keyed coalescing buffer and deterministic flush.
3. Send actual quality metadata in `trackingPositionChanged`; derive status/reason with the same rules as snapshots.
4. Make the store atomically update all live mutable fields and reject stale local updates.
5. Add a catch-up boundary that reloads the current snapshot after reconnect before marking the UI current.
6. Implement scheduled/bounded retention cleanup using efficient EF/SQL deletion; instrument its result.
7. Refresh the load baseline with an explicit no-loss invariant.

## Required tests

- Vitest regression for two vehicles emitted inside one throttle interval;
- Vitest transition from Fresh to Inaccurate/Delayed without refresh;
- API integration tests for full live metadata, duplicate, out-of-order, and stale ordering;
- reconnect/catch-up test and API restart recovery validation;
- retention test proving no unbounded materialization and correct deletion;
- normal repository quality gate.

## Security checks

Hub events, snapshots, and catch-up remain organization scoped; no quality/source data may permit cross-tenant discovery. Telemetry ingestion keeps replay protection and authenticated internal/device boundaries.

## Performance checks

At 20–50 synthetic vehicles, each accepted current update reaches the client exactly once per vehicle per flush window. Retention has a bounded query/delete cost and does not enumerate all expired rows per event.

## Acceptance criteria

- [ ] No vehicle update is lost because another vehicle emits during the same client throttle interval.
- [ ] SignalR and snapshot responses contain matching live metadata.
- [ ] Quality-state changes are rendered from live events without a page refresh.
- [ ] Duplicate and out-of-order telemetry cannot regress the current position.
- [ ] Reconnect and API restart restore a consistent tenant-scoped snapshot.
- [ ] Retention is no longer an O(N) per-event in-memory operation.
- [ ] All mandatory gates pass with no P0/P1 regression left open.

## Demo proof

Run a concurrent synthetic fleet, force a reconnect, inject duplicate/out-of-order and degraded-quality points, and show every vehicle and quality state correctly on the existing map.

## Rollback

Feature-flag or revert only the new coalescer/cleanup implementation; preserve raw telemetry and current-position data. Never delete history outside configured retention.

## Human gates

Stop for an unresolved data-retention policy conflict, an unavoidable breaking tracking contract, or three failed repair attempts for the same gate.

## Definition of Done

All acceptance boxes checked, targeted and full gates recorded, sprint/state/roadmap/handoff updated, and a local checkpoint created when policy permits.
