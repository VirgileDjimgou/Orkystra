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
- `GET /api/providers/catalog`

The development API key is intentionally not committed. Provide it through environment variables or an ignored local configuration file.
The API now allows local Vite origins on `127.0.0.1` and `localhost` for development workflows, so the operator UI can call the protected backend directly during local browser sessions.

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

## Python Services

```powershell
python -m compileall python-services
```

The Python service dependencies are declared in `python-services/pyproject.toml`. Install them in a virtual environment before running FastAPI apps.

## Infrastructure

```powershell
docker compose -f infrastructure/docker-compose.yml up -d
docker compose -f infrastructure/docker-compose.stack.yml up -d --build
```

Local infrastructure includes PostgreSQL, Mosquitto MQTT, and Qdrant.
The stack compose file also brings up the API, frontend, AI service, and optimization service.
