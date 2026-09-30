# Demo Mode Security Boundary

Runtime modes have separate purposes:

| Mode | Data | Entry | Side effects |
|---|---|---|---|
| Development | local fictitious data | developer workflow | local test adapters |
| Demo | dedicated synthetic tenant | rate-limited `Launch Live Demo` | sandboxed only |
| Production | customer data | normal authenticated users | configured production providers |

Demo is not Production with Development seeds enabled. `ProductionConfigurationValidator` remains strict. Demo has explicit configuration validation, synthetic-only startup/reset, no customer data, no reusable browser credentials, no real outbound e-mail/webhook without an explicit sandbox, and prominent `SIMULATED DEMO` labelling.

The launch endpoint issues a server-side, short-lived, scoped DemoOperator HttpOnly session. It uses existing CSRF/session controls, bounded lifetime, least privilege, and server-side rate limiting. The browser never receives a seeded administrator password. Demo controls are authorized server-side, visible only in Demo mode, and cannot cross visitor or tenant boundaries.

Release validation includes session expiry, denied sensitive administration, reset concurrency, launch limits, side-effect sandboxing, and cross-tenant denial.

## Implemented boundary (Sprint 29)

- `ASPNETCORE_ENVIRONMENT=Demo` is validated independently. It requires `Bootstrap:PublicDemoOnly`, `PublicDemo:Enabled`, sandboxed side effects, and bounded TTL/capacity values. Production still rejects demo seed and public launch.
- Startup provisions exactly one `public-demo` tenant in the real Demo profile. Its Operator identity has no password, so it cannot pass the ordinary login endpoint.
- `POST /api/v1/demo/public/launch` is fixed-window limited by client address, enforces a concurrent-session ceiling, creates a server-side `public-demo` session, and returns only user metadata plus the CSRF token. The JWT remains in an HttpOnly, SameSite=Strict cookie.
- A signed `demo_session` claim is checked against `UserSession.ClientType` during every authentication. Demo sessions are read-only across ordinary APIs; only scenario controls and logout may mutate state. Admin, integration administration, session listing, and internal APIs are denied.
- The synthetic fleet view is intentionally shared and immutable. Scenario selection/run/pause/reset state is private to the session identifier, expires with that session, and is removed in bounded batches. This prevents one visitor from controlling another visitor's view.
- The compact control bar and `SIMULATED DEMO` label are rendered only when the server-issued identity says `isDemo=true`. Expiry returns the browser to `/demo?expired=1` for a safe relaunch.

The initial hosted topology is one API instance; the private scenario-control store is process-local. A future multi-instance deployment must replace it with a shared bounded store before horizontal scaling.

## Local container profile

Validate or run the explicit overlay with:

```powershell
docker compose --env-file .env -f docker-compose.yml -f docker-compose.pilot.yml -f docker-compose.demo.yml config
```

The overlay clears bootstrap administrator values and enables only the passwordless public Demo tenant. The Demo Worker engine reaches the internal ingestion/discovery endpoints through the bounded service channel `InternalApi:Key` (`X-FleetOps-Internal-Key`); anonymous internal access is still refused, public Demo sessions remain read-only, and Production keeps the internal endpoints `404`. Production continues to use `docker-compose.pilot.yml` without this overlay.

Simplified entry points: `scripts/demo-up.ps1`, `scripts/demo-smoke.ps1`, `scripts/demo-down.ps1`. See [DEPLOYMENT_TOPOLOGY.md](DEPLOYMENT_TOPOLOGY.md).
