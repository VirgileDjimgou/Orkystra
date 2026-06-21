# Development Commands

Run commands from the repository root unless noted.

## Backend

```powershell
dotnet restore backend/Orkystra.slnx
dotnet build backend/Orkystra.slnx --configuration Release /p:UseSharedCompilation=false /nodeReuse:false
dotnet test backend/Orkystra.slnx --configuration Release /p:UseSharedCompilation=false /nodeReuse:false
dotnet run --project backend/src/Orkystra.Api
```

Protected operational API routes expect:

- `X-Api-Key`
- `X-Tenant-Id` when tenant-header enforcement is enabled

## Frontend

```powershell
cd frontend/web
npm install
npm run build
npm run dev
```

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
