# Install & Deploy

This guide covers local development setup and self-hosted deployment for Orkystra.

For the current candidate summary, see `CHANGELOG.md` and `docs/operations/releases/v0.1.0-rc.1.md`.
For the post-release-candidate maturity checkpoint, see `docs/operations/post-release-candidate-checkpoint.md`.

## Prerequisites

| Component | Requirement |
|-----------|-------------|
| .NET SDK | 9.0+ (`dotnet --version`) |
| Node.js | 20+ (`node --version`) |
| npm | 10+ (`npm --version`) |
| Python | 3.12+ (`python --version`) |
| Docker | 24+ with Docker Compose plugin |
| Git | Any recent version |

## Quick Start (Local Development)

### 1. Clone and prepare

```powershell
git clone <repo-url> orkystra
cd orkystra

# Copy environment template
cp .env.example .env
```

### 2. Start infrastructure

MQTT, PostgreSQL, and Qdrant run in Docker:

```powershell
docker compose -f infrastructure/docker-compose.yml up -d
```

### 3. Set your API key

```powershell
$env:Security__ApiKey='my-local-dev-key'
```

### 4. Start the backend

```powershell
dotnet restore backend/Orkystra.slnx
dotnet build backend/Orkystra.slnx
dotnet run --project backend/src/Orkystra.Api
```

The API starts on `http://localhost:5043`.

### 5. Start the frontend

```powershell
cd frontend/web
npm install
npm run dev
```

The dev server starts on `http://localhost:5173`.

### 6. Verify the stack is healthy

```powershell
curl http://localhost:5043/health/sanity
```

### 7. Bootstrap demo data

```powershell
curl -X POST http://localhost:5043/api/bootstrap/demo `
  -H "X-Api-Key: my-local-dev-key" `
  -H "Content-Type: application/json" `
  -d '{"scenarioName":"Demo","seed":42,"advanceMinutes":15,"includeDisruption":true}'
```

### 8. Python AI and optimization services (optional)

```powershell
cd python-services
pip install -e ".[dev]"
uvicorn orkystra_ai_service.app:app --host 127.0.0.1 --port 8001
```

```powershell
uvicorn orkystra_optimization_service.app:app --host 127.0.0.1 --port 8002
```

Without these services the backend falls back to local deterministic modes.

## Self-Host Deployment

### Option A: Docker Compose (full stack)

Fastest first-run path:

```powershell
powershell -ExecutionPolicy Bypass -File infrastructure/scripts/bring-up-selfhost.ps1
```

This helper script:

- starts the packaged Docker Compose stack
- waits for `http://127.0.0.1:8080/health/sanity`
- bootstraps deterministic demo data
- prints the frontend URL, API URL, and default local evaluation headers
- fails early with a clear message if Docker Desktop / Docker Engine is not running

Manual equivalent:

```powershell
docker compose -f infrastructure/docker-compose.stack.yml up -d --build
```

This starts all six services: PostgreSQL, MQTT, Qdrant, API, AI service, optimization service, and the web frontend.

The frontend is served on `http://localhost:8081`.
The API is served on `http://localhost:8080`.

Set the API key via environment variable before starting:

```powershell
$env:Security__ApiKey='your-production-key'
docker compose -f infrastructure/docker-compose.stack.yml up -d --build
```

You can skip demo bootstrapping with:

```powershell
powershell -ExecutionPolicy Bypass -File infrastructure/scripts/bring-up-selfhost.ps1 -SkipBootstrap
```

### Resetting local demo state

When you want to throw away the current local SQLite-backed demo state and rebuild a known-good evaluation setup:

```powershell
powershell -ExecutionPolicy Bypass -File infrastructure/scripts/reset-demo-state.ps1 -Rebootstrap
```

Default reset scope:

- remove `backend/src/Orkystra.Api/output/persistence/orkystra-operations.db`
- remove `backend/src/Orkystra.Api/output/audit`
- then optionally re-run the packaged bring-up helper

Useful variants:

```powershell
# Preview the reset without changing anything
powershell -ExecutionPolicy Bypass -File infrastructure/scripts/reset-demo-state.ps1 -Rebootstrap -WhatIf
```

```powershell
# Also stop the compose stack and remove Docker volumes
powershell -ExecutionPolicy Bypass -File infrastructure/scripts/reset-demo-state.ps1 -StopContainers -RemoveDockerVolumes
```

```powershell
# Also wipe local runtime and secret overrides
powershell -ExecutionPolicy Bypass -File infrastructure/scripts/reset-demo-state.ps1 -RemoveLocalRuntimeConfig -RemoveLocalSecrets
```

### Option B: Manual deployment

1. Build the backend for your platform:

```powershell
dotnet publish backend/Orkystra.slnx --configuration Release -o publish/api
```

2. Build the frontend:

```powershell
cd frontend/web
npm install
npm run build
```

The output is in `frontend/web/dist/`. Serve it with any static file server.

3. Configure environment variables (see Configuration Reference below).

4. Run the backend binary:

```powershell
./publish/api/Orkystra.Api.exe
```

## Configuration Reference

All settings are in `appsettings.json` and can be overridden with environment variables.

### Required

| Variable | Description |
|----------|-------------|
| `Security__ApiKey` | API key for all protected endpoints (must be non-empty) |

### Persistence

| Variable | Default | Description |
|----------|---------|-------------|
| `OperationalPersistence__Provider` | `sqlite` | `sqlite` (no deps) or `postgres` |
| `OperationalPersistence__DatabasePath` | `output/persistence/orkystra-operations.db` | SQLite file path |
| `OperationalPersistence__ConnectionString` | `Host=localhost;...` | Postgres connection string |

For runtime verification of the active persistence provider, use the protected diagnostics endpoint after the API is running:

```powershell
curl http://localhost:5043/observability/persistence/provider `
  -H "X-Api-Key: my-local-dev-key"
```

Expected posture:

- `provider: "sqlite"` and `posture: "local-default"` for the simplest local path
- `provider: "postgres"` and `posture: "self-host-recommended"` for the more serious self-host path

### Event Backbone (MQTT)

| Variable | Default | Description |
|----------|---------|-------------|
| `EventBackbone__BrokerUrl` | `mqtt://localhost:1883` | MQTT broker URL |
| `EventBackbone__Enabled` | `true` | Enable or disable MQTT |
| `EventBackbone__SimulationTopicFilter` | `orkystra/events/simulation/#` | Simulation event subscription |

### AI Service

| Variable | Default | Description |
|----------|---------|-------------|
| `AiService__Provider` | `http` | `http`, `local` (deterministic), or `disabled` |
| `AiService__BaseUrl` | `http://127.0.0.1:8001` | Python AI service URL (when Provider=http) |
| `AiService__TimeoutSeconds` | `8` | HTTP timeout |

### Optimization Service

| Variable | Default | Description |
|----------|---------|-------------|
| `OptimizationService__BaseUrl` | `http://127.0.0.1:8002` | Python optimization URL |
| `OptimizationService__TimeoutSeconds` | `8` | HTTP timeout |

### Provider Secrets

Use the naming convention `ORKYSTRA_PROVIDER_{PROVIDER_ID}_{FIELD}`. Provider IDs with hyphens become uppercase with underscores.

```powershell
$env:ORKYSTRA_PROVIDER_REST_TRANSPORT_ADAPTER_APIKEY='your-api-key'
```

## Verifying the Installation

```powershell
# Backend build and test
dotnet build backend/Orkystra.slnx
dotnet test backend/Orkystra.slnx --no-build

# Frontend build
cd frontend/web
npm run build

# Python services compile check
cd python-services
python -m pytest
python -m compileall .
```

## Troubleshooting

**API won't start** — Ensure `Security__ApiKey` is set and non-empty. The API logs a clear message on startup if it is missing.

**Frontend shows fallback data** — The API might be unreachable. Check `VITE_API_BASE_URL` (defaults to `http://localhost:5043`) and that the API is running.

**MQTT connection errors** — Ensure Docker is running and `docker compose -f infrastructure/docker-compose.yml up -d` has completed. The API falls back gracefully if MQTT is unavailable.

**Python services not found** — Run `pip install -e ".[dev]"` from `python-services/` first. That editable install now exposes both `orkystra_ai_service` and `orkystra_optimization_service` import paths used by the local `uvicorn` commands and CI checks.
