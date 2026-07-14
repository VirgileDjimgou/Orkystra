# Orkystra

Orkystra is an open-source smart logistics platform built around a warehouse digital twin, transport operations, event-driven simulation, route optimization, observability, and pluggable integrations.

## What It Includes

- 3D warehouse digital twin
- Transport planning and exception handling
- Simulation-driven demo data and scenario replay
- MQTT event backbone
- Python services for AI assistance and optimization
- Support tooling for diagnostics, packaging, and local recovery
- A browser-based operator UI built with Vue 3 and TypeScript

## Current Architecture

- Backend: .NET 9, Clean Architecture, Minimal API
- Frontend: Vue 3, TypeScript, Vite, Three.js
- Python services: FastAPI, OR-Tools, LangGraph
- Messaging: MQTT
- Persistence: SQLite by default, PostgreSQL for self-hosted runs
- Repository layout: `backend/`, `frontend/`, `python-services/`, `infrastructure/`, `docs/`, `tests/`

## Quick Start

```powershell
cp .env.example .env
docker compose -f infrastructure/docker-compose.yml up -d
dotnet run --project backend/src/Orkystra.Api
cd frontend/web
npm install
npm run dev
```

For the packaged local evaluation path:

```powershell
powershell -ExecutionPolicy Bypass -File infrastructure/scripts/bring-up-selfhost.ps1
```

To reset the demo state:

```powershell
powershell -ExecutionPolicy Bypass -File infrastructure/scripts/reset-demo-state.ps1 -Rebootstrap
```

## Local Access

- Frontend: `http://127.0.0.1:8081`
- API: `http://127.0.0.1:8080`
- Health check: `http://127.0.0.1:8080/health/sanity`
- Protected API headers:
  - `X-Api-Key: expected-local-dev-key`
  - `X-Tenant-Id: north-hub-demo`

## Verification

```powershell
dotnet build backend/Orkystra.slnx --configuration Release /p:UseSharedCompilation=false /nodeReuse:false
dotnet test backend/Orkystra.slnx --configuration Release /p:UseSharedCompilation=false /nodeReuse:false
cd frontend/web
npm run build
python -m compileall python-services
python -m pytest python-services/tests
```

For a consolidated release-grade pass, use:

```powershell
powershell -ExecutionPolicy Bypass -File infrastructure/scripts/verify-oss-readiness.ps1
```

## Source of Truth

- [constitution/SMART_LOGISTICS_TWIN_CONSTITUTION_v2.md](constitution/SMART_LOGISTICS_TWIN_CONSTITUTION_v2.md)
- [IMPLEMENTATION_ROADMAP.md](IMPLEMENTATION_ROADMAP.md)
- [PROJECT_STATUS.md](PROJECT_STATUS.md)
- [INSTALL.md](INSTALL.md)
- [CONTRIBUTING.md](CONTRIBUTING.md)
- [CHANGELOG.md](CHANGELOG.md)
- [docs/development.md](docs/development.md)
- [docs/architecture/overview.md](docs/architecture/overview.md)
- [docs/operations/post-release-candidate-checkpoint.md](docs/operations/post-release-candidate-checkpoint.md)

## Repository Layout

```text
Orkystra/
  backend/          .NET API, application, domain, and contracts
  frontend/web/     Vue operator UI
  python-services/  AI and optimization services
  infrastructure/   Docker Compose, scripts, and local stack helpers
  docs/             Architecture, operations, methodology, and screenshots
  prompts/          Reusable operational prompts
  tests/            Cross-cutting test assets
```

## Key Capabilities

- Warehouse and transport control tower views
- Route optimization and exception follow-up
- Scenario simulation and replay
- Provider catalog and runtime configuration
- Observability, audit trail, and support bundles
- Self-host deployment workflow

## Status

The current sprint state and remaining work are tracked in [PROJECT_STATUS.md](PROJECT_STATUS.md) and [IMPLEMENTATION_ROADMAP.md](IMPLEMENTATION_ROADMAP.md).
