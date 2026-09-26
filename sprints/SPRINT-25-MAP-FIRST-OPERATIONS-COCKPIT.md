# SPRINT-25 — Map-First Operations Cockpit

## Status

`NOT_STARTED`

## Goal

Make authenticated `/` a map-first Operations Cockpit for Administrators and Operators.

## Why

The existing operations center and live map are separate, large experiences. A visitor must understand the fleet state from one operational surface rather than navigating through lists first.

## Dependencies

Sprint 24 complete and its real-time contract stable.

## Scope

- introduce an `OperationsCockpit` feature and a thin page orchestration surface;
- place the live map in 65–75% of desktop operational workspace;
- add a compact toolbar, contextual right inspector, and collapsible/resizable activity dock;
- compose existing tracking, operations, dispatch, and KPI APIs without duplicating domain behavior;
- preserve `/map`, `vehicleId`, and `missionRef` compatibility.

## Non-goals

- no dispatch/operations backend rewrite;
- no new routing optimizer, reporting module, or demo controls;
- no removal of existing deep routes or role checks.

## Files/modules likely affected

`apps/web/src/features/cockpit/`, map/tracking/operations contracts and stores, router, shell navigation, component tests, and Playwright flows.

## Required architecture constraints

Vue feature components and composables own UI behavior; Pinia holds shared state only. Server authorization remains the source of truth. Reuse Leaflet and existing API contracts.

## Tasks

1. Define cockpit selection contracts for vehicle, driver, mission, and exception.
2. Extract reusable map canvas and operational toolbar boundaries.
3. Implement inspector loading/error/empty states and existing exception summary/KPIs.
4. Add dock tabs for Exceptions, Missions, and Timeline with progressive disclosure.
5. Keep `/map` as a compatible alias or redirect while retaining focus query parameters.
6. Preserve accessible focus order, keyboard selection, and narrow-screen fallback.

## Required tests

- component tests for selection, inspector, dock, and route compatibility;
- role/tenant API regression tests used by composed data;
- Playwright authenticated cockpit journey and existing bookmarked map journey;
- full quality gate.

## Security checks

Only Admin/Operator paths may query cockpit data; every composed response remains tenant-filtered. UI focus may not expose inaccessible identifiers.

## Performance checks

Map remains primary and interactive with 20 live vehicles; selection changes do not reload unrelated datasets or leak stale selection state.

## Acceptance criteria

- [ ] `/` presents the fleet state primarily through a live map.
- [ ] Vehicle, mission, driver, and exception selections resolve into one contextual inspector.
- [ ] Existing exception and dispatch value is reachable without duplicating backend logic.
- [ ] `/map?vehicleId=...` and mission focus remain compatible and tested.
- [ ] Keyboard, loading, empty, error, and responsive behavior are explicit.
- [ ] Authorization and tenant isolation regressions are absent.

## Demo proof

Sign in, select a moving vehicle, inspect mission/driver context, then open an exception from the dock and observe the focused context.

## Rollback

Route `/map` and legacy operations view remain available until the cockpit is proven; revert the `/` route mapping without database changes.

## Human gates

Stop for an API contract gap that requires a new cross-module business workflow or a design decision that materially changes roles.

## Definition of Done

Acceptance criteria, accessibility and E2E proof pass; docs/state/handoff are updated and checkpointed.
