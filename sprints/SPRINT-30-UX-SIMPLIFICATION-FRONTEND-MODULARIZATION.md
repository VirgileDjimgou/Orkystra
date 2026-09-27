# SPRINT-30 — UX Simplification & Frontend Modularization

## Status

`DONE — 2026-09-27`

## Goal

Reduce operator cognitive load and frontend complexity without removing FleetOps capabilities.

## Why

Current navigation is broad and important views exceed 500 lines. The cockpit should become the primary work surface while deep links remain stable.

## Dependencies

Sprints 25–29 complete so cockpit, map, and Demo interactions form stable targets.

## Scope

- reorganize primary navigation into Operate, Fleet, Manage, and Administration;
- surface Alerts, Overview, and Map concepts contextually in cockpit;
- place daily planning under Missions/Dispatch information architecture;
- decompose oversized Vue views into coherent feature components and composables;
- standardize loading/empty/error/success, responsive layout, keyboard flow, and accessibility;
- preserve useful deep routes and capabilities.

## Non-goals

- no cosmetic rewrite, new design-system technology, backend feature expansion, or removal of bookmarked routes.

## Files/modules likely affected

Web shell/router, `features/cockpit`, `features/map`, dispatch/operations/alerts views, shared state components, tests, and UI documentation.

## Required architecture constraints

Page components orchestrate rather than aggregate unrelated logic; aim for 250–300 lines where a real boundary exists. Do not duplicate models or authorization rules on the client.

## Tasks

1. Define navigation and deep-link compatibility map.
2. Extract reusable components/composables from cockpit, map, dispatch, alerts, and other oversized surfaces.
3. Consolidate explicit async, offline, and accessibility states.
4. Improve keyboard focus, landmarks, dialogs, responsive behavior, and 200% zoom support.
5. Remove redundant top-level navigation entries only after compatibility routing/tests exist.

## Required tests

- component/unit tests for extracted boundaries and state variants;
- keyboard and route compatibility tests;
- responsive/visual regression checks for key operator workflows;
- Playwright navigation and deep-route E2E;
- relevant Android checks only if its shared UX contract changes.

## Security checks

Navigation simplification cannot hide server-denied routes behind assumed authorization. Preferences and saved state remain user/tenant scoped and cannot elevate roles.

## Performance checks

Route chunks and component state avoid duplicate fetches. No view creates unbounded watchers or live subscriptions; browser update responsiveness remains within Sprint-31 budgets.

## Acceptance criteria

- [x] Primary navigation reflects Operate, Fleet, Manage, and Administration.
- [x] Cockpit is the obvious operator starting point; alerts/overview/map are contextual rather than competing primary workflows.
- [x] Missions include planning access without breaking dispatch links.
- [x] Major views are decomposed into meaningful, tested feature boundaries.
- [x] Key workflows are keyboard-accessible, responsive, and explicit about async states.
- [x] Existing bookmarks/deep routes continue to work or redirect compatibly.

## Demo proof

Walk a visitor from cockpit → mission planning → exception → vehicle inspector using the reduced navigation, keyboard only for the primary path.

## Rollback

Keep compatibility routes and isolated component changes; restore prior navigation mapping without data migrations.

## Human gates

Stop for a user-facing naming/information-architecture decision that changes committed product terminology or breaks external documentation.

## Definition of Done

Accessibility, compatibility, performance, tests, docs/state, and checkpoint satisfy every acceptance criterion.
