# FleetOps Roadmap

## Current phase — MVP Consolidation / Public Autonomous Fleet Demo

**Roadmap version:** `2026.10-mvp-consolidation`

**Rebased:** 2026-10-01
**Decision:** D-022

Goal: turn the current advanced prototype into a credible Fleet Operations Control Tower demo — a synthetic fleet observed on a central map, missions and exceptions, virtual drivers really executing the business workflows, used from a browser or a real Android phone through a self-hosted instance.

This phase is **consolidation only**. The operating principle is `Consolidate > Simplify > Validate > Demonstrate`, never `Add more features`. WMS, ERP, invoicing, accounting, payroll, proprietary route optimization, iOS, generic chatbot/RAG, undemonstrated predictive AI, microservices, Kafka/RabbitMQ, Kubernetes, and any function unrelated to the Fleet Operations demonstration are explicitly forbidden.

| Sprint | Outcome | Status | Source of truth |
|---|---|---|---|
| 33 | Repository truth and green CI | DONE — 2026-10-01 | `sprints/SPRINT-33-REPOSITORY-TRUTH-GREEN-CI.md` |
| 34 | Realistic fleet simulation | PLANNED | `sprints/SPRINT-34-REALISTIC-FLEET-SIMULATION.md` |
| 35 | Operations Control Tower UX | PLANNED | `sprints/SPRINT-35-OPERATIONS-CONTROL-TOWER-UX.md` |
| 36 | Contract safety and Android modularization | PLANNED | `sprints/SPRINT-36-CONTRACT-SAFETY-ANDROID-MODULARIZATION.md` |
| 37 | Hosted autonomous virtual drivers | PLANNED | `sprints/SPRINT-37-HOSTED-AUTONOMOUS-VIRTUAL-DRIVERS.md` |
| 38 | Self-hosted public MVP and final demo proof | PLANNED | `sprints/SPRINT-38-SELF-HOSTED-PUBLIC-MVP.md` |

## Previous phase — Demo Readiness / Autonomous Fleet Simulation (DONE)

**Roadmap version:** `2026.09-demo-readiness`

**Rebased:** 2026-09-26
**Decision:** D-018

FleetOps preserves its existing modular ASP.NET Core monolith, Vue Web console, Android driver application, SQL Server/EF Core persistence, SignalR tracking, private media, audit trail, dispatch workflows, integrations, and test/security controls. The immediate goal is a credible public portfolio demonstration: a map-first cockpit with a deterministic synthetic fleet, autonomous virtual drivers, and a safe hosted Demo runtime.

This is consolidation, not a business-surface expansion. Invoicing, accounting, payroll, WMS, advanced workshop management, proprietary optimization, iOS, regulatory CO2, generic AI/RAG, unrelated ERP modules, microservices, distributed buses, and proprietary hardware remain out of scope.

## Repository truth

- Sprints `00`–`32` are historical `DONE` work and are never rewritten by a rebase.
- Sprint `23` is `DONE` (commit `94e4201`, migrations `20260722235418_Sprint23RecipientStatus` and `20260913201321_Sprint23RecipientNotifications`, recipient-status coverage, 2026-09-25 gate) and is no longer the last recorded gate.
- The former planned `SPRINT-24`–`SPRINT-30` files were never implemented. They live under `sprints/archive/pre-demo-readiness/` as **SUPERSEDED — NOT IMPLEMENTED** and cannot be selected by the autopilot.
- The real-time defects identified during the rebaseline (cross-vehicle coalescing, quality metadata, stale client metadata, retention in the ingestion hot path) were addressed by Sprints 24–26 and the reliability work of Sprint 31.
- The Sprint 32 release evidence is the current reference: green quality gate `.runtime/sprint32-quality-gate.log`, hosted smoke `.runtime/sprint32-demo-smoke.log`, and the post-sprint demo-video/Android enum fix commit `ea529f6`.

## Completed demo-readiness program

| Sprint | Outcome | Status | Source of truth |
|---|---|---|---|
| 24 | Repository truth and real-time tracking hardening | DONE — 2026-09-26 | `sprints/SPRINT-24-REPOSITORY-TRUTH-REALTIME-HARDENING.md` |
| 25 | Map-first Operations Cockpit | DONE — 2026-09-26 | `sprints/SPRINT-25-MAP-FIRST-OPERATIONS-COCKPIT.md` |
| 26 | Fleet map semantics and workflow integration | DONE — 2026-09-26 | `sprints/SPRINT-26-FLEET-MAP-SEMANTICS-WORKFLOW-INTEGRATION.md` |
| 27 | Hosted Demo Engine and realistic virtual fleet | DONE — 2026-09-27 | `sprints/SPRINT-27-HOSTED-DEMO-ENGINE-VIRTUAL-FLEET.md` |
| 28 | Autonomous virtual-driver agents | DONE — 2026-09-27 | `sprints/SPRINT-28-AUTONOMOUS-VIRTUAL-DRIVER-AGENTS.md` |
| 29 | Safe public Demo mode | DONE — 2026-09-27 | `sprints/SPRINT-29-PUBLIC-DEMO-MODE.md` |
| 30 | UX simplification and frontend modularization | DONE — 2026-09-27 | `sprints/SPRINT-30-UX-SIMPLIFICATION-FRONTEND-MODULARIZATION.md` |
| 31 | Reliability, performance, security, and observability proof | DONE — 2026-09-30 | `sprints/SPRINT-31-RELIABILITY-PERFORMANCE-SECURITY-OBSERVABILITY.md` |
| 32 | Hosted demo release and portfolio showcase | DONE — 2026-09-30 | `sprints/SPRINT-32-HOSTED-DEMO-RELEASE-PORTFOLIO.md` |

## Architecture direction

- Keep Leaflet and SignalR. Sprint 26 adds a map-tile provider configuration boundary; it does not replace the map stack.
- `/` becomes the map-first cockpit. `/map` stays a compatible alias/redirect and retains `vehicleId`/`missionRef` focus semantics.
- The Demo engine runs inside the existing host boundaries, preferably `FleetOps.Worker`; it uses real typed application/domain contracts and never fakes workflows by modifying SQL tables directly.
- Virtual drivers use constrained typed tools and a deterministic policy provider. They never have arbitrary HTTP/database access, and their activity trace contains only observable state, policy, action, and result—not hidden reasoning.
- `Demo` is a distinct runtime mode. It uses synthetic tenant data, short-lived server-issued least-privilege sessions, rate limits, sandboxed side effects, automatic reset, and visible simulated-data labelling. Production validation is not weakened.
- The consolidation phase keeps that runtime and adds three constraints: contract safety between C#/TypeScript/Kotlin is tested, hosted virtual drivers obtain short-lived scoped Demo sessions instead of static credentials (Sprint 37), and public self-hosting exposes only the Web/API origin (Sprint 38).
- The cockpit becomes the Control Tower: fleet semantics, selection, route/stops, and a real operational timeline are built from existing read models, never from a second business model.

Canonical details: [Demo Readiness](docs/01-architecture/DEMO_READINESS.md), [Map-First Cockpit](docs/01-architecture/MAP_FIRST_COCKPIT.md), [Hosted Demo Engine](docs/01-architecture/HOSTED_DEMO_ENGINE.md), [Virtual Driver Agents](docs/01-architecture/VIRTUAL_DRIVER_AGENTS.md), [Demo Mode Security](docs/01-architecture/DEMO_MODE_SECURITY.md), and [Sprint Autopilot](docs/02-engineering/SPRINT_AUTOPILOT.md).

## Execution rules

`Start Next Sprint` selects exactly one lowest eligible sprint and stops after it. `Start Next Sprints N` is bounded to `1..10`; each iteration is a separate sprint context and the single-sprint runner remains authoritative. The canonical state and locking protocol are in `.agent/PROJECT_STATE.json`, `.agent/sprint.lock.json`, and `scripts/agent/sprint_orchestrator.py`.

Every sprint must retain the quality gate: Git/compose/object storage/recovery parsing, .NET format/build/tests, GPS and full simulation, Web format/lint/tests/build/Playwright, API health/readiness, Android lint/unit/build, plus the sprint-specific demo, security, tenant, and performance proof. Never weaken a gate to close a sprint.

## Selection rule

The runner chooses the smallest numbered active sprint whose state is neither `DONE` nor `SUPERSEDED`, whose dependencies are satisfied, and that has no unresolved human gate. It ignores `sprints/archive/**`. The planning vocabulary `READY` (next executable) and `PLANNED` (planned, not yet started) is selectable in numeric order by the canonical runner. It stops on a failed gate, failing build/test, acceptance gap, credential/provider/architecture decision, destructive-action approval need, unsafe worktree conflict, `STOP`, existing valid lock, or three repair failures.

## History

The pre-demo commercial roadmap was retained verbatim in `sprints/archive/pre-demo-readiness/`; none of those plans is retroactively marked done. This rebase supersedes only unimplemented planning after Sprint 23 and does not claim commercial/pilot evidence from the synthetic demo.
