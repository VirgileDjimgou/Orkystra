# SPRINT-36 — Contract Safety & Android Modularization

## Status

`PLANNED`

## 1. Objective

Prevent silent regressions between C#, TypeScript, and Kotlin by testing the real API contracts, and reduce Android structural debt without changing behavior.

## 2. Context

- A real contract bug was discovered during the demo-video work: the Android driver app serialized `action`, `upload purpose`, and `defectSeverity` as enum **names** while the API expects numeric values (no `JsonStringEnumConverter` is registered). Inspection, mission commands, and proof uploads never synchronized until the Kotlin DTOs were fixed.
- The stack duplicates contract shapes: C# records in `apps/backend/FleetOps.Api/**/Contracts.cs`, TypeScript types mostly in `apps/web/src/features/*/contracts.ts`, Kotlin DTOs and domain models in `apps/android-driver/app/src/main/java/com/fleetops/driver/DriverApi.kt`.
- Critical contracts to protect: enums (mission status/action, defect severity, upload purpose, alert/exception types), JSON casing, nullable/non-nullable, dates (`DateTimeOffset` ISO strings), row versions, mission commands, delivery proof payloads, authentication responses, and error contracts (Problem Details).
- `MainActivity.kt` (≈690 lines) mixes login, mission list, mission detail, inspection panel, proof panel, and signature/camera dialogs.
- Android architecture standards (`apps/android-driver/AGENTS.md`) already require Compose, UDF, ViewModel/StateFlow, Repository, Room, WorkManager.

## 3. In Scope

- Contract testing strategy based on real API contract shapes:
  - backend tests that freeze serialized payload shapes (numeric enums, casing, nullability) for the critical endpoints;
  - shared golden JSON fixtures checked into the repository;
  - Kotlin unit tests asserting Gson-serialized request bodies match the fixtures, and parsing responses from fixtures;
  - TypeScript tests parsing the same fixtures.
- A CI rule so an incompatible API change fails automatically.
- Android package refactor (behavior-preserving): split `MainActivity.kt` into screens/features, separate API, database, repository, and sync packages.
- Keep a single Gradle app module unless a second module is proven necessary.

## 4. Out of Scope

- No business workflow change, no new endpoint, no simulator change.
- No multi-module Gradle explosion; no shared code generation framework across stacks.
- No UI redesign.
- No change to the offline-first guarantees.

## 5. Architecture Constraints

- Contracts stay in the existing API projects; no new contract microservice.
- Golden fixtures are data files, not generated source code.
- Android target structure stays simple:
  `driver/ core/ · data/api · data/database · data/repository · feature/auth · feature/missions · feature/inspection · feature/proof · feature/sync · ui/`.
- No behavior change during the refactor; tests must prove equivalence.

## 6. Implementation Tasks

1. Enumerate the critical contracts and mark them in code (comments or test names).
2. Add backend contract tests freezing payload shapes for: `/api/auth/login`, `/api/v1/driver/missions*`, `/api/v1/dispatch/missions/{id}` detail, `/api/v1/dispatch/missions/{id}/status`, `/api/v1/driver/missions/{id}/commands`, inspection submit, upload sessions, delivery proof, and one Problem Details error.
3. Emit/verify golden JSON fixtures stored under `tests/contracts/` (e.g. `contracts/driver-command.json`).
4. Add Kotlin unit tests comparing Gson output to the fixtures for every request DTO that previously broke.
5. Add TypeScript tests parsing fixtures into the app types (compile-time + runtime checks).
6. Wire the fixture checks into the authoritative CI pipeline so drift fails the build.
7. Refactor Android: extract screens into `feature/` packages, move Retrofit/OkHttp into `data/api`, Room into `data/database`, repository/sync into `data/`, shared models into `core/`; reduce `MainActivity.kt` to application wiring.
8. Update `apps/android-driver/AGENTS.md` if the target structure clarifies guidance.

## 7. Required tests

- Backend contract tests (fast category) and the existing driver integration tests.
- Kotlin unit tests: DTO serialization for mission commands (1/2/3), upload purpose (1/2), defect severity (0/1/2/3), proof payload, date formatting.
- TypeScript tests parsing the same fixtures.
- Android `lintDebug`, `testDebugUnitTest`, `assembleDebug`, `assembleDebugAndroidTest`.
- Existing offline-first repository tests must stay green without assertion changes.
- Full quality gate.

## 8. Security / Tenant Validation

- Contract fixtures contain no secrets, tokens, or personal data.
- No contract test may bypass authentication or tenant scoping.
- The refactor must not move authorization logic to the client.

## 9. Definition of Done

- The enum contracts that caused the previous bug are explicitly tested in C#, Kotlin, and TypeScript.
- An incompatible API shape change fails CI.
- Android builds/lints/tests are green; offline-first tests unchanged and green.
- No business behavior change; no test weakened.
- Acceptance criteria checked; local checkpoint created.

## 10. Evidence to Record

- Fixture files and the list of contract tests.
- Android package structure before/after with `MainActivity.kt` size reduction.
- CI run showing fixture checks.
- `sprint36-quality-gate.log`.

## 11. Stop Conditions

- A meaningful shared-contract mechanism would require code generation or a new service → human gate; prefer fixtures.
- The refactor cannot remain behavior-preserving → stop and revert.
- Fixture maintenance becomes a second source of truth for business rules → stop.

## 12. Dependencies

- `SPRINT-33` (CI authority) so fixture checks run reliably.
- Android enum fix already committed (post-sprint `ea529f6`).

## Acceptance criteria

- [ ] Critical contracts are frozen by executable tests and shared fixtures.
- [ ] Kotlin/TypeScript/C# checks agree on enums, casing, dates, nullability, row versions, and error shapes.
- [ ] A deliberate incompatible API change fails CI (verified once).
- [ ] Android is split into the agreed packages without Gradle module multiplication.
- [ ] No behavior change; offline-first tests and integration tests remain green.
- [ ] `lintDebug`, `testDebugUnitTest`, `assembleDebug`, `assembleDebugAndroidTest` pass.
- [ ] Full quality gate passes.
