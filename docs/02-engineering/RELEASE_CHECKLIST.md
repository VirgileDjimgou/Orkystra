# Hosted Demo Release Checklist

Every image, configuration, and screenshot in this checklist is a demonstration artifact. Reports and captures are `SIMULATED DEVELOPMENT EVIDENCE — NOT PILOT OR COMMERCIAL PROOF`.

## Tested facts

| Check | Where | Result |
|---|---|---|
| Demo compose configuration is valid (base + pilot + demo overlays) | `scripts/quality-gate.ps1`, `scripts/demo-smoke.ps1 -ConfigOnly` | PASSED |
| Clean hosted Demo stack starts from documented `.env` values | `scripts/demo-smoke.ps1` | PASSED |
| API liveness and readiness (migrated database) | `/health`, `/health/ready` during Demo smoke | PASSED |
| Public launch is enabled and labelled `SIMULATED DEMO` | `/api/v1/demo/public/status`, smoke | PASSED |
| Demo session is least-privilege and cannot read administration or identity sessions | smoke, `PublicDemoIntegrationTests` | PASSED |
| Scenario RESET returns private state to `READY` | smoke, `PublicDemoIntegrationTests` | PASSED |
| Deterministic engine animates the synthetic public fleet (12 tracked vehicles) | smoke, Playwright public journey | PASSED |
| Anonymous internal engine access is refused in Demo; the configured key is required | `PublicDemoIntegrationTests.InternalEngineChannelRequiresConfiguredKeyInDemoProfile` | PASSED |
| Public journey: launch → fleet → vehicle/mission → exception | Playwright `public demo journey observes the animated fleet, vehicle mission, and exception` | PASSED |
| Operations journey: proof evidence → delayed exception → agent activity | Playwright `operations journey records proof evidence, a delayed exception, and agent activity` | PASSED |
| Web format, lint, 32 Vitest tests, production build, 10 Playwright journeys | `scripts/quality-gate.ps1` | PASSED |
| Backend format, Release build, fast/Reliability/MinIO/SQL Server suites | `scripts/quality-gate.ps1` | PASSED |
| Multi-tenant simulation (33 steps) | `scripts/run-full-simulation.ps1` inside the gate | PASSED |
| CI release validation (backend, web, Playwright, compose config) | `.github/workflows/release-validation.yml` | DEFINED — runs on push/PR |
| Current screenshots labelled simulated | `docs/assets/screenshots/demo-*.png`, `docs/00-product/DEMO_WALKTHROUGH.md` | PASSED |

## Remaining limitations (honest)

- Hosted virtual drivers are not provisioned: the public Demo identity is intentionally passwordless, and per-agent credentials must not be fabricated in a clean deployment. The hosted profile animates the fleet with the deterministic engine; agent activity is proven through the development simulation and the Playwright operations journey.
- Load above 20 vehicles is not tested (the Demo engine is bounded to 20).
- Demo session state and process metrics are process-local; horizontal scaling needs a shared bounded store.
- The map tile provider for public hosting remains an explicit human decision; the repository uses OpenStreetMap tiles for local/development use only.
- CI covers a bounded subset (no SQL Server, MinIO, Reliability, or Android jobs) to stay fast; the full gate runs locally.
- Screenshots were captured against an isolated, in-memory DemoTesting API, not against the hosted deployment.
- No public deployment, DNS, or domain is part of this release; hosting remains user-authorized.

## Rollback

1. Stop public Demo ingress and expire/revoke Demo sessions.
2. Run `pwsh -File scripts/demo-down.ps1` and, for a clean slate, add `-RemoveVolumes`.
3. Restore the previous image/configuration set; the pilot and local profiles are unaffected.
4. Do not modify Production tenant data during rollback.

## Operations ownership

- Repository owner: Orkystra FleetOps maintainer.
- Release decision, hosting provider, DNS/domain, tile policy, and legal/brand approval remain human gates recorded in `.agent/PROJECT_STATE.json`.
- Incident path for a hosted Demo: disable `PublicDemo:Enabled`, stop the overlay, inspect API/Worker logs, then re-deploy the last known-good configuration.
