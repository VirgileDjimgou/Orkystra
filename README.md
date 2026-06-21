# Orkystra - Smart Logistics Twin

Orkystra is the working repository for Smart Logistics Twin: a simulation-first, reality-ready logistics operating system with warehouse digital twin, transport dispatch, event-driven projections, optimization, and AI-assisted decision support.

## Core Documents

- Architecture overview: `docs/architecture/overview.md`
- Connector architecture: `docs/architecture/connector-layer.md`
- Production hardening notes: `docs/architecture/production-hardening.md`
- Development commands: `docs/development.md`

## Repository Layout

```text
Orkystra/
  backend/
    src/
  docs/
    adr/
    architecture/
  frontend/
    web/
  infrastructure/
  python-services/
    ai-service/
    optimization-service/
  tests/
```

The repository is intentionally modular. Backend, frontend, AI services, optimization, infrastructure, and tests evolve independently behind explicit boundaries.

## Current Verification

```powershell
dotnet build backend/Orkystra.slnx
dotnet test backend/Orkystra.slnx --no-build
Push-Location frontend/web
npm run build
Pop-Location
python -m compileall python-services
```
