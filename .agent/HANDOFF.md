# Handoff — Demo Readiness rebaseline

## Current state

The repository has been rebased to `2026.09-demo-readiness`. The next eligible sprint is `SPRINT-24` and its source of truth is `sprints/SPRINT-24-REPOSITORY-TRUTH-REALTIME-HARDENING.md`.

Sprint 23 is factually `DONE`: commit `94e4201`, its two migrations, recipient-status integration tests, and the quality report dated 2026-09-25 prove the implementation. The unchecked Sprint-23 acceptance boxes were a documentation drift and have been reconciled; no product behavior changed during this rebaseline.

The former planned Sprints 24–30 were moved to `sprints/archive/pre-demo-readiness/` and marked `SUPERSEDED — NOT IMPLEMENTED`. The active program now has exactly Sprints 24–32.

## Confirmed Sprint-24 work

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
