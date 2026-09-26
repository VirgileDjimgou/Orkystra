# FleetOps Roadmap

## Current phase — Demo Readiness / Autonomous Fleet Simulation

**Roadmap version:** `2026.09-demo-readiness`

**Rebased:** 2026-09-26
**Decision:** D-018

FleetOps preserves its existing modular ASP.NET Core monolith, Vue Web console, Android driver application, SQL Server/EF Core persistence, SignalR tracking, private media, audit trail, dispatch workflows, integrations, and test/security controls. The immediate goal is a credible public portfolio demonstration: a map-first cockpit with a deterministic synthetic fleet, autonomous virtual drivers, and a safe hosted Demo runtime.

This is consolidation, not a business-surface expansion. Invoicing, accounting, payroll, WMS, advanced workshop management, proprietary optimization, iOS, regulatory CO2, generic AI/RAG, unrelated ERP modules, microservices, distributed buses, and proprietary hardware remain out of scope.

## Repository truth

- Sprints `00`–`22` are historical `DONE` work and are not rewritten by this rebase.
- Sprint `23` is `DONE`: commit `94e4201`, migrations `20260722235418_Sprint23RecipientStatus` and `20260913201321_Sprint23RecipientNotifications`, recipient-status integration coverage, and the 2026-09-25 recorded full quality gate provide the evidence. Its original acceptance boxes were not checked: a documentation defect now reconciled in its sprint file and project state.
- The former planned `SPRINT-24`–`SPRINT-30` files were never implemented. They live under `sprints/archive/pre-demo-readiness/` as **SUPERSEDED — NOT IMPLEMENTED** and cannot be selected by the autopilot.
- The verified real-time defects are scheduled in Sprint 24: global client coalescing loses cross-vehicle updates; hub messages omit actual quality metadata; the client store retains stale metadata; and telemetry retention materializes expired rows in the ingestion hot path.

## Active demo-readiness program

| Sprint | Outcome | Status | Source of truth |
|---|---|---|---|
| 24 | Repository truth and real-time tracking hardening | NOT_STARTED | `sprints/SPRINT-24-REPOSITORY-TRUTH-REALTIME-HARDENING.md` |
| 25 | Map-first Operations Cockpit | NOT_STARTED | `sprints/SPRINT-25-MAP-FIRST-OPERATIONS-COCKPIT.md` |
| 26 | Fleet map semantics and workflow integration | NOT_STARTED | `sprints/SPRINT-26-FLEET-MAP-SEMANTICS-WORKFLOW-INTEGRATION.md` |
| 27 | Hosted Demo Engine and realistic virtual fleet | NOT_STARTED | `sprints/SPRINT-27-HOSTED-DEMO-ENGINE-VIRTUAL-FLEET.md` |
| 28 | Autonomous virtual-driver agents | NOT_STARTED | `sprints/SPRINT-28-AUTONOMOUS-VIRTUAL-DRIVER-AGENTS.md` |
| 29 | Safe public Demo mode | NOT_STARTED | `sprints/SPRINT-29-PUBLIC-DEMO-MODE.md` |
| 30 | UX simplification and frontend modularization | NOT_STARTED | `sprints/SPRINT-30-UX-SIMPLIFICATION-FRONTEND-MODULARIZATION.md` |
| 31 | Reliability, performance, security, and observability proof | NOT_STARTED | `sprints/SPRINT-31-RELIABILITY-PERFORMANCE-SECURITY-OBSERVABILITY.md` |
| 32 | Hosted demo release and portfolio showcase | NOT_STARTED | `sprints/SPRINT-32-HOSTED-DEMO-RELEASE-PORTFOLIO.md` |

## Architecture direction

- Keep Leaflet and SignalR. Sprint 26 adds a map-tile provider configuration boundary; it does not replace the map stack.
- `/` becomes the map-first cockpit. `/map` stays a compatible alias/redirect and retains `vehicleId`/`missionRef` focus semantics.
- The Demo engine runs inside the existing host boundaries, preferably `FleetOps.Worker`; it uses real typed application/domain contracts and never fakes workflows by modifying SQL tables directly.
- Virtual drivers use constrained typed tools and a deterministic policy provider. They never have arbitrary HTTP/database access, and their activity trace contains only observable state, policy, action, and result—not hidden reasoning.
- `Demo` is a distinct runtime mode. It uses synthetic tenant data, short-lived server-issued least-privilege sessions, rate limits, sandboxed side effects, automatic reset, and visible simulated-data labelling. Production validation is not weakened.

Canonical details: [Demo Readiness](docs/01-architecture/DEMO_READINESS.md), [Map-First Cockpit](docs/01-architecture/MAP_FIRST_COCKPIT.md), [Hosted Demo Engine](docs/01-architecture/HOSTED_DEMO_ENGINE.md), [Virtual Driver Agents](docs/01-architecture/VIRTUAL_DRIVER_AGENTS.md), [Demo Mode Security](docs/01-architecture/DEMO_MODE_SECURITY.md), and [Sprint Autopilot](docs/02-engineering/SPRINT_AUTOPILOT.md).

## Execution rules

`Start Next Sprint` selects exactly one lowest eligible sprint and stops after it. `Start Next Sprints N` is bounded to `1..10`; each iteration is a separate sprint context and the single-sprint runner remains authoritative. The canonical state and locking protocol are in `.agent/PROJECT_STATE.json`, `.agent/sprint.lock.json`, and `scripts/agent/sprint_orchestrator.py`.

Every sprint must retain the quality gate: Git/compose/object storage/recovery parsing, .NET format/build/tests, GPS and full simulation, Web format/lint/tests/build/Playwright, API health/readiness, Android lint/unit/build, plus the sprint-specific demo, security, tenant, and performance proof. Never weaken a gate to close a sprint.

## Selection rule

The runner chooses the smallest numbered active sprint whose state is neither `DONE` nor `SUPERSEDED`, whose dependencies are satisfied, and that has no unresolved human gate. It ignores `sprints/archive/**`. It stops on a failed gate, failing build/test, acceptance gap, credential/provider/architecture decision, destructive-action approval need, unsafe worktree conflict, `STOP`, existing valid lock, or three repair failures.

## History

The pre-demo commercial roadmap was retained verbatim in `sprints/archive/pre-demo-readiness/`; none of those plans is retroactively marked done. This rebase supersedes only unimplemented planning after Sprint 23 and does not claim commercial/pilot evidence from the synthetic demo.
