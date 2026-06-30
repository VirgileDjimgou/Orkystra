# Orkystra — Smart Logistics Twin

**Orkystra** est un système d'exploitation logistique (Logistics OS) complet avec jumeau numérique d'entrepôt, gestion de transport, simulation événementielle, optimisation de tournées et assistant IA. Plateforme modulaire « simulation-first, reality-ready » construite en .NET, Vue 3, Python/FastAPI et MQTT.

## Démo interactive par rôle

| Rôle | Captures d'écran |
|------|-------------------|
| 🏢 **Président / Directeur** | <img src="docs/screenshots/01-president/01-vue-ensemble-control-tower.png" width="600" alt="Vue Control Tower"> |
| 📦 **Opérateur Entrepôt** | <img src="docs/screenshots/02-warehouse-operator/01-entrepots-et-jumeau-numerique.png" width="600" alt="Entrepôts et jumeau numérique"> |
| 🚛 **Dispatcher Transport** | <img src="docs/screenshots/03-transport-dispatcher/01-tableau-des-routes.png" width="600" alt="Tableau des routes"> |
| 🤖 **Analyste IA** | <img src="docs/screenshots/04-ai-analyst/01-assistant-IA-recommandations.png" width="600" alt="Assistant IA"> |
| ⚙️ **Administrateur** | <img src="docs/screenshots/05-admin/01-catalogue-providers-configuration.png" width="600" alt="Catalogue providers"> |
| 📊 **Superviseur** | <img src="docs/screenshots/06-supervisor/01-audit-observabilite.png" width="600" alt="Audit et observabilité"> |

### Scénarios de démonstration

Les captures ci-dessus illustrent les interactions typiques de chaque rôle avec la plateforme, utilisant des **données réelles provenant de l'API** (3 scénarios de simulation, 2 entrepôts, 3 routes, 3 providers). Chaque vue correspond à un onglet dédié dans l'interface Control Tower.

---

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
      Orkystra.Api/          # ASP.NET Core API (Minimal API)
      Orkystra.Application/   # Use cases, projections, provider adapters
      Orkystra.Contracts/     # DTOs et read-models
      Orkystra.Domain/        # Modèle métier pur (entités, valeur, événements)
  docs/
    adr/
    architecture/
    blueprints/
    screenshots/             # Captures par rôle (ci-dessus)
  frontend/
    web/                     # Vue 3 + Three.js + TypeScript
  infrastructure/
    docker-compose.yml       # PostgreSQL, Mosquitto MQTT, Qdrant
  python-services/
    ai-service/              # FastAPI + LangGraph (recommandations)
    optimization-service/    # FastAPI + OR-Tools (optimisation tournées)
  tests/
    backend/                 # 95+ tests xUnit
```

## Stack Technique

| Couche | Technologie |
|--------|-------------|
| **Backend** | .NET 9 (C#), Minimal API, Clean Architecture |
| **Frontend** | Vue 3, TypeScript, Vite, Three.js (jumeau 3D) |
| **IA** | Python 3.12+, FastAPI, LangGraph |
| **Optimisation** | Python 3.12+, FastAPI, OR-Tools |
| **Broker événementiel** | MQTT (Mosquitto) via MQTTnet |
| **Base de données** | SQLite (persistance opérationnelle), PostgreSQL (ref) |
| **Vector store** | Qdrant (IA - RAG) |
| **Infrastructure** | Docker Compose |
| **Auth** | API Key + Tenant headers |

## Fonctionnalités clés

- **Jumeau numérique 3D** d'entrepôt (Three.js) avec zones, racks, quais
- **Gestion de transport** avec cycle de vie complet (assignation → livraison)
- **Simulation déterministe** avec horloge virtuelle et semences reproductibles
- **Backbone MQTT** pour publications/consommations événementielles idempotentes
- **IA conversationnelle** avec recommandations « grounded » (preuves, confiance, actions)
- **Optimisation de tournées** (OR-Tools) avec plans alternatifs et explications
- **Connecteurs** (CSV, REST, GPS) avec registry, configuration runtime, gestion de secrets
- **Observabilité** : métriques, audit trail JSONL, santé des providers
- **Multitenant** avec résolution par en-tête HTTP

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│                     Orkystra Control Tower (Vue 3)        │
│            Tableau de bord · Jumeau 3D · Carte GPS        │
└──────────────────────────┬───────────────────────────────┘
                           │ HTTP (API Key + Tenant)
┌──────────────────────────▼───────────────────────────────┐
│                   Orkystra.Api (.NET 9)                   │
│    Contrôleur + Middleware + Projection + Workflows        │
└──────┬──────────────────────────────┬────────────────────┘
       │                              │
┌──────▼────────┐            ┌────────▼──────────┐
│ Orkystra.Domain│            │ Orkystra.Contracts │
│ (Aggregates,   │            │ (DTOs, ReadModels) │
│  Events, VOs)  │            └───────────────────┘
└──────┬────────┘                     
       │
┌──────▼──────────────┐
│ Orkystra.Application │
│ (Projections, Providers Registry, Event Envelopes)
└──────────────────────┘
       │                          ┌─────────────────────┐
       ├── MQTT (Mosquitto) ──────┤  Python AI Service   │
       │                          │  (FastAPI/LangGraph) │
       │                          └─────────────────────┘
       │                          ┌─────────────────────┐
       ├── HTTP ──────────────────┤  Python Optimization │
       │                          │  (FastAPI/OR-Tools) │
       │                          └─────────────────────┘
       │                          ┌─────────────────────┐
       └── Provider Registry ─────┤  CSV / REST / GPS   │
                                  │  Adapters           │
                                  └─────────────────────┘
```

## Démarrer en local

```powershell
# 1. Infrastructure (MQTT, PostgreSQL, Qdrant)
cd infrastructure
docker compose up -d

# 2. Backend API
cd backend
dotnet run --project src/Orkystra.Api

# 3. Frontend
cd frontend/web
npm install
npm run dev

# 4. Services Python (optionnel)
cd python-services
pip install -e .
uvicorn orkystra_ai_service.app:app --port 8001
uvicorn orkystra_optimization_service.app:app --port 8002
```

## Current Verification

```powershell
dotnet build backend/Orkystra.slnx
dotnet test backend/Orkystra.slnx --no-build
Push-Location frontend/web
npm run build
Pop-Location
python -m compileall python-services
```
