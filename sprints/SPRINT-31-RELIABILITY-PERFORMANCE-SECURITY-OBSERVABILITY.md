# SPRINT-31 — Reliability, Performance, Security & Observability

## Status

`NOT_STARTED`

## Goal

Prove the Demo behaves as a reliable product, not a scripted animation.

## Why

Hosted public exposure needs measured resilience across API, Worker, SignalR, telemetry, browser state, reset, session, and tenant boundaries.

## Dependencies

Sprints 24–30 complete; Demo engine and public session flow available.

## Scope

- sustained multi-agent simulation and telemetry/SignalR load validation;
- browser update/memory observation, API/Worker restart, reconnect, catch-up, and reset-concurrency tests;
- auth/session/rate-limit/tenant isolation regression;
- database query/index review, measured budgets, and structured metrics/OpenTelemetry coverage;
- reversible failure injection and regression-suite integration.

## Non-goals

- no unsupported performance claims, distributed-system migration, production customer load test, or destructive experiment outside an isolated synthetic environment.

## Files/modules likely affected

Worker/API tracking and Demo services, telemetry metrics, OpenTelemetry setup, load harnesses, database migrations only where measured indexes justify them, CI/gate scripts, tests, and reliability report.

## Required architecture constraints

Preserve modular monolith and SQL Server. Use synthetic Demo tenants only. Any schema/index change uses an EF migration and rollback plan. Record actual measurements, environment, duration, and limitations.

## Tasks

1. Define measurable budgets and telemetry for ingestion, snapshot/catch-up, SignalR, worker loop, reset, and browser responsiveness.
2. Run 20 agents continuously for at least 15 minutes; optionally run 50-vehicle technical load.
3. Inject API/Worker restart, network reconnect, duplicate/out-of-order inputs, and reset concurrency.
4. Profile database queries and add only justified indexes/batches.
5. Verify session expiration, launch limits, side-effect sandbox, and cross-tenant denial under load.
6. Publish a factual reliability report and extend quality gates appropriately.

## Required tests

- deterministic 20-agent/15-minute sustained scenario;
- API and Worker restart/catch-up integration tests;
- SignalR/browser lifecycle performance test;
- reset race, rate-limit, auth/session, and tenant isolation tests;
- query-plan/index validation and all normal quality gates.

## Security checks

Failure injection cannot target non-demo infrastructure. Verify no cross-tenant state or trace leakage, rate limits remain effective, and telemetry/logging excludes secrets and private location content beyond the intended synthetic demo.

## Performance checks

Record p50/p95 and error rates. At minimum: 20 agents for 15 minutes, no unexplained missing current positions, stable browser memory/update behavior, consistent reconnect snapshot. Record whether 50 vehicles passes or remains an explicit limit.

## Acceptance criteria

- [ ] Measured 20-agent, 15-minute run completes with documented results.
- [ ] No unexplained missing current positions or cross-tenant leakage occurs.
- [ ] API/Worker restart and browser reconnect restore consistent state.
- [ ] Reset concurrency, session expiry, and rate limits behave predictably.
- [ ] Relevant query/index and OpenTelemetry evidence is recorded.
- [ ] Failures are injected only in safe isolated scope and regression suite remains green.

## Demo proof

Show the live cockpit through a Worker/API restart and browser reconnect, then attach the measured reliability report to the release documentation.

## Rollback

Revert individual metrics/index/mode changes with migrations where necessary; retain observations. Disable load harnesses and fault injection outside the test environment.

## Human gates

Stop for an infrastructure quota/hosting limit, production-like load decision, or three unsuccessful repairs against the same quality gate.

## Definition of Done

Actual budgets/results, regression evidence, security review, runbook, state, and checkpoint are complete.
