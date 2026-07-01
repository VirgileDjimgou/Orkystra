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
- `GET /health/sanity` (anonymous — component connectivity check)
- `POST /api/bootstrap/demo` (protected — one-step seeded demo setup)
- `GET /observability/persistence/projections`
- `GET /observability/persistence/workflows`

The development API key is intentionally not committed. Provide it through environment variables or an ignored local configuration file.
The API now allows local Vite origins on `127.0.0.1` and `localhost` for development workflows, so the operator UI can call the protected backend directly during local browser sessions.
The AI workflow endpoint proxies the current control-tower overview into the Python AI service, so the browser only needs to ask a question and the backend supplies the grounded projection context.
The operational persistence layer stores snapshots and workflow runs in `backend/src/Orkystra.Api/output/persistence/orkystra-operations.db` by default (SQLite), which makes recent backend state queryable without reading ad hoc JSON files.

### Postgres operational persistence

The persistence layer supports both SQLite (default, no external dependencies) and PostgreSQL for multi-session self-hosted deployments.

**Configuration:**

The provider is selected through the `OperationalPersistence` section in `appsettings.json`:

```json
"OperationalPersistence": {
  "Provider": "sqlite",
  "DatabasePath": "output/persistence/orkystra-operations.db",
  "ConnectionString": "Host=localhost;Port=5432;Database=orkystra;Username=orkystra;Password=orkystra",
  "ReadLimit": 200
}
```

- Set `Provider` to `"sqlite"` for local file-based persistence (default, no external services required).
- Set `Provider` to `"postgres"` to use PostgreSQL. The `ConnectionString` must point to a running PostgreSQL instance.

**Postgres setup:**

1. Start the PostgreSQL container from the infrastructure compose file:
   ```powershell
   docker compose -f infrastructure/docker-compose.yml up -d postgres
   ```
2. Update `OperationalPersistence:Provider` to `"postgres"` and verify `ConnectionString` matches your Postgres instance.
3. Tables (`projection_snapshots`, `workflow_runs`) are auto-created on first use. No manual migration scripts are required.
4. Restart the API. It will use Postgres for all operational persistence operations.

**Migration from SQLite to Postgres:**

Data is not automatically migrated. To preserve existing data when switching from SQLite to Postgres:

1. Stop the API.
2. Start a Postgres instance and create the `orkystra` database.
3. Restart the API with `Provider: "postgres"`. New projections and workflow runs will be written to Postgres.
4. Previous SQLite data remains in `orkystra-operations.db` for reference.

**Postgres schema:**

Tables are created with `IF NOT EXISTS` on first connection:

- `projection_snapshots`: Stores read-model projections with upsert semantics (`ON CONFLICT DO UPDATE`). Columns: `tenant_id`, `projection_name`, `projection_key`, `source`, `captured_at_utc`, `payload_json`.
- `workflow_runs`: Append-only workflow execution records. Columns: `id` (BIGSERIAL), `tenant_id`, `workflow_kind`, `subject_key`, `scenario_id`, `source`, `status`, `created_at_utc`, `payload_json`. Indexed on `(tenant_id, workflow_kind, created_at_utc DESC)`.
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

### Durable outbox and inbox

The inbox state store and outbox are now backed by the operational persistence database instead of in-memory-only state. This means processed message deduplication and published event records survive process restarts.

**Configuration:**

The outbox and inbox share the same database as the operational persistence layer. No additional configuration is needed beyond the `OperationalPersistence` settings described above.

**Protected outbox endpoints:**

```powershell
# Inspect recent outbox entries (includes pending, published, and failed)
curl http://127.0.0.1:5043/observability/event-backbone/outbox?count=20 `
  -H "X-Api-Key: your-dev-key"

# Replay pending or failed outbox entries through the MQTT publisher
curl -X POST http://127.0.0.1:5043/observability/event-backbone/replay `
  -H "X-Api-Key: your-dev-key"
```

The replay endpoint returns `{ replayed, failed, total }` describing how many pending entries were successfully republished.

**Deduplication behavior:**

The durable inbox uses `(consumer_name, message_id)` as a primary key. If a message is received after a restart, the inbox store returns `true` for `HasProcessedAsync`, and the idempotent projection runner skips the duplicate. This ensures exactly-once projection semantics across restarts as long as the same database is used.

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

# Read deferred exception follow-up queue
curl http://127.0.0.1:5043/api/transport/exceptions-workbench/follow-up-queue `
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
Deferred outcomes now feed a dedicated follow-up queue as well, so the team can distinguish exceptions that are closed from exceptions that were deliberately deferred and still need a return pass.
Deferred outcomes can now carry a lightweight commitment too: a follow-up owner and a target return window, both persisted through the same exception-resolution flow and surfaced back into the workbench and follow-up queue.
The follow-up queue now flags risky commitments directly as well, with a compact alert posture for overdue return windows and missing owners so deferred transport work is easier to escalate before it disappears into backlog drift.
The same queue now auto-prioritizes those commitments too: overdue and ownerless items float to the top, and the surface calls out the next best follow-up target explicitly so operators know where to re-enter first.
Follow-up work now has an explicit lifecycle as well: operators can retire or reopen deferred commitments without erasing their note trail, owner, or target return window history.
The follow-up queue now reads more like an operator SLA surface than a raw deferred list: each item carries `Healthy`, `At Risk`, `Overdue`, or `Retired` posture, the queue exposes an at-risk metric and escalation digest, and top owner lanes summarize where deferred return work is accumulating.
Operators can now also filter the queue by active, at-risk, overdue, ownerless, retired, or watchlist lanes, then lock onto a specific follow-up item with a small spotlight panel that echoes its latest saved history updates.
The transport surface now also derives an explicit shift-handoff pack from that same active deferred queue: it exposes a compact shortlist for the next operator pass, readiness posture for each handoff item, current-vs-next-shift timing, owner-lane pressure, and short briefing lines that can be reused during support handoff.
The same handoff flow now includes explicit acknowledgement support, so the next operator can mark a deferred item as received without retiring it or changing the underlying follow-up posture.
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

## Environment Variable Configuration

All `appsettings.json` settings can be overridden with environment variables. The API validates critical settings on startup and will log a clear message if required values are missing.

**Naming convention:**

Use double-underscore (`__`) as the key separator for nested settings. For example, `Security:ApiKey` in JSON becomes `Security__ApiKey` as an environment variable.

**Required setting:**

| Variable | Description |
|---|---|
| `Security__ApiKey` | API key for all protected endpoints. Must be non-empty or the API will not start. |

**Optional settings with defaults:**

| Variable | Default | Description |
|---|---|---|
| `TenantAccess__DefaultTenantId` | `local-demo-tenant` | Default tenant when header is not required |
| `TenantAccess__RequireTenantHeader` | `false` | Set to `true` to enforce tenant header |
| `Observability__AuditLogFilePath` | `output/audit/audit-log.jsonl` | Path for audit log file |
| `Observability__EnableAuditLogging` | `true` | Enable or disable audit logging |
| `OperationalPersistence__Provider` | `sqlite` | `sqlite` or `postgres` |
| `OperationalPersistence__DatabasePath` | `output/persistence/orkystra-operations.db` | SQLite database path |
| `OperationalPersistence__ConnectionString` | `Host=localhost;...` | Postgres connection string |
| `EventBackbone__BrokerUrl` | `mqtt://localhost:1883` | MQTT broker URL |
| `EventBackbone__Enabled` | `true` | Enable or disable MQTT event backbone |
| `AiService__BaseUrl` | `http://127.0.0.1:8001` | AI recommendation service URL |
| `AiService__TimeoutSeconds` | `8` | AI service HTTP timeout |
| `OptimizationService__BaseUrl` | `http://127.0.0.1:8002` | Optimization service URL |
| `OptimizationService__TimeoutSeconds` | `8` | Optimization service HTTP timeout |

**Provider secrets:**

Provider secrets use a separate naming convention with the `ORKYSTRA_PROVIDER_` prefix:

```
ORKYSTRA_PROVIDER_{PROVIDER_ID}_{FIELD}
```

Provider IDs with hyphens are normalized to uppercase with underscores. For example, the REST transport adapter API key:

```powershell
$env:ORKYSTRA_PROVIDER_REST_TRANSPORT_ADAPTER_APIKEY='your-api-key-here'
```

**Quick-start (PowerShell):**

```powershell
$env:Security__ApiKey='my-local-key'
$env:OperationalPersistence__Provider='sqlite'
dotnet run --project backend/src/Orkystra.Api
```

**Quick-start (Linux/macOS):**

```bash
export Security__ApiKey='my-local-key'
export OperationalPersistence__Provider='sqlite'
dotnet run --project backend/src/Orkystra.Api
```

**Example `.env` file:**

Copy `.env.example` from the repository root and customize as needed:

```powershell
cp .env.example .env
# Edit .env with your settings, then export before running the API
```

The `.env` file itself is not automatically loaded -- you must export the variables or use a tool like `dotenv` in your shell.

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

## Demo Bring-Up (First-Run)

After starting the API and local infrastructure, bootstrap a complete seeded demo state:

```powershell
# 1. Verify all components are reachable
curl http://127.0.0.1:5043/health/sanity

# 2. Bootstrap the demo (deterministic seed, publishes MQTT events, seeds projections)
curl -X POST http://127.0.0.1:5043/api/bootstrap/demo `
  -H "X-Api-Key: your-local-key" `
  -H "Content-Type: application/json" `
  -d '{"scenarioName":"Demo","seed":42,"advanceMinutes":15,"includeDisruption":true}'

# 3. Confirm scenario was created
curl http://127.0.0.1:5043/api/simulation/scenarios `
  -H "X-Api-Key: your-local-key"
```

The bootstrap endpoint creates:
- A deterministic scenario with configurable seed (default: 42, `includeDisruption` adds a dock-blocked event)
- Scenario events published through the MQTT backbone
- Persisted bootstrap workflow record for auditability
- Warehouses, routes, and GPS positions accessible through existing API endpoints
