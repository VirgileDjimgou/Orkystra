# SPRINT-26 — Fleet Map Semantics & Workflow Integration

## Status

`NOT_STARTED`

## Goal

Make the cockpit map operationally meaningful through stateful markers and coherent workflow focus.

## Why

The current Leaflet map displays locations and diagnostics, but it does not yet make assignment, mission, exception, and tracking quality intelligible at a glance.

## Dependencies

Sprints 24–25 complete.

## Scope

- encode identity, heading/movement, operational state, freshness/quality, and exception severity in uncluttered markers;
- connect vehicle, driver, active mission, selected trail/route, exception, and mission focus;
- introduce runtime/environment map tile-provider configuration with attribution;
- preserve robust reconnect and safe batching from Sprint 24;
- verify realistic demo-fleet rendering performance.

## Non-goals

- no provider lock-in, external routing dependency, route optimization, or rendering of every historical trail;
- no cosmetic Leaflet replacement.

## Files/modules likely affected

`apps/web/src/features/map/`, cockpit components/composables, tracking/dispatch/operations contracts, runtime configuration, tests, and map documentation.

## Required architecture constraints

Leaflet stays the renderer. Tile URL, attribution, and policy are configuration, not cockpit logic. Use progressive disclosure and existing tenant-safe APIs.

## Tasks

1. Build marker and legend semantics with accessible non-color cues.
2. Add map focus adapters from mission/exception/inspector selection.
3. Render only the selected vehicle trail or mission route when available.
4. Provide a `MapTileProviderConfiguration` boundary and documented Dev/Demo values.
5. Test reconnect, focus, marker update, and performance with a realistic synthetic fleet.

## Required tests

- unit/component tests for marker semantics, focus, provider configuration, and trail selection;
- browser test for mission/exception → map → inspector cycle;
- visual/accessibility checks for quality and exception distinctions;
- 20-vehicle update performance exercise.

## Security checks

Map configuration contains no credentials. Route/trail and mission context remain authenticated and tenant-scoped; no external tile provider receives identifiers beyond ordinary tile requests.

## Performance checks

Only selected history is rendered. At the Sprint-31 target cadence, updates do not grow unbounded and map interaction stays usable with 20 vehicles.

## Acceptance criteria

- [ ] A marker conveys identity, movement, operational state, quality/freshness, and important exception state.
- [ ] Selection flows bidirectionally between map, inspector, missions, and exceptions.
- [ ] Selected trail/route is useful without rendering all history.
- [ ] Tile URL and attribution can change by configuration without cockpit rewrite.
- [ ] Reconnect and safe batching remain intact under realistic fleet updates.
- [ ] The map avoids visual clutter and is accessible without color alone.

## Demo proof

Open a delayed mission, focus its vehicle, inspect the trail and tracking warning, and return to the related exception from the same cockpit.

## Rollback

Keep the default OpenStreetMap configuration and individual marker feature flags; remove derived layers without affecting tracking data.

## Human gates

Stop if hosted tile-provider terms, credentials, or geographic privacy policy require a product-owner choice.

## Definition of Done

All criteria and map performance evidence pass; configuration, docs, state, and checkpoint are complete.
