# Development Commands

Run commands from the repository root unless noted.

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
- `GET /api/warehouses`
- `GET /api/warehouses/{warehouseId}`
- `GET /api/transport/routes`
- `GET /api/transport/routes/{routeId}`
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
