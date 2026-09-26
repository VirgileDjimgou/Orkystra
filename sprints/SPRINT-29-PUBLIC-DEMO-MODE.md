# SPRINT-29 — Public Demo Mode

## Status

`NOT_STARTED`

## Goal

Expose the hosted simulator safely through a public `Launch Live Demo` path.

## Why

A recruiter should not need a seeded password, and Demo convenience must never weaken Production configuration or reveal privileged capabilities.

## Dependencies

Sprints 27–28 complete; Cockpit demo integration available; explicit Demo security design approved in repository documentation.

## Scope

- add a clearly distinct `Demo` runtime mode alongside Development and Production;
- provision only synthetic dedicated demo tenant(s) and sandbox side effects;
- issue short-lived, least-privilege DemoOperator HttpOnly sessions from `Launch Live Demo`;
- apply CSRF, rate limits, session TTL, reset policy, and safe concurrent-visitor semantics;
- expose compact scenario controls only in authorized Demo context;
- display persistent `SIMULATED DEMO` labelling and create an E2E public journey.

## Non-goals

- no Production validator relaxation, shared reusable credentials, real e-mail/webhooks, access to sensitive administration, or customer data.

## Files/modules likely affected

API runtime/security/session configuration, Worker Demo host, Web launch/cockpit components, compose templates, tests, and Demo security documentation.

## Required architecture constraints

Demo is a first-class explicit runtime profile—not Development seeds in Production. Session tenant and permissions originate server side; browser code receives no password or privileged token. Existing session/CSRF patterns remain authoritative.

## Tasks

1. Define runtime-mode validation and Demo-only configuration surface.
2. Implement anonymous launch rate limits and bounded DemoOperator session issuance.
3. Isolate/reset scenario state safely for concurrent viewers according to documented model.
4. Guard all controls server-side and hide them outside Demo mode.
5. Sandbox outgoing integrations/notifications and surface synthetic-data label.
6. Add browser E2E covering launch, control, expiry, and denied admin access.

## Required tests

- runtime configuration tests for Development/Demo/Production separation;
- session/CSRF/rate-limit/TTL and role-denial integration tests;
- concurrent launch/reset isolation tests;
- Playwright public demo journey and expired-session recovery;
- full quality gate.

## Security checks

No Demo user can access a non-demo tenant, security/data administration, external credentials, recipient information, or persistent privileged API keys. Logs and telemetry contain synthetic data only; Production remains fail-fast.

## Performance checks

Rate limits and reset coordination tolerate a defined small visitor burst without cross-session state corruption. Session cleanup is bounded and observable.

## Acceptance criteria

- [ ] Public visitors launch a live demo without copied credentials.
- [ ] Demo sessions are server-issued, HttpOnly, short-lived, scoped, rate-limited, and CSRF-protected.
- [ ] Demo mode is visibly synthetic and has sandboxed side effects.
- [ ] Controls cannot appear or operate in ordinary Production workspaces.
- [ ] Concurrent visitors cannot access each other's tenant/session state.
- [ ] Production configuration validation remains as strict as before.

## Demo proof

Open an incognito browser, launch the demo, observe a scenario, attempt an Admin-only route (denied), let the session expire, and relaunch safely.

## Rollback

Disable public launch and Demo host configuration while retaining Production behavior unchanged. Expire Demo sessions and reset only synthetic data.

## Human gates

Stop for a hosting/domain/rate-limit provider choice, external privacy decision, or any requirement to use a real external side effect.

## Definition of Done

The public E2E path, security boundary, concurrency proof, documentation, state, and checkpoint are complete.
