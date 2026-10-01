# SPRINT-38 — Self-Hosted Public MVP & Final Demo Proof

## Status

`PLANNED`

## 1. Objective

Make the MVP usable from the outside on the developer's machine: a remote browser and a real Android phone (Wi-Fi and 4G/5G) use the hosted Demo over HTTPS, SignalR works over the Internet, delivery proof uploads from a real phone, SQL Server and MinIO administration are never exposed, and the complete final scenario is demonstrated and documented from a clean machine.

## 2. Context

- Docker Compose profiles already exist: `docker-compose.yml` (infrastructure), `docker-compose.pilot.yml` (Production-like services), `docker-compose.demo.yml` (Demo overlay with the engine).
- Web nginx already proxies `/api` and `/hubs` to the API on one origin; the Android app reads its API base URL at build time (`FLEETOPS_API_URL`) and already works with `adb reverse` locally.
- The hosted Demo smoke (`scripts/demo-smoke.ps1`) validates readiness, launch, sandbox, reset, and the animated fleet.
- Hosting decisions remain open human gates: final commercial name, map-tile provider, and now a public ingress/tunnel provider with DNS.
- Non-negotiable: no database or object-storage administration port exposed to the Internet; secrets only through environment/secret storage, never Git.

## 3. In Scope

- A documented and automated self-hosted profile:
  - Compose-based deployment with the Demo overlay;
  - HTTPS/public ingress through Cloudflare Tunnel or an equivalent documented abstraction (reverse proxy → Web/API), keeping SQL Server and MinIO private;
  - health/readiness checks, restart policies, persistent volumes;
  - documented backup/restore (reuse `scripts/sql-backup.ps1`, `scripts/sql-restore.ps1`) and media persistence;
  - secrets via `.env`/secret storage with rotation guidance.
- Android configuration for a public HTTPS URL and validation on a physical device over Wi-Fi and mobile data.
- Remote validation: SignalR over the Internet, proof upload from the phone, public Demo session on a remote browser.
- Documentation of CGNAT/tunnel constraints; Tailscale allowed only as a private admin option if needed.

## 4. Out of Scope

- No cloud provider migration, no Kubernetes, no managed database.
- No auto-deployment, push, or merge.
- No new product feature.
- No exposure of SQL Server, MinIO console, or any admin service to the Internet.
- No public release under a commercial name until the open human gates are resolved.

## 5. Architecture Constraints

- Keep the modular monolith, Docker Compose, and the existing single-origin proxy design.
- TLS terminates at the tunnel/reverse proxy; the app trust boundary is unchanged.
- Demo and Production profiles stay distinct; Production validation is never weakened.
- The Android app remains offline-first and must tolerate intermittent connectivity; only its base URL becomes configurable.

## 6. Implementation Tasks

1. Author a self-hosted runbook (`docs/02-engineering/SELF_HOSTED_DEPLOYMENT.md`) with a clean-machine procedure and prerequisites.
2. Add/verify the ingress abstraction: Cloudflare Tunnel (recommended) or equivalent, with DNS, TLS, and WebSocket support for SignalR; document alternatives and CGNAT behavior.
3. Harden the exposed surface: only 443 (and optionally 80 for redirect) reachable; SQL Server, MinIO API/console, MQTT, and Mailpit remain private; verify with an external port check.
4. Ensure `restart: unless-stopped` (or equivalent) for API, Worker, Web, and infrastructure; verify volumes persist across restarts and upgrades.
5. Extend health/readiness usage to the ingress (tunnel origin health) and document rollback.
6. Configure the Android build for a public HTTPS base URL (`FLEETOPS_API_URL=https://…/`) and validate login, missions, inspection, proof upload, and SignalR-independent resilience on a physical device over Wi-Fi and 4G/5G.
7. Extend the hosted smoke for the public origin (health, launch, fleet, one agent workflow if Sprint 37 landed, proof availability).
8. Validate the final scenario end to end (see Demo proof) and record limitations honestly.

## 7. Required tests

- Clean-machine deployment reproduction (documented commands executed on this repository).
- Remote browser: launch Demo, observe fleet, inspect vehicle/mission/exception.
- Physical Android: login over the public URL, mission list, inspection, proof photo/signature, completion.
- SignalR over the Internet (live updates observed remotely).
- Exposure check: ports 1433/9000/9001/1883 not reachable from the Internet.
- Restart persistence: stop/start stack, data and sessions behavior verified.
- Backup/restore executed once.
- Hosted smoke on the public origin.
- Full quality gate (unchanged).

## 8. Security / Tenant Validation

- No admin/infrastructure service exposed; only the Web/API origin.
- Secrets never committed; `.env` remains local and is not pushed.
- Demo sessions remain short-lived, read-only, rate-limited; no customer data.
- Production profile untouched by the self-hosted Demo ingress.
- Tenant isolation unchanged and verified by existing tests.

## 9. Definition of Done

- A reproducible deployment from a clean machine is documented and proven.
- Remote browser, physical Android (Wi-Fi and mobile), and SignalR over the Internet work.
- No database/admin service is Internet-reachable; secrets are environment-supplied.
- The final scenario is demonstrated end to end; remaining limits are documented in the release checklist.
- Acceptance criteria checked; local checkpoint created.

## 10. Evidence to Record

- Deployment runbook with exact commands and versions.
- Smoke logs for the public origin; remote browser and Android recordings/screenshots.
- External exposure check output.
- Backup/restore proof and restart persistence proof.
- Updated release checklist, hosted-demo runbook, and `sprint38-quality-gate.log`.

## 11. Stop Conditions

- DNS/provider credentials or a domain decision are required → human gate (expected).
- The chosen tunnel requires exposing an admin port → stop; redesign the ingress.
- Mobile carrier/CGNAT makes a direct ingress impossible and no tunnel is approved → human gate.

## 12. Dependencies

- `SPRINT-33`–`SPRINT-37` complete.
- Human gates: final commercial name, map-tile provider, ingress/domain approval.

## Acceptance criteria

- [ ] Clean-machine self-hosted deployment reproducible from documented commands.
- [ ] Remote browser works over HTTPS.
- [ ] Physical Android device works over Wi-Fi and 4G/5G against the public HTTPS URL.
- [ ] SignalR live updates work over the Internet.
- [ ] Proof upload works from a real phone.
- [ ] No SQL/MinIO/admin service is directly exposed to the Internet.
- [ ] Restart policies, volumes, and backup/restore are proven.
- [ ] The complete final scenario is demonstrated and remaining limits documented.
- [ ] Full quality gate passes and `main` stays green.

## Demo proof

Launch Demo → 12–20 vehicles visible → continuous movement → select vehicle → driver and mission visible → virtual driver inspection → mission start → travel → stop arrival → delay or incident → exception created → timeline updated → proof photo/signature → mission completed → coherent final state.
