# Smoke Test Checklist

Use this checklist before a demo, a local release candidate, or a staging handoff.

## 1. Infrastructure

- Start the stack: `docker compose -f infrastructure/docker-compose.stack.yml up -d --build`
- Confirm PostgreSQL, MQTT, and Qdrant containers are healthy enough to stay running
- Confirm API, AI service, optimization service, and web containers are up

## 2. API

- `GET /health/live` returns 200
- `GET /health/ready` returns 200
- `GET /observability/metrics` returns counters
- `GET /observability/context` returns 200 when `X-Api-Key` and `X-Tenant-Id` are present
- Missing API key returns 401 on protected routes
- Missing tenant header only fails when tenant-header enforcement is enabled
- `GET /api/warehouses` returns warehouse summaries with 200
- `GET /api/warehouses/{warehouseId}` returns detailed zones and docks with 200
- `GET /api/transport/routes` returns route summaries with 200
- `GET /api/transport/routes/{routeId}` returns detailed stops, shipments, and deliveries with 200
- `POST /api/ai/recommendations` returns a grounded response with evidence, assumptions, and missing-data fields
- `POST /api/transport/routes/{routeId}/optimization` returns a bounded optimization review with route order, explanation, and alternatives
- `GET /observability/persistence/projections` returns persisted projection snapshots for the active tenant
- `GET /observability/persistence/workflows` returns persisted AI and optimization workflow runs for the active tenant
- When a real transport provider `baseUrl` is configured locally, provider health and route endpoints reflect live upstream posture instead of demo fallback
- When `authMode` is `api-key` and no key is supplied, the catalog reports readiness as `Auth Key Missing` and the health report includes the `auth-key-missing` signal
- When an API key is supplied via environment variable or the secrets endpoint, the catalog reports readiness as `Configured` and health signals include `auth-key-configured`
- `PUT /api/providers/catalog/rest-transport-adapter/secrets` with `{ "secretKey": "apiKey", "secretValue": "..." }` returns 204 and persists the key to the local secrets file
- The secrets endpoint rejects unknown providers (404) and non-secret fields (422)
- The browser catalog card shows `API key: configured` or `API key: not set` without exposing the value
- The `Set API key` form in the catalog card stores and clears the value after a successful save

## 3. Frontend

- Control Tower loads in the browser
- Connection posture shows `API live` for the overview, `Warehouse API live` for the twin detail surface, and `Editable local API` for the provider catalog once the local stack is ready
- Connection posture shows `Transport API live` for the route detail surface once the local stack is ready
- Connection posture shows `Optimization live` for the dispatcher optimization workflow once the local stack is ready
- `Refresh data` recovers cleanly after a local API or Vite restart
- Scenario selector changes visible state
- Warehouse selector swaps between at least two API-backed warehouse detail views
- Warehouse 3D placeholder renders and rotates
- Route selector swaps between at least two API-backed route detail views
- Transport board and detail panels render without overlap on desktop and mobile-width viewports
- AI workflow panel renders a grounded recommendation, shows confidence, and exposes evidence and missing-data context
- Optimization workflow panel renders the current remaining order, a recommended plan, and at least one explanation or fallback trade-off note
- Operational trace surface renders recent workflow runs, persisted snapshots, and recent audit entries without layout overlap
- Provider runtime editor stays enabled only when the catalog is API-backed and shows save feedback after a local configuration update
- Provider catalog and overview health posture make it obvious when transport is using live upstream data versus demo fallback

## 4. Python services

- AI service `/health` returns 200
- AI service `/recommendations/demo/warehouse` returns grounded response fields
- Optimization service `/health` returns 200
- Optimization service `/optimize/demo` returns an explainable route plan

## 5. Quality gates

- `dotnet build backend/Orkystra.slnx --configuration Release /p:UseSharedCompilation=false /nodeReuse:false`
- `dotnet test backend/Orkystra.slnx --configuration Release`
- `npm run build` in `frontend/web`
- `python -m unittest discover python-services/tests`
- `python -m compileall python-services`

## 6. Audit and observability

- Verify API responses include `X-Correlation-Id`
- Verify at least one protected API call produces an audit log entry
- Verify request counters increase after exercising the stack
- Verify at least one recent projection snapshot and one recent workflow run are visible through the persistence observability endpoints
