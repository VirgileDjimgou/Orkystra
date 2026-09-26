# SPRINT-28 — Autonomous Virtual Driver Agents

## Status

`NOT_STARTED`

## Goal

Make virtual drivers stateful, autonomous, audited actors within the deterministic Demo engine.

## Why

Moving markers alone do not demonstrate the operational workflow. Synthetic drivers must visibly carry missions through inspection, delivery, exceptions, and recovery using bounded capabilities.

## Dependencies

Sprint 27 complete and Demo engine operational.

## Scope

- add `IVirtualDriverAgent`, typed tool/capability interfaces, and an explicit state machine;
- use a deterministic `IAgentDecisionProvider` default;
- execute assignment, pre-trip, telemetry, arrival, proof, completion, delay, and vehicle-issue workflows;
- persist tenant-scoped structured agent-activity events and publish safe realtime activity;
- implement retries, idempotency, failure transitions, and controlled scenario faults;
- retain a future extension point for non-default decision providers.

## Non-goals

- no external LLM integration, arbitrary HTTP, direct database access, hidden chain-of-thought, or human-driver behavior surveillance.

## Files/modules likely affected

Core Demo/agent ports and entities, Infrastructure persistence/migration, Worker execution, API/realtime presentation, cockpit agent-activity UI, tests, and agent architecture docs.

## Required architecture constraints

Agent tools are typed, tenant-scoped, authorized application capabilities. The state model may use `AVAILABLE`, `ASSIGNED`, `PRE_TRIP`, `EN_ROUTE`, `AT_STOP`, `DELIVERING`, `COMPLETING`, `COMPLETED`, `BLOCKED`, and `OFFLINE` or equivalent domain names. Activity stores observed state, policy, action, and result—not hidden reasoning.

## Tasks

1. Define state, transition guards, tool contracts, and deterministic decision policies.
2. Execute real mission/inspection/proof contracts through a constrained service façade.
3. Persist and stream safe activity/audit events tied to the synthetic tenant.
4. Model scenario-triggered delay, connectivity, and vehicle issue branches with idempotent retries.
5. Present activity in the cockpit dock without exposing internal reasoning.

## Required tests

- state-machine transition and invalid-transition unit tests;
- tool authorization/tenant/idempotency integration tests;
- deterministic scenario replay tests;
- agent retry/offline/issue recovery tests;
- cockpit activity presentation and realtime tests.

## Security checks

Agents receive no database connection, user credential, arbitrary URL, or cross-tenant capability. Each tool action audits actor, scenario, policy, and result. Activity event payloads exclude hidden reasoning and sensitive media content.

## Performance checks

Multiple agents progress independently without serial global blocking. Activity retention and SignalR delivery remain bounded for the default scenario.

## Acceptance criteria

- [ ] Multiple agents independently execute their assigned synthetic missions.
- [ ] Every action follows a valid explicit state transition and typed tool contract.
- [ ] Delays, vehicle issues, and offline events lead to deterministic, recoverable behavior.
- [ ] Actions are idempotent, tenant scoped, authorized, and auditable.
- [ ] Cockpit shows safe agent activity without private chain-of-thought.
- [ ] No LLM or external provider is required for the demo.

## Demo proof

Observe two agents begin missions, one report a deterministic delay and resume, and another complete delivery while the activity dock shows policy/action/result events.

## Rollback

Disable agent scheduling in Demo mode and preserve scenario telemetry. Agent records stay audit-only and can be reset only with the synthetic scenario.

## Human gates

Stop when an existing workflow cannot be invoked through a safe typed contract or an audit/privacy policy needs product-owner direction.

## Definition of Done

State, tools, activity, scenario recovery, tenant isolation, tests, documentation, and checkpoint evidence are complete.
