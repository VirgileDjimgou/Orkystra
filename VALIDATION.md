# Validation status

Updated 2026-10-01 for phase `MVP Consolidation / Public Autonomous Fleet Demo` (`2026.10-mvp-consolidation`).

## What GREEN means

GREEN is defined by one authoritative pipeline: `.github/workflows/release-validation.yml`. It must never be weaker than the local quality gate, and it is the only CI pipeline in this repository.

Both must pass:

| Surface | Local (`scripts/quality-gate.ps1`) | CI (`release-validation.yml`) |
|---|---|---|
| Repository truth | compose configs, demo smoke config, agent state consistency, orchestrator tests, recovery script parsing | compose config job, governance job |
| Backend | format, Release build, fast tests, Reliability, MinIO (real infrastructure), SQL Server (Testcontainers) | same steps, MinIO via a real container, SQL Server via Testcontainers |
| Dependencies | npm audit High/Critical, NuGet audit on restore (`NuGetAuditMode=all`) | npm audit High/Critical, `dotnet list package --vulnerable` |
| Web | Prettier, ESLint, 32 Vitest tests, production build | same |
| E2E | Playwright public, operations, isolation journeys | same |
| Android | lint, unit tests, debug APK and instrumentation APK | same |
| Simulation / runtime | GPS dry run, 33-step multi-tenant simulation, reliability harness, API health/readiness | covered locally; hosted smoke is a release step |

A red gate always blocks completion. No test is disabled, skipped without justification, or weakened to obtain green.

## Current status

- `SPRINT-00`–`SPRINT-33` are `DONE`; the last recorded full gate is Sprint 33 (`.runtime/sprint33-quality-gate.log`), preceded by the Sprint 32 gate (`.runtime/sprint32-quality-gate.log`) and the hosted Demo smoke `.runtime/sprint32-demo-smoke.log`.
- `SPRINT-33` (Repository Truth & Green CI) consolidated CI, cleared npm advisories (6 → 0), added the MinIO/SQL Server/Android/audit/governance jobs and the state-consistency check.
- The post-sprint demo-video pipeline and the Android driver enum fix are committed (`ea529f6`) and covered by the Android unit test suite.

## Verified in this phase so far

- npm advisories: 0 (was 6, including 3 High) after `npm audit fix` and the Vitest 5 upgrade; Web format/lint/32 Vitest/build green.
- NuGet: no vulnerable direct or transitive package; restore fails on NU1901–NU1904 via `TreatWarningsAsErrors` and `NuGetAuditMode=all`.
- CI: one workflow (`release-validation.yml`) with backend, web, e2e, android, compose and governance jobs; the contradictory `ci.yml` was removed and all its checks were moved, not dropped.

## Known limits

- CI jobs run on GitHub-hosted runners and cannot be executed from this machine; the workflow mirrors the exact commands verified locally. The first push executes them for real.
- SQL Server integration tests use Testcontainers and pull a large image; runtime is expected to be several minutes.
- The MinIO contract test requires a live MinIO container and a pre-created `fleetops-private-media` bucket.
- Android connected-device tests remain optional (`FLEETOPS_ENABLE_ANDROID_CONNECTED=1`) and are not part of GREEN.
- Load above 20 vehicles, hosted virtual-driver provisioning and public self-hosting remain future phase work (SPRINT-34 to SPRINT-38).
