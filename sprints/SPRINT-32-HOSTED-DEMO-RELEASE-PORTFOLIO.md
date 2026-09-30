# SPRINT-32 — Hosted Demo Release & Portfolio Showcase

## Status

`NOT_STARTED`

## Goal

Produce a reproducible hosted Demo release and concise portfolio walkthrough.

## Why

The prior sprints create the experience; this sprint makes it deployable, inspectable, and honest about simulated evidence.

## Dependencies

Sprints 24–31 complete with their required quality and reliability evidence.

## Scope

- Demo deployment configuration, Docker packaging, migrations, health/readiness, startup/reset lifecycle, and CI release validation;
- hosted smoke test and Playwright public demo journey;
- concise recruiter-facing README, architecture diagram, scenario documentation, screenshots, technical walkthrough, and release checklist;
- documented path: launch → moving fleet → vehicle/mission → exception → agent activity → evidence.

## Non-goals

- no automatic public deployment, push, merge, commercial claim, or statement that simulation equals a customer pilot;
- no new business module.

## Files/modules likely affected

Compose/Docker/config templates, API/Worker Demo host configuration, CI, Playwright, README, docs architecture/engineering, release checklist, and screenshots/assets.

## Required architecture constraints

Deployment remains the modular monolith with SQL Server, object storage, Worker, and Web. Demo secrets are environment supplied; production controls stay distinct. Health/readiness prove dependencies without exposing synthetic tenant data.

## Tasks

1. Make a clean Demo deployment reproducible from documented configuration templates.
2. Validate migrations, health/readiness, reset lifecycle, logs, and side-effect sandbox.
3. Add CI release checks and hosted smoke/E2E journey instructions.
4. Write recruiter-facing README and deep linked architecture/security/agent documents.
5. Capture current screenshots and a concise guided demo with simulated-evidence labels.
6. Complete a release checklist containing tested facts, limitations, rollback, and operations ownership.

## Required tests

- clean-environment Docker/config/migration validation;
- Demo health/readiness and reset smoke test;
- Playwright public journey;
- relevant API/Worker integration tests and Sprint-31 reliability regression;
- documentation link/reference validation and normal quality gate.

## Security checks

Verify no credentials are committed, public launch remains bounded, test data is synthetic, external side effects are sandboxed, and release docs do not publish privileged endpoints or imply commercial/pilot proof.

## Performance checks

Run published smoke path at normal Demo load and confirm the release configuration preserves Sprint-31 measured limits. Do not invent hosted capacity figures.

## Acceptance criteria

- [x] A clean documented deployment can start the Demo profile and become ready. — `scripts/demo-smoke.ps1` built and started the full overlay, then verified `/health`, `/health/ready`, and the web client (`.runtime/sprint32-demo-smoke.log`).
- [x] The public journey launches, observes fleet/agent activity, and completes through Playwright. — `public demo journey observes the animated fleet, vehicle mission, and exception` (launch, 12 moving vehicles, inspector, exception) and `operations journey records proof evidence, a delayed exception, and agent activity` (canonical agent-activity contract); both pass in the full gate.
- [x] Health, migration, reset, side-effect sandbox, and rollback procedures are documented and tested. — smoke assertions plus `docs/01-architecture/DEPLOYMENT_TOPOLOGY.md` and `docs/02-engineering/RELEASE_CHECKLIST.md`.
- [x] README is concise for a recruiter and links deep technical evidence. — rewritten `README.md`.
- [x] Screenshots/scenarios are current and visibly labelled simulated. — `docs/assets/screenshots/demo-*.png` captured from the labelled Demo UI; captions in `docs/00-product/DEMO_WALKTHROUGH.md`.
- [x] Release checklist records actual validation and remaining limitations. — `docs/02-engineering/RELEASE_CHECKLIST.md` (hosted virtual drivers not provisioned, 20-vehicle bound, process-local stores, tile-provider decision, no deployment).

## Demo proof

1. Launch Live Demo.
2. Observe moving fleet.
3. Select vehicle, driver, and mission.
4. Observe exception and virtual-driver response.
5. Inspect agent activity and reliability/architecture evidence.

## Rollback

Stop public Demo ingress, expire Demo sessions, disable the Demo runtime, and restore the last known-good image/configuration. Do not modify Production tenant data.

## Human gates

Stop for a hosting/provider credential, DNS/domain, legal/brand approval, or any request to deploy/publish not explicitly authorized.

## Definition of Done

Every acceptance criterion, release checklist, security review, full gate, state/handoff, and local checkpoint is complete; deployment remains user-authorized.
