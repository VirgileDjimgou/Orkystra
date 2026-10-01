# SPRINT-37 — Hosted Autonomous Virtual Drivers

## Status

`PLANNED`

## 1. Objective

Let virtual drivers genuinely execute missions in the hosted Demo profile without static dangerous credentials: the Worker obtains short-lived, tenant-scoped, revocable Demo agent sessions through a bounded internal mechanism, and agents then use only the real Driver/Operator application contracts.

## 2. Context

- Decision D-020 (Sprint 32) recorded that hosted virtual drivers were not provisioned because the public Demo identity is intentionally passwordless and static per-agent credentials must not be fabricated.
- Today the Worker's demo engine uses:
  - `DemoEngine:InternalApiKey` for telemetry/fleet discovery (bounded service channel, `X-FleetOps-Internal-Key`, Demo/DemoTesting only, `404` in Production);
  - `DemoEngine:ApiAccessToken` / `DriverAccessToken` / per-agent tokens for mission commands and activities — long-lived JWTs supplied manually, which is not viable for a clean deployment.
- `HttpVirtualDriverTools` calls the real driver endpoints; `HttpAgentActivitySink` posts canonical activity; `VirtualDriverHostedService` steps configured agents from `DemoEngine:Agents`.
- The fleet simulator emits telemetry for the configured organization; missions must exist and be assigned for agents to act.

## 3. In Scope

- An internal, bounded **Demo Agent Session** mechanism:
  - endpoint under `/api/internal/v1/demo/agents/sessions` protected by the existing internal key;
  - hard requirements: Demo/DemoTesting host only, tenant resolved from the synthetic organization slug, token bound to a synthetic `DriverId`, short TTL, revocable, audited, unusable in Production, incapable of Admin/Operator escalation;
  - a bounded Demo engine operator session (for mission provisioning/assignment only) that is also tenant-scoped, short-lived, auditable, and non-escalating.
- Worker-side session lifecycle: request, refresh before expiry, revoke/release on shutdown, bounded retries.
- Automatic provisioning of the hosted Demo so virtual drivers execute the real chain: `Assigned → inspection → start → travel → arrive stop → proof → completion`, plus delay, vehicle issue, connectivity loss, and controlled retry/recovery.
- Keep the existing internal telemetry channel; extend the hosted smoke and the Playwright/public journey to observe agent activity in the hosted profile.

## 4. Out of Scope

- No agent access to SQL, filesystem, arbitrary URLs, global secrets, or a generic HTTP tool.
- No new role in the product; no password creation for the public Demo operator.
- No change to the public read-only session model.
- No Production capability whatsoever.

## 5. Architecture Constraints

- All agent actions go through existing application contracts (driver commands, inspections, uploads, proof) executed by the Worker.
- The internal key channel remains the only service-to-service authentication; the new endpoint must reject anonymous callers and any non-Demo environment.
- Tokens are JWTs issued by the existing token issuer with a dedicated claim marking the Demo agent session; every authentication path must validate it against a server-side session record.
- No business table is modified directly by the engine.

## 6. Implementation Tasks

1. Design and document the Demo Agent Session contract (claims, TTL, storage, revocation, audit events) in `docs/01-architecture/DEMO_MODE_SECURITY.md` and `HOSTED_DEMO_ENGINE.md`.
2. Implement the internal session endpoint(s) with strict guards: environment check, internal key match, organization slug allow-list, synthetic driver membership validation, TTL bounds, rate limit.
3. Persist agent sessions (identity session record or bounded in-memory store with explicit limits) and enforce expiry/revocation on every request.
4. Add audit events for issuance, refresh, revocation, and denied attempts.
5. Worker: replace static token configuration with a session provider that refreshes before expiry and revokes on shutdown; keep deterministic behavior.
6. Demo provisioning: from a clean start, ensure the synthetic organization has an assigned mission per agent vehicle using the operator demo session through canonical dispatch contracts.
7. Extend the hosted Demo smoke to assert at least one completed driver workflow and agent activity recorded through canonical endpoints.
8. Add tests: Production `404`, non-Demo denial, cross-tenant denial, expired/revoked token rejection, role-escalation attempt rejection, audit presence, refresh behavior.

## 7. Required tests

- Integration tests for session issuance and denial matrix (environment, key, tenant, driver membership, TTL, revocation, escalation).
- Worker lifecycle tests: refresh before expiry, revoke on shutdown, bounded retry.
- End-to-end hosted smoke: clean Demo start → agents run at least one full workflow → activity/exception/proof visible.
- Playwright: public/operator views show agent activity in the hosted profile.
- Full quality gate (including SQL Server, MinIO, Reliability).

## 8. Security / Tenant Validation

- The session endpoint returns `404` in Production and for any unknown tenant.
- Tokens are short-lived, tenant-bound, driver-bound, revocable, and audited.
- An agent token cannot list users, access admin/integration surfaces, or escalate roles (negative tests).
- Internal key comparison stays constant-time; no secret is logged.
- Public Demo sessions remain read-only.

## 9. Definition of Done

- A clean Demo deployment (compose demo overlay) shows several virtual drivers executing real missions with no manual password/token provisioning.
- All security conditions above are tested and documented.
- Hosted smoke and Playwright evidence captured; acceptance criteria checked; local checkpoint created.

## 10. Evidence to Record

- Session issuance/refresh/revocation logs (redacted) and audit entries.
- Hosted smoke log showing a completed agent workflow and recorded activity.
- Security negative-test list and results.
- Documentation updates (`DEMO_MODE_SECURITY.md`, `HOSTED_DEMO_ENGINE.md`, release checklist).
- `sprint37-quality-gate.log`.

## 11. Stop Conditions

- A scoped operator session cannot be issued without violating least privilege or existing session rules → human gate with a design decision.
- Agents would need arbitrary HTTP/SQL to complete workflows → stop (out of scope).
- Hosted Demo cannot be exercised locally without external credentials → human gate.

## 12. Dependencies

- `SPRINT-33` (green CI), `SPRINT-34` (credible movement), `SPRINT-36` (contract safety) complete.
- Existing internal key channel and driver workflows (Sprints 27/28/32).

## Acceptance criteria

- [ ] Bounded internal Demo Agent Session endpoint exists and is Demo-only (Production `404`).
- [ ] Sessions are tenant-bound, driver-bound, short-lived, revocable, audited, non-escalating.
- [ ] Agents execute the full real workflow chain through canonical contracts.
- [ ] A clean Demo start needs no manual agent credentials.
- [ ] Negative security tests (environment, tenant, expiry, revocation, escalation) pass.
- [ ] Hosted smoke and Playwright observe real agent activity.
- [ ] No direct SQL/filesystem/arbitrary HTTP capability is given to agents.
- [ ] Full quality gate passes.
