# Contributing to Orkystra

## Architecture Entrypoints

### Key Architectural Concepts

- **Simulation-first, reality-ready** — The same domain model powers both simulated and real data. Providers (interfaces) abstract the source of truth.
- **Provider pattern** — Every external system (warehouse, transport, GPS, AI) is accessed through a provider interface. Simulators and real adapters implement the same contract.
- **Event-driven backbone** — State changes flow through MQTT. Events are idempotent, versioned, and traceable via correlation/causation IDs.
- **Clean Architecture** — Domain (`Orkystra.Domain`) has zero infrastructure dependencies. Application (`Orkystra.Application`) orchestrates use cases. API (`Orkystra.Api`) wires HTTP, MQTT, and DI.

### Repository Layout

```
backend/
  src/
    Orkystra.Api/          ASP.NET Core minimal API — endpoints, middleware, DI, providers
    Orkystra.Application/  Use cases, projections, provider adapters, event envelopes
    Orkystra.Contracts/    DTOs and read models shared across layers
    Orkystra.Domain/       Pure domain — entities, aggregates, value objects, events
frontend/
  web/                     Vue 3 + TypeScript + Three.js — operator control tower UI
python-services/
  ai-service/              FastAPI + LangGraph — recommendation agents
  optimization-service/    FastAPI + OR-Tools — route optimization
infrastructure/
  docker-compose.yml       PostgreSQL, Mosquitto MQTT, Qdrant (infra only)
  docker-compose.stack.yml Full stack including API, frontend, AI, optimization
tests/
  backend/                 xUnit unit tests (138+ tests)
```

### Context Boundaries

| Context | Location | Purpose |
|---------|----------|---------|
| Warehouse | `backend/src/Orkystra.Domain/Warehouse`, `Orkystra.Api/Endpoints/Warehouse*.cs` | Inventory, docks, zones, pallets |
| Transport | `backend/src/Orkystra.Domain/Transport`, `Orkystra.Api/Endpoints/Transport*.cs` | Routes, trucks, shipments, deliveries |
| Simulation | `backend/src/Orkystra.Domain/Simulation` | Scenarios, virtual clock, synthetic events |
| AI | `backend/src/Orkystra.Api/AI/`, `python-services/ai-service/` | Recommendations, evidence, intent routing |
| Optimization | `backend/src/Orkystra.Api/Optimization/`, `python-services/optimization-service/` | Route planning, OR-Tools solving |
| Integration | `backend/src/Orkystra.Application/Providers/` | Connector adapters, provider registry |

## Development Workflow

This project uses **sprint-based development** managed by AI coding agents. Each sprint is a bounded, verifiable increment.

### Sprint Model

1. Read the constitution, roadmap, and project status to understand context.
2. Identify the next unfinished sprint from `IMPLEMENTATION_ROADMAP.md`.
3. Implement only that sprint's scope — no sideways feature expansion.
4. Run the relevant builds and tests for touched components.
5. Fix any failures before moving on.
6. Update documentation when behavior, architecture, contracts, or operations change.
7. Update `PROJECT_STATUS.md` and `IMPLEMENTATION_ROADMAP.md`.

### Rules

- No sprint counts if it only polishes a single UI file without adding meaningful product capability.
- Domain logic must never depend on infrastructure (HTTP, MQTT, database).
- Providers abstract the simulation/reality boundary — never branch on "is this real?" in domain code.
- Events must be idempotent, versioned, and traceable.
- Tests must cover domain invariants, event contracts, and provider interfaces.

## Code Standards

- Use the project's ubiquitous language — terms mean the same thing in code, docs, and UI.
- One responsibility per module — no giant files, no cyclic dependencies.
- Prefer intention-revealing names over abbreviations.
- Value objects are immutable and self-validating.
- Aggregates protect invariants and emit domain events — minimal mutation surface.

## Testing

```powershell
# Run all backend tests
dotnet test backend/Orkystra.slnx

# Run frontend type check and build
cd frontend/web
npm run build

# Python compile check
python -m compileall python-services
```

## Pull Request Flow

1. Work in a feature branch from `main`.
2. Implement the sprint scope.
3. Run all relevant builds and tests.
4. Update documentation if behavior or contracts changed.
5. Open a PR with the sprint goal and a summary of changes.

## Key Documents

| Document | Purpose |
|----------|---------|
| `constitution/SMART_LOGISTICS_TWIN_CONSTITUTION_v2.md` | Product and architecture source of truth |
| `IMPLEMENTATION_ROADMAP.md` | Executable sprint plan |
| `PROJECT_STATUS.md` | Current sprint, completed work, remaining risks |
| `docs/development.md` | Development commands and operational workflows |
| `docs/architecture/overview.md` | System architecture overview |
| `INSTALL.md` | Installation and deployment guide |
