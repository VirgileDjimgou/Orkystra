# Orkystra FleetOps

Orkystra FleetOps is a modular fleet operations MVP for small and mid-sized transport businesses. The platform covers the operational chain from identity and tenant isolation to vehicle tracking, dispatch execution, offline-capable driver workflows, proof of delivery, deterministic alerting, maintenance coordination, external integrations, and pilot-grade production readiness.

The repository currently delivers a complete Sprint 00 through Sprint 09 baseline:

- reproducible local environment;
- modular ASP.NET Core backend;
- Vue 3 web console for Admin and Operator roles;
- Android driver app foundation;
- SQL Server persistence with Entity Framework Core migrations;
- tenant-aware authentication and authorization;
- tenant-scoped vehicle, driver, and GPS device registry;
- historized GPS device-to-vehicle assignments;
- idempotent CSV imports for fleet master data;
- persisted live telemetry with replay protection and current-position snapshots;
- paged tracking history and tenant-scoped tracking metrics;
- SignalR streaming for live fleet positions;
- deterministic multi-vehicle GPS simulator for demos and validation.
- dispatch missions with explicit state transitions, assignment checks, audited timelines, and mission-to-map linkage.
- Android driver login, offline mission cache, Room persistence, idempotent action outbox, and WorkManager-based background sync.
- pre-departure inspections, critical defect blocking, private media uploads with resumable sessions, and operator-visible delivery proof.
- deterministic alert scans for compliance, maintenance, and inactive vehicles with worker-backed retries.
- web alert center for scan triggering, assignment, acknowledgment, compliance setup, and odometer-driven maintenance configuration.
- scoped API keys for partners and devices, SQL outbox-backed webhooks, sandbox receipts, OpenAPI exposure, and web administration for integrations;
- administrator MFA with authenticator challenge, tenant export and controlled purge tooling, OpenTelemetry OTLP wiring, JSON logs, readiness checks, pilot container packaging, and SQL backup/restore scripts.

## Product Goal

The product goal is to provide a commercially credible fleet management platform that remains understandable, maintainable, and extensible for a small product team.

The MVP is intentionally staged in vertical sprints:

1. foundation and reproducibility;
2. identity, organizations, and roles;
3. fleet registry and devices;
4. live tracking and simulation;
5. dispatch and mission execution;
6. driver mobile workflows;
7. inspections and proof of delivery;
8. integrations and auditability;
9. production hardening and pilot readiness.

## Core Architecture

FleetOps follows a modular monolith architecture. The goal is to keep deployment and operations simple while preserving clear module boundaries and tenant-safe business flows.

```mermaid
flowchart LR
    Web["Vue Web Console<br/>Admin / Operator"] --> Api["ASP.NET Core API"]
    Android["Android Driver App<br/>Compose + Room + WorkManager"] --> Api
    Simulator["GPS Simulator"] --> Api
    Api --> SignalR["SignalR Hub"]
    Api --> Db["SQL Server"]
    Api --> ObjectStore["Object Storage"]
    Api --> Mqtt["MQTT (device edge only)"]
    Worker["Background Worker"] --> Db
    Worker --> Api
    Api --> OTel["OpenTelemetry / OTLP"]
    Worker --> OTel
```

### Architectural principles

- Modular monolith instead of microservices.
- One Web application for Admin and Operator personas.
- One native Android application dedicated to Driver workflows.
- SQL Server as the main transactional store.
- SignalR only for real-time positions, states, and alerts.
- MQTT reserved for direct device or simulator communication.
- Tenant-aware entities carry `OrganizationId`.
- Tenant resolution comes from authenticated claims, never from free-form client input.
- Security-sensitive actions are enforced server-side.

## Solution Structure

```text
apps/
  backend/
    FleetOps.Api/              HTTP API, auth, SignalR, minimal endpoints
    FleetOps.Core/             domain model and business invariants
    FleetOps.Infrastructure/   EF Core, Identity, persistence, migrations, seeding
    FleetOps.Worker/           background services
  web/                         Vue 3 Admin/Operator console
  android-driver/              Android driver application
simulators/
  GpsSimulator/                deterministic telemetry simulator
docs/                          product, architecture, engineering, and commercial docs
scripts/                       local environment and quality-gate automation
tests/
  backend/FleetOps.UnitTests/  domain and integration coverage
```

## Module View

```mermaid
flowchart TD
    subgraph Backend
        Core["FleetOps.Core<br/>Domain entities and invariants"]
        Infra["FleetOps.Infrastructure<br/>EF Core, Identity, seeding, migrations"]
        Api["FleetOps.Api<br/>Auth, admin APIs, tracking APIs, SignalR hub"]
        Worker["FleetOps.Worker<br/>Background processing"]
    end

    Core --> Infra
    Core --> Api
    Infra --> Api
    Core --> Worker
    Infra --> Worker
```

### Current functional slices

- Identity and tenancy
  - organizations;
  - seeded users and roles;
  - JWT-based authentication;
  - claim-based tenant resolution;
  - audit logs for login and administrative actions.
- Fleet registry
  - tenant-aware `Vehicle`, `Driver`, `GpsDevice`, and `DeviceAssignment` entities;
  - unique vehicle registrations, driver license numbers, and GPS serial numbers per organization;
  - active/inactive status lifecycle;
  - historized GPS device assignments with a single active assignment per device;
  - CSV imports for vehicles, drivers, and devices;
  - server-side authorization and audit trails for registry operations.
- Live tracking and simulation
  - idempotent versioned telemetry ingestion;
  - persisted telemetry history plus current vehicle positions;
  - duplicate and out-of-order event handling;
  - paged telemetry history for operators;
  - tenant-scoped SignalR push updates and live tracking metrics;
  - deterministic multi-vehicle simulator with replay and reset support.
- Dispatch and mission execution
  - tenant-aware `Mission`, `MissionStop`, and `MissionTimelineEvent` entities;
  - explicit mission state machine from `Draft` to `Completed`;
  - operator mission planning with ordered stops;
  - driver and vehicle assignment with overlap conflict detection;
  - delay simulation and timeline auditing for demos and operations review;
  - map linkage between a mission and its assigned live vehicle.
- Driver mobile workflow
  - secure driver login bound to a server-side `DriverId`;
  - driver-only mission list and mission detail APIs;
  - local Room cache for missions, stops, timeline, session, and pending commands;
  - idempotent mobile outbox commands for `Start`, `Arrive`, and `Complete`;
  - background sync with WorkManager and conflict/offline states surfaced in the UI.
- Field operations proof
  - checklist templates and checklist items seeded per tenant;
  - pre-departure inspections with pass/fail outcomes and defect severity;
  - delivery proof records linked to mission stops;
  - resumable media upload sessions and signed private media access;
  - operator mission detail enriched with inspection and POD evidence.
- Alerting and light maintenance
  - tenant-scoped compliance documents for vehicles and drivers;
  - maintenance plans driven by date and odometer thresholds;
  - deterministic deduplicated `OperationalAlert` and `AlertNotification` records;
  - in-app and development-email notification traces;
  - role-aware assignment and acknowledgment APIs;
  - alert worker re-scan behavior resilient to process restart.
- Integrations and auditability
  - scoped `ApiClientCredential` records for partner and device access;
  - immutable audit persistence guarded in the EF Core unit of work;
  - SQL outbox messages and webhook delivery attempts with retry and dead-letter states;
  - HMAC-signed sandbox and external webhook delivery contracts;
  - tenant-safe integration administration APIs plus CSV export endpoints.
- Production hardening and pilot readiness
  - administrator MFA setup, verification, disablement, and login challenge flow;
  - tenant-scoped lifecycle summary, JSON export package, and controlled purge categories;
  - JSON console logging plus OpenTelemetry OTLP hooks for API and worker services;
  - `/health` and `/health/ready` endpoints for runtime supervision;
  - container images for API, worker, and web console with reverse-proxy web routing;
  - SQL backup and restore scripts aligned with the pilot compose stack.

## Fleet Registry Flow

Sprint 02 adds the operational master data needed before live tracking and dispatch workflows can become meaningful.

```mermaid
flowchart LR
    Admin["Admin"] --> WebFleet["Fleet Registry Screens"]
    Operator["Operator"] --> WebFleet
    WebFleet --> ApiFleet["/api/v1/fleet/*"]
    ApiFleet --> Tenant["Tenant Claims<br/>OrganizationId"]
    Tenant --> Vehicles["Vehicles"]
    Tenant --> Drivers["Drivers"]
    Tenant --> Devices["GPS Devices"]
    Devices --> Assignments["Device Assignments<br/>historized"]
    Assignments --> Vehicles
    ApiFleet --> Audit["Audit Log"]
```

### Registry capabilities

- `Admin` users can create, update, activate, deactivate, and import vehicles, drivers, and GPS devices.
- `Operator` users can read registry data and manage GPS device assignments without being able to create or deactivate master data.
- CSV imports are idempotent: existing records are updated by natural key and new records are created.
- Device assignments are append-only history records; closing an assignment preserves the previous relationship and enables reassignment.
- All registry queries are scoped by the authenticated tenant and never accept an organization identifier from the client.

## Tracking Flow

Sprint 03 turns the seeded fleet registry into a demonstrable live operations view.

```mermaid
sequenceDiagram
    participant Simulator
    participant API as "Tracking API"
    participant DB as "SQL/EF Core"
    participant Hub as "SignalR Hub"
    participant Web as "Vue Tracking Console"

    Simulator->>API: POST telemetry event (internal v1)
    API->>API: validate tenant, vehicle, device assignment, idempotency
    API->>DB: persist telemetry history
    API->>DB: update current vehicle position if event is newer
    API-->>Hub: publish current position change
    Web->>API: GET current positions / history / metrics
    Hub-->>Web: push live position update
```

### Tracking capabilities

- Internal telemetry ingestion is versioned and idempotent.
- Duplicate events are ignored without creating a second history point.
- Out-of-order events are stored in history without replacing the current vehicle position.
- Operators can inspect current positions, paged history, and tracking metrics inside the same authenticated web console.
- The development simulator can reset a scenario, stream three vehicles at once, and optionally replay duplicates or older events for validation.

## Dispatch Flow

Sprint 04 connects planning and execution on top of the fleet and tracking baseline.

```mermaid
sequenceDiagram
    participant Operator
    participant Web as "Vue Dispatch Board"
    participant API as "Dispatch API"
    participant DB as "SQL/EF Core"
    participant Map as "Fleet Map"

    Operator->>Web: create draft mission + ordered stops
    Web->>API: POST /api/v1/dispatch/missions
    API->>DB: persist mission, stops, timeline
    Operator->>Web: assign driver and vehicle
    Web->>API: PUT /assignment
    API->>API: validate tenant, status, and schedule conflicts
    API->>DB: persist assignment + timeline event
    Operator->>Web: change status / simulate delay
    Web->>API: POST /status or /delay-simulation
    API->>DB: persist transition + timeline event
    Web->>Map: open the assigned vehicle on the live map
```

### Dispatch capabilities

- Missions follow an explicit validated state machine instead of ad-hoc flags.
- Illegal transitions are rejected server-side.
- Assignments stay tenant-scoped and reject inactive or cross-tenant resources.
- Overlapping assignments for the same driver or vehicle are refused.
- Every assignment, status change, and delay appends to an auditable timeline.
- Operators can jump from a mission board entry to the live vehicle map context.

## Driver Mobile Flow

Sprint 05 turns the Android driver app into a real offline-first execution client on top of the dispatch model introduced in Sprint 04.

```mermaid
sequenceDiagram
    participant Driver
    participant App as "Android Driver App"
    participant Room as "Room Cache"
    participant API as "Driver API"
    participant Outbox as "Pending Command Queue"

    Driver->>App: sign in
    App->>API: POST /api/auth/login
    API-->>App: JWT + tenant + driver binding
    App->>API: GET /api/v1/driver/missions
    App->>Room: cache missions, stops, timeline
    Driver->>App: start / arrive / complete mission
    App->>Room: apply optimistic local state
    App->>Outbox: enqueue commandId + rowVersion
    App->>API: sync queued command when online
    API-->>App: deduplicated mission state
    App->>Room: replace cached mission with server truth
```

### Mobile capabilities

- Drivers only see missions assigned to their authenticated driver profile.
- Mission data remains available offline after the first successful refresh.
- Route actions are queued locally and replayed with stable `commandId` values.
- Duplicate command submissions are ignored server-side without applying the transition twice.
- Background sync retries automatically when connectivity returns.
- The mobile UI surfaces `Synced`, `Pending sync`, `Offline`, and `Needs reload` states.

## Inspection and POD Flow

Sprint 06 extends the mobile execution flow with pre-departure checks, signed private media access, and stop-level proof of delivery.

```mermaid
sequenceDiagram
    participant Driver
    participant App as "Android Driver App"
    participant API as "Driver Operations API"
    participant Store as "Private Media Store"
    participant Dispatch as "Vue Dispatch Board"

    Driver->>App: complete pre-departure inspection
    App->>API: create resumable upload session
    App->>API: append photo chunks with offset resume
    API->>Store: persist private media
    App->>API: submit inspection with uploaded asset ids
    API->>API: block Start if inspection missing or critical defect remains
    Driver->>App: submit delivery proof for a mission stop
    App->>API: submit recipient, signature, notes, and photo asset ids
    Dispatch->>API: read mission detail
    API-->>Dispatch: latest inspection + delivery proofs + signed read URLs
```

### Sprint 06 capabilities

- Checklist-based pre-departure inspections bound to a mission.
- Critical defects prevent the mission from starting until a compliant inspection exists.
- Delivery proof is captured per mission stop with recipient name, signature name, notes, and photo references.
- Private media downloads are never exposed as public static files; the API returns short-lived signed URLs.
- The Android repository persists pending workflow operations and resumes media upload from the last acknowledged byte offset.

## Alerting and Maintenance Flow

Sprint 07 turns compliance and maintenance signals into deterministic operational alerts that can be reproduced on demand or by the worker after a restart.

```mermaid
sequenceDiagram
    participant Admin as "Admin / Operator"
    participant Web as "Vue Alert Center"
    participant API as "Alerts API"
    participant Worker as "FleetOps Worker"
    participant DB as "SQL/EF Core"

    Admin->>Web: create document / maintenance plan / odometer update
    Web->>API: POST compliance or maintenance endpoint
    API->>DB: persist tenant-scoped setup data
    Admin->>Web: run scan
    Web->>API: POST /api/v1/alerts/scan
    Worker->>DB: periodic re-scan after restart
    API->>DB: evaluate deterministic rules and dedupe by business key
    API->>DB: create/update/resolve alerts + notifications
    Web->>API: GET dashboard / alerts / notifications / assignees
    Admin->>Web: assign owner or acknowledge alert
    Web->>API: POST /assign or /acknowledge
```

### Sprint 07 capabilities

- Vehicle and driver compliance expirations are stored explicitly and scanned in UTC-safe rules.
- Maintenance plans can trigger by elapsed days, odometer intervals, or both.
- Inactive vehicle alerts are derived from stale or missing latest telemetry snapshots.
- Each alert is deduplicated by a deterministic business key so repeated scans do not multiply incidents.
- Development-email delivery failures are tolerated without losing the in-app alert trace.
- The web console exposes only role-allowed actions: Admin and Operator can triage alerts, while Admin keeps setup-only flows.

## Integration Flow

Sprint 08 opens the platform to controlled external connectivity without giving up tenant safety, delivery traceability, or replay protection.

```mermaid
sequenceDiagram
    participant Admin as "Admin"
    participant Web as "Integrations Console"
    participant API as "FleetOps API"
    participant DB as "SQL/EF Core"
    participant Worker as "Webhook Worker"
    participant Partner as "Partner System"

    Admin->>Web: issue API key / create webhook
    Web->>API: POST admin integration endpoints
    API->>DB: persist credential, endpoint, immutable audit trail
    Partner->>API: call partner/device endpoints with X-Api-Key
    API->>API: validate scope, type, tenant, and rate limit
    API->>DB: enqueue outbox event after business action
    Worker->>DB: fetch pending outbox messages
    Worker->>Partner: POST HMAC-signed webhook
    Worker->>DB: mark delivered, retry, or dead-letter
    Web->>API: inspect contracts, outbox, attempts, and sandbox receipts
```

### Sprint 08 capabilities

- Partner and device API keys are isolated by tenant, type, and scope.
- OpenAPI exposes the documented integration routes.
- Webhook signatures use HMAC SHA-256 and forged sandbox requests are rejected.
- Delivery retries are persisted and eventually dead-lettered instead of disappearing in logs.
- Fleet CSV exports complement replayable imports for operational data exchange.
- The web console exposes contracts, credentials, webhooks, outbox state, and CSV operations only to administrators.

## Production Hardening Flow

Sprint 09 turns the MVP into a pilot-ready package with explicit security, observability, packaging, and data-governance controls.

```mermaid
flowchart LR
    Admin["Admin"] --> Security["Security & Data Console"]
    Security --> MFA["Authenticator MFA"]
    Security --> Export["Tenant Export"]
    Security --> Purge["Controlled Purge"]
    Web["Nginx Web Container"] --> Api["FleetOps API"]
    Api --> Ready["/health/ready"]
    Api --> Otlp["OTLP Collector"]
    Worker["FleetOps Worker"] --> Otlp
    Scripts["Backup / Restore Scripts"] --> Sql["SQL Server"]
```

### Sprint 09 capabilities

- administrators can rotate an authenticator secret, verify MFA, receive recovery codes, and must provide a code once MFA is enabled;
- tenant data can be summarized, exported as JSON, and purged selectively for tracking history, integration history, or upload sessions;
- API and worker emit JSON logs and OpenTelemetry telemetry when `OTEL_EXPORTER_OTLP_ENDPOINT` is configured;
- readiness is separated from liveness through `/health/ready`;
- pilot deployments can run from Docker images using `docker-compose.pilot.yml`;
- SQL backup and restore scripts support recovery drills before onboarding pilot users.

## Authentication and Tenant Isolation

Sprint 01 introduces a real authentication and authorization baseline.

```mermaid
sequenceDiagram
    participant User
    participant Web
    participant API
    participant DB

    User->>Web: submit email + password
    Web->>API: POST /api/auth/login
    API->>DB: validate user, organization, roles
    API->>DB: write audit log
    API-->>Web: JWT with role + organization claims
    Web->>API: authenticated requests with bearer token
    API->>API: resolve tenant from claims
    API->>DB: filter data by OrganizationId
    API-->>Web: tenant-scoped response
```

### Implemented role model

- `Admin`
  - can sign in;
  - can administer users in the current organization;
  - can access tenant-scoped operational data.
- `Operator`
  - can sign in;
  - can access the operational shell and telemetry;
  - cannot administer users.
- `Driver`
  - seeded for backend completeness and future mobile integration;
  - reserved for the Android app evolution.

### Sprint 01 guarantees

- two organizations cannot read each other's tenant-scoped data;
- an `Operator` cannot access user administration APIs;
- invalid and expired tokens are rejected;
- login and administrative actions are audited.

## Frontend Architecture

The web console is a Vue 3 application using Composition API, Pinia, and Vue Router.

```mermaid
flowchart LR
    Router["Vue Router<br/>route guards"] --> Session["Pinia session store"]
    Session --> ApiClient["Central API client"]
    Session --> Shell["App shell"]
    Shell --> Dashboard["Overview"]
    Shell --> Map["Fleet map"]
    Shell --> Admin["User administration"]
    Shell --> Integrations["Integrations console"]
    Map --> SignalR["SignalR tracking hub"]
    ApiClient --> Backend["FleetOps API"]
```

### Web responsibilities

- login experience for seeded demo accounts;
- session persistence in local storage;
- role-aware navigation;
- organization-scoped user administration;
- authenticated telemetry fetch and SignalR connection.
- professional fleet registry screens for vehicles, drivers, and GPS devices;
- CSV import panels with success, empty, loading, and error states;
- role-aware controls that hide server-forbidden create/deactivate actions from non-admin users.
- a live fleet map synchronized with a vehicle list, paged history, and connection-state feedback.
- a dispatch board for mission creation, assignment, lifecycle transitions, delay simulation, timeline review, inspection evidence, and delivery proof review.
- an alert center dashboard for scan execution, notifications, ownership assignment, acknowledgment, and admin-only compliance or maintenance setup.
- an integrations console for API credentials, webhook supervision, published contracts, outbox visibility, and CSV exchange operations.

## Technology Stack

### Backend

- `.NET 10`
- `ASP.NET Core`
- `ASP.NET Core Identity`
- `JWT Bearer Authentication`
- `SignalR`
- `Entity Framework Core`
- `SQL Server`
- `Minimal APIs`
- `OpenTelemetry OTLP`

### Frontend

- `Vue 3`
- `TypeScript`
- `Pinia`
- `Vue Router`
- `Vite`
- `Vitest`
- `ESLint`
- `Prettier`
- `Bootstrap 5`
- `Leaflet`
- `@microsoft/signalr`

### Mobile

- `Kotlin`
- `Jetpack Compose`
- `Material 3`
- `Room`
- `Retrofit`
- `OkHttp`
- `WorkManager`
- `KSP`
- `Android Gradle Plugin`

### Storage and Media

- `Private filesystem-backed object storage abstraction`
- `Signed download URLs`
- `Chunked resumable upload sessions`

### Tooling and Infrastructure

- `Docker Compose`
- `SQL Server`
- `MinIO`
- `Mosquitto`
- `Mailpit`
- `dotnet-ef`
- `Nginx`

## Product Surfaces

The following captures were produced from a deterministic pilot walkthrough on July 16, 2026. They show the same product baseline across role-specific surfaces and two isolated demo tenants.

### Northwind Logistics admin security and governance

Northwind administrators can manage MFA, review tenant lifecycle metrics, export a tenant package, and run controlled purge actions from the same hardened console.

![Northwind admin security console](docs/assets/screenshots/admin-security-northwind.png)

### Northwind Logistics operator dashboard

The operator dashboard consolidates fleet health, alert pressure, maintenance workload, and notification traces in a single operational view.

![Northwind operator dashboard](docs/assets/screenshots/operator-dashboard-northwind.png)

### Northwind Logistics dispatch board

The dispatch board links mission planning, execution status, delay simulation, and route timelines with the same tenant-scoped mission model consumed by the Android driver app.

![Northwind dispatch board](docs/assets/screenshots/operator-dispatch-northwind.png)

### Southridge Transport isolated tenant view

Southridge starts with its own clean tenant workspace, proving that dashboard data, alerts, and fleet context do not bleed across organizations.

![Southridge isolated admin dashboard](docs/assets/screenshots/admin-dashboard-southridge.png)

### Android driver app

The native driver surface keeps missions readable offline, reflects sync state explicitly, and exposes the same route context used by dispatch and proof-of-delivery workflows.

![Android driver mission list](docs/assets/screenshots/android-driver-missions.png)

![Android driver mission detail](docs/assets/screenshots/android-driver-mission-detail.png)

## Demo Accounts

The local seed creates isolated demo organizations and users.

| Organization | Role | Email | Password |
|---|---|---|---|
| Northwind Logistics | Admin | `admin@northwind.local` | `Admin123!` |
| Northwind Logistics | Operator | `operator@northwind.local` | `Operator123!` |
| Northwind Logistics | Driver | `driver@northwind.local` | `Driver123!` |
| Southridge Transport | Admin | `admin@southridge.local` | `Admin123!` |
| Southridge Transport | Operator | `operator@southridge.local` | `Operator123!` |

## Local Environment

Validated locally on Windows with:

- `.NET SDK 10.0.201`
- `Node.js 24.18.0`
- `npm 11.16.0`
- `Docker Desktop 28.0.1`
- `Android Studio JBR 21`
- `Android SDK platform 35`

Create a local `.env` if needed:

```powershell
Copy-Item .env.example .env
```

## Running the Platform

### 1. Start infrastructure

```powershell
./scripts/dev-up.ps1
docker compose ps
```

### 2. Restore backend tools and packages

```powershell
dotnet tool restore
dotnet restore FleetOps.slnx
```

### 3. Apply the database

```powershell
dotnet dotnet-ef database update --project apps/backend/FleetOps.Infrastructure --startup-project apps/backend/FleetOps.Api
```

### 4. Start the API

```powershell
dotnet run --project apps/backend/FleetOps.Api
```

### 5. Start the web console

```powershell
Set-Location apps/web
npm ci
npm run dev -- --host 127.0.0.1
```

### 6. Optional: start the GPS simulator

Dry-run:

```powershell
dotnet run --project simulators/GpsSimulator -- --dry-run --once
```

Connected mode:

```powershell
dotnet run --project simulators/GpsSimulator
```

Replay duplicate and out-of-order events:

```powershell
dotnet run --project simulators/GpsSimulator -- --once --replay-duplicate --send-out-of-order
```

### 7. Optional: validate the Android app

```powershell
Set-Location apps/android-driver
.\gradlew.bat testDebugUnitTest assembleDebug --stacktrace
```

If your API is running on a host other than the Android emulator loopback, set:

```powershell
$env:FLEETOPS_API_URL="http://10.0.2.2:5080/"
```

### 8. Pilot container deployment

```powershell
docker compose --env-file .env -f docker-compose.yml -f docker-compose.pilot.yml up -d --build
```

Pilot defaults:

- web console: `http://localhost:8081`
- API: `http://localhost:5080`
- readiness probe: `http://localhost:5080/health/ready`

Optional observability:

```powershell
$env:OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4317"
```

### 9. Backup and restore drills

Create a SQL backup:

```powershell
./scripts/sql-backup.ps1
```

Restore a SQL backup:

```powershell
./scripts/sql-restore.ps1 -InputPath backups/fleetops-YYYYMMDD-HHMMSS.bak
```

## Quality Gate

The repository includes a local quality gate that validates the complete Sprint 00 through Sprint 09 baseline:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality-gate.ps1
```

It covers:

- Git status visibility;
- tool restore;
- Docker Compose validation;
- backend restore, format, build, and tests;
- GPS simulator dry-run;
- web install, format, lint, tests, and build;
- API health check;
- API readiness check through the production boot path;
- Android wrapper and Android build.

## Validation Summary

Current local validation includes:

- backend build in `Release`;
- backend tests including integration coverage for auth, roles, tokens, and tenant isolation;
- fleet registry unit and integration tests covering tenant isolation, role permissions, duplicate data, stale updates, CSV idempotency, and assignment invariants;
- tracking unit and integration tests covering duplicate telemetry, out-of-order handling, paged history, tenant isolation, and multi-vehicle visibility;
- dispatch domain and integration tests covering mission lifecycle, illegal transitions, tenant-safe assignment, schedule conflicts, and mission-to-map linkage;
- driver mobile integration tests covering assigned-mission filtering, idempotent command sync, stale row-version conflicts, inspection blocking, resumable uploads, and proof visibility;
- web tests, lint, format, and production build;
- web dispatch board coverage for mission rendering, assignment context, timeline visibility, and map linkage;
- authenticated tracking endpoints, paged history, metrics, and SignalR hub;
- alerting and maintenance tests covering deduplication, UTC handling, notification failure tolerance, role permissions, tenant isolation, and restart-safe re-scans;
- integration tests covering OpenAPI exposure, API key scope isolation, forged webhook rejection, retry/dead-letter handling, and CSV exports;
- integration tests covering administrator MFA enablement and login challenge plus tenant-scoped lifecycle export and controlled purge;
- EF Core migrations for Sprint 01, Sprint 02, Sprint 03, Sprint 04, Sprint 05, Sprint 06, Sprint 07, and Sprint 08;
- OpenTelemetry OTLP wiring and JSON logs for the API and worker runtime;
- pilot Docker packaging plus SQL backup and restore scripts;
- Android unit tests and debug assembly;
- full local quality gate.

## Engineering Notes

### Why a modular monolith?

The MVP favors speed, simplicity, and operational clarity. The monolith keeps local development, CI, migrations, and observability much easier while still preserving domain boundaries.

### Why JWT in Sprint 01?

Sprint 01 needs real claim-based tenant resolution and testable token refusal scenarios. JWT bearer auth gives a lightweight, demonstrable baseline while leaving room for future external OIDC federation.

### Why separate Web and Android apps?

Admin/Operator and Driver workflows have different interaction models, offline requirements, and device constraints. The split keeps each experience focused and technically appropriate.

## Roadmap Snapshot

| Sprint | Focus |
|---|---|
| 00 | Foundation, CI, local reproducibility |
| 01 | Identity, organizations, roles, tenant isolation |
| 02 | Fleet registry, drivers, devices |
| 03 | Live tracking and simulation |
| 04 | Dispatch and missions |
| 05 | Android driver workflow |
| 06 | Inspections and proof of delivery |
| 07 | Alerts and maintenance |
| 08 | Integrations and audit |
| 09 | Production hardening and pilot |

## Supporting Documentation

- [ROADMAP.md](./ROADMAP.md)
- [VALIDATION.md](./VALIDATION.md)
- [docs/01-architecture/ARCHITECTURE.md](./docs/01-architecture/ARCHITECTURE.md)
- [docs/01-architecture/DOMAIN_MODEL.md](./docs/01-architecture/DOMAIN_MODEL.md)
- [docs/02-engineering/ENGINEERING_STANDARDS.md](./docs/02-engineering/ENGINEERING_STANDARDS.md)
- [docs/02-engineering/PILOT_RUNBOOK.md](./docs/02-engineering/PILOT_RUNBOOK.md)
- [docs/03-commercial/PILOT_ONBOARDING.md](./docs/03-commercial/PILOT_ONBOARDING.md)
- [sprints/SPRINT-01-IDENTITY-TENANCY.md](./sprints/SPRINT-01-IDENTITY-TENANCY.md)
- [sprints/SPRINT-02-FLEET-REGISTRY.md](./sprints/SPRINT-02-FLEET-REGISTRY.md)
- [sprints/SPRINT-03-TRACKING-SIMULATION.md](./sprints/SPRINT-03-TRACKING-SIMULATION.md)
- [sprints/SPRINT-04-DISPATCH-MISSIONS.md](./sprints/SPRINT-04-DISPATCH-MISSIONS.md)
- [sprints/SPRINT-05-ANDROID-DRIVER.md](./sprints/SPRINT-05-ANDROID-DRIVER.md)
- [sprints/SPRINT-06-INSPECTIONS-POD.md](./sprints/SPRINT-06-INSPECTIONS-POD.md)
- [sprints/SPRINT-07-ALERTS-MAINTENANCE.md](./sprints/SPRINT-07-ALERTS-MAINTENANCE.md)
- [sprints/SPRINT-08-INTEGRATIONS-AUDIT.md](./sprints/SPRINT-08-INTEGRATIONS-AUDIT.md)
- [sprints/SPRINT-09-PRODUCTION-PILOT.md](./sprints/SPRINT-09-PRODUCTION-PILOT.md)
