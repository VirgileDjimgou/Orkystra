# Orkystra — Smart Logistics Twin

**Orkystra** est un système d'exploitation logistique (Logistics OS) complet avec jumeau numérique d'entrepôt, gestion de transport, simulation événementielle, optimisation de tournées et assistant IA. Plateforme modulaire « simulation-first, reality-ready » construite en .NET, Vue 3, Python/FastAPI et MQTT.

## Démo interactive par rôle — Workflows pas-à-pas

Chaque rôle suit un workflow de 4 étapes (consulter → analyser → décider → suivre). Captures réalisées depuis l'API réelle (3 scénarios, 2 entrepôts, 3 routes, 3 providers).

### 🏢 Président / Directeur — Vue stratégique globale

| Étape | Capture |
|-------|---------|
| ① Vue d'ensemble : KPI, alertes, santé des providers | <img src="docs/screenshots/01-president/01-vue-ensemble.png" width="500"> |
| ② Analyse des alertes critiques et flux tendus | <img src="docs/screenshots/01-president/02-details-alertes.png" width="500"> |
| ③ Décision : contacter les prestataires dégradés | <img src="docs/screenshots/01-president/03-sante-providers.png" width="500"> |
| ④ Suivi : plan d'action validé pour la journée | <img src="docs/screenshots/01-president/04-decisions.png" width="500"> |

### 📦 Opérateur Entrepôt — Gestion des stocks et jumeau 3D

| Étape | Capture |
|-------|---------|
| ① Vue des entrepôts : occupation, zones, quais | <img src="docs/screenshots/02-warehouse-operator/01-entrepots.png" width="500"> |
| ② Jumeau numérique 3D interactif | <img src="docs/screenshots/02-warehouse-operator/02-jumeau-numerique.png" width="500"> |
| ③ Analyse des capacités et risques de congestion | <img src="docs/screenshots/02-warehouse-operator/03-analyse-capacite.png" width="500"> |
| ④ Décision : réaffectation des zones de stockage | <img src="docs/screenshots/02-warehouse-operator/04-reaffectation.png" width="500"> |

### 🚛 Dispatcher Transport — Suivi et optimisation des routes

| Étape | Capture |
|-------|---------|
| ① Tableau des routes : statut, arrêts, livraisons | <img src="docs/screenshots/03-transport-dispatcher/01-tableau-routes.png" width="500"> |
| ② Analyse de la route RT-412 en retard | <img src="docs/screenshots/03-transport-dispatcher/02-route-retard.png" width="500"> |
| ③ Re-routage : optimisation OR-Tools disponible | <img src="docs/screenshots/03-transport-dispatcher/03-optimisation.png" width="500"> |
| ④ Synchro transport : plan de tournée mis à jour | <img src="docs/screenshots/03-transport-dispatcher/04-synchronisation.png" width="500"> |

### 🤖 Analyste IA — Recommandations opérationnelles

| Étape | Capture |
|-------|---------|
| ① Assistant IA : recommandations opérationnelles | <img src="docs/screenshots/04-ai-analyst/01-assistant-IA.png" width="500"> |
| ② Preuves, hypothèses, niveau de confiance HIGH | <img src="docs/screenshots/04-ai-analyst/02-preuves-confiance.png" width="500"> |
| ③ Trace opérationnelle et historique IA | <img src="docs/screenshots/04-ai-analyst/03-trace-operationnelle.png" width="500"> |
| ④ Workflow IA : analyse, décision, action | <img src="docs/screenshots/04-ai-analyst/04-workflow-ia.png" width="500"> |

### ⚙️ Administrateur — Configuration et connecteurs

| Étape | Capture |
|-------|---------|
| ① Catalogue des providers connecteurs | <img src="docs/screenshots/05-admin/01-catalogue-providers.png" width="500"> |
| ② Configuration des connecteurs et secrets API | <img src="docs/screenshots/05-admin/02-configuration.png" width="500"> |
| ③ État des connexions et santé des services | <img src="docs/screenshots/05-admin/03-etat-connexions.png" width="500"> |
| ④ Configuration runtime et déploiement | <img src="docs/screenshots/05-admin/04-runtime-config.png" width="500"> |

### 📊 Superviseur — Observabilité et audit

| Étape | Capture |
|-------|---------|
| ① Piste d'audit et observabilité | <img src="docs/screenshots/06-supervisor/01-audit.png" width="500"> |
| ② Métriques système et backbone événementiel | <img src="docs/screenshots/06-supervisor/02-metriques.png" width="500"> |
| ③ Santé du système : API, MQTT, SQLite | <img src="docs/screenshots/06-supervisor/03-sante-systeme.png" width="500"> |
| ④ Tableau de bord superviseur : vue consolidée | <img src="docs/screenshots/06-supervisor/04-tableau-bord.png" width="500"> |

### Lancement de la démo interactive

```powershell
# 1. Infrastructure (MQTT, PostgreSQL, Qdrant)
cd infrastructure
docker compose up -d

# 2. Backend API
cd backend
dotnet run --project src/Orkystra.Api

# 3. Page démo standalone (port 4180)
cd docs/screenshots
npx http-server . -p 4180 -c-1

# 4. Ouvrir http://127.0.0.1:4180/demo.html
```

Les données sont chargées en direct depuis l'API .NET sur le port 5043 avec les headers `X-Api-Keys` et `X-Tenant-Id`.

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
