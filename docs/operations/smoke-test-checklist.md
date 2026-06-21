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

## 3. Frontend

- Control Tower loads in the browser
- Connection posture shows `API live` for the overview, `Warehouse API live` for the twin detail surface, and `Editable local API` for the provider catalog once the local stack is ready
- `Refresh data` recovers cleanly after a local API or Vite restart
- Scenario selector changes visible state
- Warehouse selector swaps between at least two API-backed warehouse detail views
- Warehouse 3D placeholder renders and rotates
- Transport board and detail panels render without overlap on desktop and mobile-width viewports
- Provider runtime editor stays enabled only when the catalog is API-backed and shows save feedback after a local configuration update

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
