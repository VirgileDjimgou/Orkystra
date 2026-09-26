# Handoff — Demo Readiness rebaseline

## Current state

Sprint 24 is `DONE`. Its implementation passed Release build (zero warnings/errors), six tracking integration tests, four new tracking Vitest tests, Web lint, Web production build, Web Prettier check, and `git diff --check`.

The canonical `scripts/quality-gate.ps1` is green after its formatter invocation was made explicit (`--no-restore --verbosity diagnostic`). It passed compose/MinIO/recovery, backend format/build/147 fast tests/MinIO/SQL Server, GPS dry-run, the 33-step multi-tenant simulation, Web format/lint/25 tests/build/5 Playwright flows, API health/readiness, and Android lint/unit/APK build. Connected Android execution was not requested or configured.

Sprint-24 changes: browser SignalR events are coalesced independently by vehicle; reconnect waits for tenant snapshot catch-up before becoming live; stale local live updates are rejected; all mutable quality metadata is updated; hub and snapshot mapping share the same metadata/status rules; telemetry retention runs in a bounded Worker batch instead of ingestion.

## Rebaseline state

The repository has been rebased to `2026.09-demo-readiness`. The next eligible sprint is `SPRINT-25` and its source of truth is `sprints/SPRINT-25-MAP-FIRST-OPERATIONS-COCKPIT.md`.

Sprint 23 is factually `DONE`: commit `94e4201`, its two migrations, recipient-status integration tests, and the quality report dated 2026-09-25 prove the implementation. The unchecked Sprint-23 acceptance boxes were a documentation drift and have been reconciled; no product behavior changed during this rebaseline.

The former planned Sprints 24–30 were moved to `sprints/archive/pre-demo-readiness/` and marked `SUPERSEDED — NOT IMPLEMENTED`. The active program now has exactly Sprints 24–32.

## Original confirmed Sprint-24 work

- `apps/web/src/features/tracking/live.ts` uses one global pending position, so simultaneous vehicle events overwrite one another.
- `TrackingIngestionService` emits `TrackingPositionResponse` without actual sequence/accuracy/source/quality metadata.
- the Pinia tracking store copies coordinate fields but leaves live quality fields stale.
- `ApplyRetentionAsync` materializes expired telemetry inside every ingestion call.
- reconnect calls catch-up, but no regression test proves the full reconnect/server-restart consistency contract.

Do not repair these in the rebaseline. Implement and test them only through Sprint 24.

## Autopilot

Use `scripts/sprint-runner.ps1 -Action Start` only when the user says `Start Next Sprint`. The canonical state is `.agent/PROJECT_STATE.json`; the lock is `.agent/sprint.lock.json`; the implementation is `scripts/agent/sprint_orchestrator.py`. Do not bypass `STOP` or `HUMAN_REQUIRED.json`, and stop after one sprint.

## Rebaseline validation pending/completed

Run the lightweight orchestration/documentation validation listed in `.agent/QUALITY_REPORT.md` after reviewing the final diff. No full product quality gate has been claimed or run by this planning rebaseline.
