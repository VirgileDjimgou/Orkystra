# Development Commands

Run commands from the repository root unless noted.

## Autonomous Continuation

Use this command in Codex or GitHub Copilot when you want a longer supervised run:

```text
Smart Logistic continue
```

This means: read project memory, then execute up to 5 consecutive unfinished sprints, one sprint at a time.

The agent must stop early if a sprint needs human approval, secrets, paid external services, destructive migration work, or a build/test failure that cannot be repaired in the current session.

The canonical local prompt is `prompts/SMART_LOGISTIC_CONTINUE_5_SPRINTS.md`. GitHub Copilot also reads `.github/copilot-instructions.md`, which contains the same batch-mode rule in a tracked file.

## Backend

```powershell
dotnet restore backend/Orkystra.slnx
dotnet build backend/Orkystra.slnx --configuration Release /p:UseSharedCompilation=false /nodeReuse:false
dotnet test backend/Orkystra.slnx --configuration Release /p:UseSharedCompilation=false /nodeReuse:false
$env:Security__ApiKey='replace-with-local-key'
dotnet run --project backend/src/Orkystra.Api
```

Protected operational API routes expect:

- `X-Api-Key`
- `X-Tenant-Id` when tenant-header enforcement is enabled

Useful local warehouse endpoints once the API is running:

- `GET /api/control-tower/overview`
- `GET /observability/event-backbone`
- `GET /api/simulation/scenarios`
- `POST /api/simulation/scenarios/demo-events`
- `GET /api/gps/positions`
- `POST /api/gps/positions/publish`
- `GET /api/warehouses`
- `GET /api/warehouses/{warehouseId}`
- `GET /api/transport/routes`
- `GET /api/transport/routes/{routeId}`
- `GET /api/transport/sync-status`
- `POST /api/transport/sync`
- `POST /api/ai/recommendations`
- `POST /api/transport/routes/{routeId}/optimization`
- `GET /api/providers/catalog`
- `GET /observability/persistence/projections`
- `GET /observability/persistence/workflows`

The development API key is intentionally not committed. Provide it through environment variables or an ignored local configuration file.
The API now allows local Vite origins on `127.0.0.1` and `localhost` for development workflows, so the operator UI can call the protected backend directly during local browser sessions.
The AI workflow endpoint proxies the current control-tower overview into the Python AI service, so the browser only needs to ask a question and the backend supplies the grounded projection context.
The operational persistence layer stores snapshots and workflow runs in `backend/src/Orkystra.Api/output/persistence/orkystra-operations.db` by default, which makes recent backend state queryable without reading ad hoc JSON files.
The REST transport connector can now switch from demo fallback to live upstream reads when its local runtime configuration contains a valid `baseUrl`; placeholder hosts such as `.invalid` intentionally remain in fallback mode so local demos do not start failing on outbound calls.

### MQTT event backbone

The API now activates the first real MQTT event flow through Mosquitto. The current slice is intentionally narrow: demo simulation events are published onto MQTT, consumed back into the API, then projected into `ScenarioSummaryReadModel` instances through the existing idempotent projection runner.

Useful local event-backbone checks:

```powershell
# Publish a demo scenario sequence onto MQTT via the API
curl -X POST http://127.0.0.1:5043/api/simulation/scenarios/demo-events `
  -H "X-Api-Key: your-dev-key" `
  -H "Content-Type: application/json" `
  -d '{"name":"MQTT Demo","seed":42,"advanceMinutes":15,"includeDisruption":true,"completeScenario":true}'

# Inspect the current MQTT-backed scenario projections
curl http://127.0.0.1:5043/api/simulation/scenarios `
  -H "X-Api-Key: your-dev-key"

# Inspect publish/consume telemetry for the backbone itself
curl http://127.0.0.1:5043/observability/event-backbone `
  -H "X-Api-Key: your-dev-key"
```

The MQTT broker URL is configured through `EventBackbone:BrokerUrl` in `backend/src/Orkystra.Api/appsettings.json` and defaults to the local Mosquitto container on `mqtt://localhost:1883`.

### GPS telematics stream

The first connector-originated MQTT slice now uses the GPS provider. The workflow is:

1. `GpsTelematicsProvider` returns canonical `GpsPositionSnapshot` data.
2. `POST /api/gps/positions/publish` publishes those snapshots to the configured GPS stream topic.
3. The MQTT consumer subscribes to that stream topic and dispatches each telemetry event through the idempotent projection runner.
4. `GET /api/gps/positions` returns the latest projected truck positions.

Useful local GPS checks:

```powershell
# Publish the latest provider GPS positions into MQTT
curl -X POST http://127.0.0.1:5043/api/gps/positions/publish `
  -H "X-Api-Key: your-dev-key"

# Read the latest projected GPS positions
curl http://127.0.0.1:5043/api/gps/positions `
  -H "X-Api-Key: your-dev-key"
```

The GPS stream topic comes from `ProviderRuntime:Providers[gps-telematics-adapter].Settings.streamTopic` and defaults to `fleet/gps/demo` in the local demo configuration.

### Live provider authentication

The REST transport adapter supports API-key-style upstream authentication. The secret is kept separate from the regular provider runtime settings to prevent accidental commits.

Supply the API key through one of these two paths:

**Environment variable (takes precedence)**

```powershell
$env:ORKYSTRA_PROVIDER_REST_TRANSPORT_ADAPTER_APIKEY='your-key-here'
dotnet run --project backend/src/Orkystra.Api
```

**Local secrets file via the API (operator workflow)**

```powershell
# With a running API:
curl -X PUT http://127.0.0.1:5043/api/providers/catalog/rest-transport-adapter/secrets \
  -H "X-Api-Key: your-dev-key" \
  -H "Content-Type: application/json" \
  -d '{"secretKey":"apiKey","secretValue":"your-upstream-key"}'
```

Secrets are persisted into `backend/src/Orkystra.Api/appsettings.Secrets.local.json`, which is covered by the `*.local.json` gitignore pattern and will never be committed.

The provider catalog shows `API key: configured` or `API key: not set` depending on secret presence, without ever exposing the key value in any API response.

The operator workspace includes a **Set API key** form in the connector catalog card for any provider whose `authMode` is not `none`. The form sends the key through the secrets endpoint and clears the value from the browser after saving.

## Frontend

```powershell
cd frontend/web
npm install
npm run build
$env:VITE_API_BASE_URL='http://127.0.0.1:5043'
$env:VITE_API_KEY='replace-with-local-key'
npm run dev
```

For repeatable local browser work, you can also place `VITE_API_BASE_URL`, `VITE_API_KEY`, and `VITE_TENANT_ID` in `frontend/web/.env.local`. That file is ignored by Git.
The operator workspace now retries transient local API failures and exposes a manual `Refresh data` action, so browser sessions can recover more gracefully when the API or Vite server is restarted during development.
The warehouse twin now loads detailed zone and dock posture from `GET /api/warehouses/{warehouseId}` instead of relying only on control-tower summary shaping in the browser.
The transport board now loads detailed route, shipment, and delivery posture from `GET /api/transport/routes/{routeId}` instead of relying only on overview shaping in the browser.
The AI workflow panel now sends the current question to `POST /api/ai/recommendations`, and the backend routes the request through the AI service with explicit evidence, assumptions, and missing-data handling.
The optimization workflow panel now sends the selected route and scenario context to `POST /api/transport/routes/{routeId}/optimization`, and the backend routes the request through the optimization service with a resilient local fallback.
The backend now persists key snapshots and workflow envelopes centrally, so `GET /observability/persistence/projections` and `GET /observability/persistence/workflows` are useful when tracing recent state transitions during local debugging.
The frontend now exposes those same observability feeds through an `Operational trace` surface, which makes recent persisted runs and audit evidence visible during local demos without opening backend files.
The provider catalog remains the right place to edit non-secret transport runtime settings locally; once a real `baseUrl` is saved, the backend transport provider can hydrate route summaries and route details from `/routes` and `/routes/details` on the configured upstream.

### Transport snapshot sync

The transport slice now has an explicit import step that persists a reusable snapshot for the current tenant.

Useful local transport-sync checks:

```powershell
# Import the current transport snapshot from the configured provider
curl -X POST http://127.0.0.1:5043/api/transport/sync `
  -H "X-Api-Key: your-dev-key" `
  -H "X-Tenant-Id: local-demo-tenant"

# Inspect the latest sync evidence
curl http://127.0.0.1:5043/api/transport/sync-status `
  -H "X-Api-Key: your-dev-key" `
  -H "X-Tenant-Id: local-demo-tenant"

# Compare latest vs previous imported snapshots
curl http://127.0.0.1:5043/api/transport/sync-diff `
  -H "X-Api-Key: your-dev-key" `
  -H "X-Tenant-Id: local-demo-tenant"

# Review transport exception workbench
curl http://127.0.0.1:5043/api/transport/exceptions-workbench `
  -H "X-Api-Key: your-dev-key" `
  -H "X-Tenant-Id: local-demo-tenant"

# Read persisted exception resolutions
curl http://127.0.0.1:5043/api/transport/exceptions-workbench/resolutions `
  -H "X-Api-Key: your-dev-key" `
  -H "X-Tenant-Id: local-demo-tenant"

# Read recent exception resolution history
curl http://127.0.0.1:5043/api/transport/exceptions-workbench/resolution-history?count=12 `
  -H "X-Api-Key: your-dev-key" `
  -H "X-Tenant-Id: local-demo-tenant"
```

When a persisted transport snapshot exists, `GET /api/transport/routes` and `GET /api/transport/routes/{routeId}` can reuse that tenant snapshot instead of forcing a fresh upstream read every time. This makes local demos and future support flows more stable, and it gives later sprints a clearer synchronization boundary.
The operator workspace now surfaces that same transport sync evidence directly in the transport board, including source posture, imported route count, last import freshness, and a local `Import snapshot` action for the current tenant.
The transport board now also tells a small route-by-route story around that sync evidence: it shows whether the selected route is present in the latest import, how far deliveries have progressed, and the most recent transport-sync timeline entries recorded in the operational trace.
The transport board now includes a compact support layer for operator recovery: shortcut actions to refresh sync evidence, reload the selected route, re-run optimization, and jump to a route confirmed by the latest import, plus recovery cues that explain which follow-up action is most useful when transport data is stale, degraded, or outside the latest snapshot.
The historical diff panel now supports operator triage as well: you can filter deltas by changed, added, removed, selected-route, or all evidence, and jump directly from a diff row back into the current route detail when that route still exists in the latest board.
The transport sync card now shows a freshness state with an approximate age for the latest persisted snapshot, and the board surfaces escalation cues that tell the operator when the import is getting old enough that it should no longer be trusted at face value.
The transport board now includes a historical diff drill-down that compares the latest two imported snapshots, including route counts, added/removed/changed totals, and route-level change summaries.
The transport board now also includes a dedicated exception workbench that turns sync posture, delayed routes, missing imported routes, and recent import deltas into a prioritized operator shortlist with direct jump actions.
The exception workbench now also supports grouped review: operators can filter by exception family, review the next outstanding exception in sequence, and locally mark items as reviewed during a support pass without losing the underlying transport evidence.
The review loop is now auditable as well: exception items can carry a persisted resolution status, timestamp, and short support note, and the local tenant can save reviewed or resolved posture through the exception-resolution ledger API.
The workbench now separates that posture explicitly with resolution-aware filters for `Open`, `Reviewed`, `Resolved`, `Deferred`, and `All`, so operators can move between active and closed exceptions without rebuilding their mental queue.
The same surface now exposes open-vs-closed metrics, inline resolution notes, and direct `Save review`, `Defer`, and `Resolve` actions on each exception row.
The transport board now also exposes recent resolution history as a dedicated operator card, which separates the latest current posture for the focused exception from previous saved updates and broader recent activity.
The latest freshness warning now drills through to a dedicated evidence panel that ties the snapshot age back to the last import time, sync posture, historical diff, selected route, and latest sync note, so the operator can move from symptom to cause without leaving the transport board.
The freshness story now extends one step further with a lineage card, a sync cadence card, a trust badge, a selected-route spotlight, and a short operator checklist so the next action is obvious when the snapshot starts to age.
Chrome-driven browser QA confirmed that the five-card transport freshness cluster stays readable together at desktop width.
The mobile viewport polish pass tightened the freshness action cards so they stack cleanly on narrow screens, and the freshness actions now share a consistent height and spacing rhythm.
The latest accessibility pass added 48px touch targets, visible keyboard focus, and explicit accessible labels across the transport freshness action cluster.
The cluster now opens with a posture banner that summarizes whether the current transport snapshot is reliable, drifting, fallback-only, or illustrative, and the diff and route-focus shortcuts now disable themselves with explicit reason text whenever the required import evidence is not available yet.
The freshness header is now denser and easier to scan: it starts with a compact four-step timeline, a digest chip row for trust/cadence/source/route posture, and quick links that jump directly into drill-through, lineage, cadence, trust, route, or checklist sections.
The freshness card summaries were also tightened so the operator can read the shared timeline once and use the lower cards for explanation rather than re-reading the same timestamps in slightly different wording.
The latest hierarchy pass adds a dedicated primary-concern panel inside the freshness header, pairing the main risk with one recommended action and one priority section target so degraded or fallback states no longer force the operator to infer what matters most.
The priority section is now visually emphasized in the cluster itself, and the matching quick link is promoted in the header so the user can move from posture to the most decision-critical card in one step.
The priority layer now speaks more crisply as well: each state has a shorter urgency label, a tighter main title, and a one-line action reason that explains why the recommended move is the right next step.
The navigation copy inside the priority layer was normalized around `Go to ...`, which keeps the action rail and quick-link language more consistent when operators bounce between the header and the detailed freshness cards.

## Python Services

```powershell
python -m compileall python-services
cd python-services/ai-service/src
python -m uvicorn orkystra_ai_service.app:app --host 127.0.0.1 --port 8001
cd ../../optimization-service/src
python -m uvicorn orkystra_optimization_service.app:app --host 127.0.0.1 --port 8002
```

The Python service dependencies are declared in `python-services/pyproject.toml`. Install them in a virtual environment before running FastAPI apps.

## Infrastructure

```powershell
docker compose -f infrastructure/docker-compose.yml up -d
docker compose -f infrastructure/docker-compose.stack.yml up -d --build
```

Local infrastructure includes PostgreSQL, Mosquitto MQTT, and Qdrant.
The stack compose file also brings up the API, frontend, AI service, and optimization service.
