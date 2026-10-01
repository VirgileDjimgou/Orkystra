# SPRINT-35 — Operations Control Tower UX

## Status

`PLANNED`

## 1. Objective

Make the map-first cockpit the central, immediately understandable Control Tower: a user must understand fleet state, the selected mission, the driver, active exceptions, and the operational timeline mostly from the cockpit itself, without permanent navigation to other screens.

## 2. Context

- Existing cockpit components: `CockpitMapCanvas.vue`, `CockpitInspector.vue`, `CockpitActivityDock.vue`, `OperationsCockpitView.vue`, `CockpitContextLinks.vue` (Sprints 25/26).
- The activity dock has tabs `Exceptions`, `Missions`, `Virtual drivers`, `Timeline`; the `Timeline` tab is currently a placeholder that only invites the user to select a mission or exception.
- Marker semantics already exist in `features/map/markerSemantics.ts` (state, quality, heading in the accessible label) but there is no full status vocabulary (offline, warning, critical, real vs virtual driver) and no selected-vehicle halo, selected route, or stop display.
- History is loaded for the selected vehicle; the cockpit already supports `?vehicleId`/`?missionRef` deep links.
- Data sources already exist: tracking positions/history/diagnostics, dispatch missions/timeline, operations exceptions, agent activities. No new business model is required.

## 3. In Scope

- Vehicle semantics: `moving`, `stopped`, `offline`, `warning`, `critical exception`, plus a visible distinction between real driver and virtual driver.
- Heading/direction indication on markers; clear selection halo.
- Selected mission route and mission stops rendered on the map.
- Smart `fitBounds`/focus when selecting a vehicle, mission, or exception.
- History trail only for the selected vehicle.
- Progressive disclosure: dense list vs inspector details; simple filters (status, exceptions only, real/virtual).
- Visual grouping that avoids marker overload.
- Implement the real `Timeline` tab aggregating existing events: vehicle connected, mission assigned, driver accepted, inspection, mission started, stop arrived, proof uploaded, delay/exception raised, exception handled, mission completed.
- Keep Leaflet, Bootstrap, Pinia, SignalR.

## 4. Out of Scope

- No new backend endpoint unless an aggregation read is strictly missing (then keep it read-only and tenant-scoped).
- No second business model or event store; the timeline aggregates existing read models.
- No tile-provider decision; no licensing change.
- No redesign of dispatch, planning, admin, or Android screens.

## 5. Architecture Constraints

- Authorization remains server-side; the cockpit only renders tenant-filtered responses.
- No client-side authorization rule; visibility flags must come from the API identity.
- Map must remain testable without network tile access.
- Accessibility: markers and controls keep explicit accessible names.
- No duplication of role-specific components.

## 6. Implementation Tasks

1. Define the cockpit status vocabulary and map it to existing tracking quality, exception links, and agent activities.
2. Extend marker rendering: selection halo, heading, offline/warning/critical styles, real/virtual driver distinction.
3. Render selected mission route and stops (polyline + stop markers) using existing mission stop data.
4. Implement focus rules: `fitBounds` on selection, sensible zoom, no forced movement while the user pans.
5. Restrict history rendering to the selected vehicle and dispose of other trails.
6. Add simple cockpit filters and progressive disclosure (dock collapsed by default on small widths).
7. Implement the `Timeline` tab by merging existing sources with stable ordering and source labels; degrade gracefully when a source is empty.
8. Extend Vitest coverage and the Playwright public/operator journeys for the cockpit, selection, filters, and timeline.

## 7. Required tests

- Vitest: marker semantics, status vocabulary mapping, timeline aggregation ordering/dedup, filters, selection state.
- Playwright: cockpit loads without tiles, select vehicle via dock/mission, inspector content, timeline shows real events, filters work, no admin leakage.
- Accessibility checks (labels/roles) inside component tests.
- Full quality gate.

## 8. Security / Tenant Validation

- Every displayed item comes from a tenant-filtered response.
- Demo sessions remain read-only; no new mutation from the cockpit.
- No tenant-identifying information is inferred client-side.
- A second tenant never sees Northwind/Public Demo data (Playwright isolation scenario).

## 9. Definition of Done

- A user can explain fleet state, selected mission/driver, exceptions, and timeline from the cockpit.
- Real `Timeline` tab implemented and fed by existing events.
- No second business model; no new authorization rule client-side.
- Tests and gate pass; acceptance criteria checked; local checkpoint created.

## 10. Evidence to Record

- Screenshots of the cockpit statuses, selection halo, route/stops, filters, and populated timeline.
- Updated Playwright scenarios and Vitest lists.
- `sprint35-quality-gate.log`.

## 11. Stop Conditions

- A required aggregation needs a new persisted model or an event bus → stop; redesign within read models instead.
- Map-tile licensing becomes necessary for the cockpit → human gate (existing decision).
- Accessibility or tenant-isolation cannot be preserved → stop.

## 12. Dependencies

- `SPRINT-34` complete so the map shows realistic movement during cockpit work.
- Existing cockpit components and read models from Sprints 25/26.

## Acceptance criteria

- [ ] Vehicle semantics cover moving, stopped, offline, warning, critical, real and virtual drivers.
- [ ] Selection halo, heading, route and stops are visible for the selected context.
- [ ] Focus/fitBounds behaves predictably and history is limited to the selected vehicle.
- [ ] Simple filters and progressive disclosure reduce visual overload.
- [ ] The `Timeline` tab shows the aggregated operational sequence from existing events.
- [ ] Accessibility and tenant isolation are preserved; no client-side authorization.
- [ ] Vitest, Playwright and the full quality gate pass.
