# Implementation Roadmap

This roadmap is the executable sprint ledger for Orkystra. Each sprint must be completed independently and must update this file before the next sprint starts.

## Completion Rules

A sprint is complete only when:

- Build passes for touched components.
- Tests pass for touched components.
- Documentation is updated.
- Architecture boundaries are preserved.
- No critical warnings remain unexplained.
- `PROJECT_STATUS.md` reflects the new state.

## Sprint 0 - Project Skeleton

Status: Completed

Goal:
Establish the repository as a durable monorepo that Codex can resume safely.

Deliverables:

- Stable folder structure.
- Git initialized.
- `.gitignore`.
- README.
- Constitution and blueprint organized.
- Autopilot prompt.
- Sprint protocol.
- Initial architecture docs scaffold.
- Basic backend, frontend, Python service, infrastructure, and test placeholders.
- Basic CI workflow.

Exit criteria:

- A new implementation session can understand where to continue by reading the workflow, current-state, roadmap, and status files.
- Backend, frontend, and Python skeleton checks pass.

## Sprint 1 - Core Domain Primitives

Status: Completed

Goal:
Create the first backend domain foundation without infrastructure dependencies.

Deliverables:

- Identity types.
- Value objects.
- Base entity and aggregate abstractions.
- Domain event base model.
- Result or validation pattern if selected.
- Unit tests for invariants.

Implemented notes:

- Added strong `Guid`-backed identifier types for core logistics concepts.
- Added immutable validated value objects for quantity, weight, volume, money, geo coordinates, time windows, and risk score.
- Added `Entity<TId>`, `AggregateRoot<TId>`, `DomainEvent`, `Result`, and `DomainError`.
- Added 14 backend unit tests covering identifiers, value object invariants, result behavior, and aggregate event collection.

Exit criteria:

- Domain primitives compile and tests pass.
- No persistence, HTTP, MQTT, or UI dependency leaks into the domain layer.

## Sprint 2 - Warehouse Context

Status: Completed

Goal:
Implement the first meaningful warehouse domain slice.

Deliverables:

- Warehouse, Zone, Rack, Slot, Dock, and Pallet basics.
- Warehouse events.
- Pallet movement and dock occupancy invariants.
- Warehouse read model contract.
- Tests for valid and invalid transitions.

Implemented notes:

- Added `Warehouse` as the aggregate root with `Zone`, `Rack`, `Slot`, `Dock`, and `Pallet` entities.
- Added warehouse event types for creation, storage, movement, and dock occupancy lifecycle.
- Enforced slot occupancy, slot weight, pallet uniqueness, and dock occupancy rules.
- Added a first warehouse summary read-model contract in `Orkystra.Contracts`.
- Added 8 warehouse-focused tests, bringing the backend domain suite to 22 passing tests.

Exit criteria:

- A simulated warehouse state can be represented and mutated through domain methods.

## Sprint 3 - Transport Context

Status: Completed

Goal:
Implement the first transport domain slice.

Deliverables:

- Truck, Driver, Route, Stop, Shipment, and Delivery basics.
- Route lifecycle events.
- Delay event model.
- Tests for route and shipment transitions.

Implemented notes:

- Added `Route` as the aggregate root with `Truck`, `Driver`, `Stop`, `Shipment`, and `Delivery`.
- Added transport event types for assignment, loading, departure, delay, arrival, and delivery completion.
- Enforced route departure prerequisites, truck capacity constraints, and valid shipment lifecycle transitions.
- Added the first transport read-model contract in `Orkystra.Contracts`.
- Added 8 transport-focused tests, bringing the backend domain suite to 30 passing tests.

Exit criteria:

- A simulated shipment can be assigned to a route and moved through lifecycle states.

## Sprint 4 - Simulation Engine

Status: Completed

Goal:
Create deterministic simulation foundations.

Deliverables:

- Scenario model.
- Virtual clock.
- Seeded random source.
- Synthetic warehouse, order, and truck generators.
- Event generation service.
- Replay design note.

Implemented notes:

- Added `Scenario` as the aggregate root for simulation lifecycle and virtual time.
- Added `SimulationClock`, `SimulationSeed`, and deterministic random-source support.
- Added synthetic world generation for warehouses, orders, and trucks.
- Added deterministic disruption generation and simulation event types.
- Added the first simulation read-model contract in `Orkystra.Contracts`.
- Added 7 simulation-focused tests, bringing the backend domain suite to 37 passing tests.

Exit criteria:

- Running the same scenario seed produces the same event sequence.

## Sprint 5 - Event Backbone

Status: Completed

Goal:
Create the event envelope and first event publishing pipeline.

Deliverables:

- Event envelope contract.
- Topic naming constants.
- Outbox and inbox design.
- MQTT integration skeleton.
- Projection update skeleton.
- Contract tests.

Exit criteria:

- A simulation event can be published, consumed idempotently, and projected.

Implemented notes:

- Added shared event-envelope contracts, MQTT-aligned topic naming conventions, and outbox/inbox transport records in `Orkystra.Contracts`.
- Added `DomainEventEnvelopeFactory` in `Orkystra.Application` to map domain events to routable envelopes.
- Added `IdempotentProjectionRunner`, `IInboxStateStore`, and an in-memory inbox implementation for duplicate-safe consumers.
- Added the first `ScenarioSummaryProjection` skeleton to project simulation events into a read model.
- Added 4 backbone-focused tests, bringing the backend test suite to 41 passing tests.

## Sprint 6 - Control Tower UI

Status: Completed

Goal:
Create the first visible demo shell.

Deliverables:

- Vue app shell.
- Dashboard layout.
- 3D warehouse placeholder connected to read model DTOs.
- Map placeholder connected to read model DTOs.
- Simulation control panel.
- Details panel.

Exit criteria:

- A user can open the app and see simulated state without knowing the data source.

Implemented notes:

- Replaced the starter Vue shell with a control-tower workspace built around scenario, warehouse, transport, and alert surfaces.
- Added read-model-shaped demo data so the UI can evolve independently from unfinished API and broker adapters.
- Added a first interactive Three.js warehouse twin placeholder with rotating rack volumes and dock beacons.
- Added simulation controls, route network placeholder, and live narrative panels for a realistic operator demo.
- Added frontend bundling refinements for the isolated Three.js chunk and verified the production build.

## Sprint 7 - AI Layer 1

Status: Completed

Goal:
Create the first grounded AI assistance slice.

Deliverables:

- FastAPI AI service skeleton.
- LangGraph supervisor skeleton.
- Warehouse and dispatcher tool contracts.
- Response format with evidence and assumptions.
- Basic RAG ingestion plan.

Exit criteria:

- The AI can answer a narrow operational question from projections or explicitly say the evidence is missing.

Implemented notes:

- Replaced the AI service stub with a FastAPI service that exposes grounded recommendation endpoints and demo projection data.
- Added projection-shaped request and response contracts with explicit evidence, assumptions, confidence, recommended actions, and missing-data fields.
- Added warehouse and dispatcher specialist tools plus a supervisor router with a LangGraph-shaped orchestration skeleton and deterministic fallback.
- Added a first RAG ingestion plan contract to document future knowledge grounding without pretending retrieval is already live.
- Added 6 Python unit tests for intent routing, specialist behavior, supervisor fallback, and RAG policy.

## Sprint 8 - Optimization Layer

Status: Completed

Goal:
Add the first OR-Tools optimization use case.

Deliverables:

- Route optimization input model.
- Solver service.
- Solution explanation model.
- Alternative route support.
- Tests for feasible and infeasible cases.

Exit criteria:

- A route plan can be optimized and explained in user-facing terms.

Implemented notes:

- Replaced the optimization service stub with a FastAPI service that exposes route optimization endpoints and backend capability reporting.
- Added a canonical route optimization contract with depot, vehicle, stop, time-window, and constraint models.
- Added an explainable solver that prefers OR-Tools when available and falls back to a deterministic evaluator otherwise.
- Added objective score, ETA, load distribution, infeasibility reporting, and alternative-plan outputs for dispatcher-facing explanations.
- Added 4 optimization-focused Python tests, bringing the Python service suite to 10 passing tests.

## Sprint 9 - Connector Layer

Status: Completed

Goal:
Prove that simulation providers can be replaced by real adapters.

Deliverables:

- Provider registry.
- CSV import provider.
- REST adapter skeleton.
- GPS adapter skeleton.
- Provider health contract.

Exit criteria:

- A provider can be swapped without changing domain logic or frontend contracts.

Implemented notes:

- Added provider-neutral connector contracts for health, capabilities, sync status, schema description, and GPS position snapshots.
- Added `IProviderAdapter` plus warehouse, transport, and GPS domain-specific adapter interfaces in `Orkystra.Application`.
- Added a `ProviderRegistry` that resolves providers by identifier, domain, and capability.
- Added skeleton adapters for CSV warehouse import, REST transport integration, and GPS telematics snapshots.
- Added 4 connector-focused backend tests, bringing the backend test suite to 45 passing tests.

## Sprint 10 - Production Hardening

Status: Completed

Goal:
Prepare the MVP for real operational demos and future SaaS.

Deliverables:

- Auth baseline.
- Tenant model.
- Audit logs.
- Observability.
- Deployment packaging.
- Smoke-test checklist.

Exit criteria:

- The MVP can be deployed, observed, and demonstrated reliably.

Implemented notes:

- Added API-key authentication baseline, tenant resolution baseline, audit logging scaffold, and request metrics in `Orkystra.Api`.
- Added liveness, readiness, metrics, and operational-context endpoints for a first observability surface.
- Added Dockerfiles for the API, frontend, AI service, and optimization service plus a stack-level compose file.
- Added a smoke-test checklist and updated development docs for repeatable demo bring-up.
- Added 4 production-hardening backend tests, bringing the backend suite to 49 passing tests.

## Sprint 11 - API To UI Wiring

Status: Completed

Goal:
Connect the control tower frontend to a real backend overview endpoint while preserving a resilient demo fallback.

Deliverables:

- Control tower overview API endpoint.
- UI-facing overview contract.
- Frontend fetch service with authenticated request headers.
- Fallback local demo data when the API is unavailable.
- Verification that API and UI builds still pass.

Exit criteria:

- The control tower frontend can render from API-delivered overview data without losing local demo resilience.

Implemented notes:

- Added `GET /api/control-tower/overview` in `Orkystra.Api`.
- Added control tower overview contracts in `Orkystra.Contracts`.
- Added a backend overview service that assembles scenario, warehouse, route, alert, and event-feed data.
- Added frontend API wiring and view-model mapping with a local fallback path.
- Added 1 backend overview test, bringing the backend suite to 50 passing tests.

## Sprint 12 - Data Source Observability

Status: Completed

Goal:
Make the control tower's API connection operationally transparent by exposing data-source freshness, provider health, and visible fallback states.

Deliverables:

- Overview metadata with generation timestamp.
- Provider status snapshots in the overview contract.
- Frontend loading, fallback, and connection-state messaging.
- Provider health visibility in the operator UI.
- Verification that backend and frontend still build and the enriched endpoint responds correctly.

Exit criteria:

- An operator can tell whether the control tower is rendering API data or fallback data and can see the health posture of the current providers.

Implemented notes:

- Added `generatedAtUtc` plus provider status records to the control tower overview contract.
- Extended the backend overview service to collect provider health and sync states from the registry.
- Added frontend connection-state handling, fallback error visibility, and a provider watch surface.
- Kept the local fallback path intact while making its activation explicit in the UI.
- Re-verified the backend suite at 50 passing tests, the frontend production build, and the live overview endpoint payload.

## Sprint 13 - Broader Overview Coverage

Status: Completed

Goal:
Make the API-backed control tower feel more operationally representative by serving broader warehouse and route coverage from the overview endpoint.

Deliverables:

- Multiple warehouse summaries from the warehouse demo provider.
- Multiple route summaries from the transport demo provider.
- Alerts and event-feed entries derived from live overview contents instead of only static text.
- Tests updated to validate the richer provider and overview payloads.
- Verification that the frontend still builds and the enriched endpoint returns wider coverage.

Exit criteria:

- The API overview exposes more than one warehouse and more than one route, and the resulting alerts and event feed reflect those current projections.

Implemented notes:

- Expanded the warehouse demo provider to return `North Hub A` and `West Flow Center`.
- Expanded the transport demo provider to return `RT-204`, `RT-318`, and `RT-412` with varied statuses.
- Updated the control tower overview service to derive operational alerts and event-feed entries from route, warehouse, and provider data.
- Updated connector and overview tests to validate the richer payload.
- Re-verified the backend suite, frontend production build, and the live overview endpoint with 2 warehouses, 3 routes, and 6 event-feed entries.

## Sprint 14 - Connector Catalog Visibility

Status: Completed

Goal:
Expose the provider registry as an operator-facing catalog so the team can inspect capabilities, schemas, and supported canonical read models in one place.

Deliverables:

- Provider catalog API endpoint.
- Provider catalog read-model contracts.
- Frontend connector catalog surface.
- Tests validating the catalog service payload.
- Verification that the catalog endpoint and UI build still pass.

Exit criteria:

- Operators can inspect the current provider inventory, capabilities, and schema surface without opening the codebase.

Implemented notes:

- Added `GET /api/providers/catalog` in `Orkystra.Api`.
- Added provider catalog contracts in `Orkystra.Contracts`.
- Added a provider catalog service that projects health, sync, capability, and schema metadata from the registry.
- Added a frontend catalog surface that renders provider capabilities, supported read models, and schema summaries.
- Added a backend test for the provider catalog and re-verified the backend suite at 51 passing tests.

## Sprint 15 - Local Audit Persistence

Status: Completed

Goal:
Turn audit from log-only scaffolding into a locally persisted operational trail that can be inspected through the API.

Deliverables:

- Append-only local audit store.
- Audit entry contract and persistence service.
- Protected API endpoint for recent audit reads.
- Tests for audit-store behavior and observability defaults.
- Documentation update for local API key configuration and audit posture.

Exit criteria:

- Protected API activity produces locally persisted audit entries that can be queried through the API for support and demo workflows.

Implemented notes:

- Added `AuditEntry`, `IAuditStore`, and `FileAuditStore` in `Orkystra.Api`.
- Updated `AuditLoggingMiddleware` to append every protected operational request to a local JSONL audit file.
- Added `GET /observability/audit` with bounded recent-entry reads.
- Added file-audit-store tests and re-verified the backend suite at 53 passing tests.
- Documented the local API key injection pattern required after removing committed development secrets.

## Sprint 16 - Provider Configuration Posture

Status: Completed

Goal:
Expose the local runtime posture of each provider so operators can see which connectors are enabled, where they run, and which configuration fields are still missing.

Deliverables:

- Provider runtime configuration options in the API.
- Configuration summary in provider catalog contracts.
- Catalog UI updates for enabled state, environment, configured fields, and missing fields.
- Backend test coverage for configuration-aware provider catalog output.
- Verification that the catalog endpoint returns configuration readiness for every provider.

Exit criteria:

- The provider catalog shows whether each provider is configured, partially configured, or missing configuration, without exposing secrets.

Implemented notes:

- Added runtime configuration options and configuration summary contracts for provider catalog items.
- Extended `ProviderCatalogService` to derive readiness, configured fields, and missing fields from local provider runtime settings.
- Updated the frontend connector catalog to render provider configuration posture directly in the operator workspace.
- Re-verified the backend suite at 53 passing tests, the frontend production build, and the live catalog endpoint with 3 configured providers.

## Sprint 17 - Editable Provider Configuration

Status: Completed

Goal:
Allow operators to update non-secret provider runtime settings through the product instead of editing files manually.

Deliverables:

- Protected provider-configuration update endpoint.
- Local runtime store with ignored file persistence.
- Catalog contract updates for editable configuration values.
- Frontend runtime editor for enabled state, environment, and approved settings.
- Verification that local edits persist and are visible again through the catalog.

Exit criteria:

- An operator can edit safe provider runtime settings from the product, save them locally, and see the updated posture reflected without exposing secrets.

Implemented notes:

- Added `ProviderRuntimeStore` to hold provider runtime state and persist it into ignored `appsettings.Local.json`.
- Added provider metadata and request contracts so the API only accepts known providers and approved non-secret fields.
- Added `PUT /api/providers/catalog/{providerId}/configuration` in `Orkystra.Api`.
- Extended the catalog UI with inline runtime-editing controls and save feedback.
- Re-verified the backend suite at 55 passing tests, the frontend production build, the live configuration update endpoint, and a browser-driven local save flow.

## Sprint 18 - Browser QA And Visual Hardening

Status: Completed

Goal:
Validate the control tower end to end in a browser and fix layout, loading, and interaction issues across desktop and mobile.

Deliverables:

- Browser-driven verification of the local control tower on desktop and mobile-width viewports.
- API-loading hardening so overview and catalog surfaces recover more gracefully during local restarts.
- UI connection-state visibility for overview and provider-catalog surfaces.
- Visual fixes for responsive layout and local interaction polish.
- Updated smoke-test guidance for browser-facing operator checks.

Exit criteria:

- The control tower can be opened in a browser on desktop and mobile widths without layout overlap, and partial local startup failures recover to stable API-backed state.

Implemented notes:

- Moved CORS handling ahead of tenancy/auth flow and exempted `OPTIONS` requests from tenant resolution so browser preflight traffic can succeed.
- Added a shared frontend API request helper with retry behavior for transient local failures.
- Added a connection-posture surface plus a manual `Refresh data` action in the control tower UI.
- Added an automatic recovery retry when only one of the two API-backed surfaces falls back during startup timing races.
- Replaced the deprecated Three.js shadow-map setting and enabled better touch interaction handling for the warehouse twin.
- Re-verified the backend suite at 56 passing tests, the frontend production build, browser behavior on desktop, and browser behavior on a mobile-width viewport.

## Sprint 19 - Warehouse Projection API

Status: Completed

Goal:
Expose warehouse-focused API projections beyond the overview so the product can drill into warehouse state with less fallback shaping in the frontend.

Deliverables:

- Dedicated warehouse detail contracts for zones and docks.
- Protected `GET /api/warehouses` and `GET /api/warehouses/{warehouseId}` endpoints.
- Backend service for warehouse projection lookup.
- Frontend warehouse-detail loader wired to the selected warehouse.
- Updated smoke-test and development docs.

Exit criteria:

- The operator can switch warehouses and see API-backed zone and dock detail without depending on hardcoded frontend decorations.

Implemented notes:

- Added `WarehouseDetailReadModel`, `WarehouseZoneReadModel`, and `WarehouseDockReadModel` to `Orkystra.Contracts`.
- Added `WarehouseProjectionService` and new warehouse endpoints in `Orkystra.Api`.
- Extended the CSV warehouse demo provider so summary and detail projections come from the same canonical source.
- Added backend tests for warehouse projection listing and detail lookup, bringing the backend suite to 59 passing tests.
- Rewired the frontend warehouse twin to fetch detail data per selected warehouse and show dock posture plus projection freshness.
- Re-verified the backend suite, frontend production build, live warehouse endpoints, and browser warehouse switching on desktop plus mobile-width rendering.

## Sprint 20 - Transport Projection API

Status: Completed

Goal:
Expose transport-focused API projections beyond the overview so route and shipment workflows can evolve independently from the control-tower summary payload.

Deliverables:

- Dedicated transport route detail contracts for stops, shipments, and deliveries.
- Protected `GET /api/transport/routes` and `GET /api/transport/routes/{routeId}` endpoints.
- Backend service for transport projection lookup.
- Frontend transport board wired to API-backed route detail data.
- Updated smoke-test and development docs.

Exit criteria:

- The operator can switch routes and see API-backed route, shipment, and delivery detail without relying on hardcoded frontend shaping.

Implemented notes:

- Added `RouteDetailReadModel`, `TransportRouteStopReadModel`, `TransportRouteShipmentReadModel`, and `TransportRouteDeliveryReadModel` to `Orkystra.Contracts`.
- Added `TransportProjectionService` and new transport endpoints in `Orkystra.Api`.
- Extended the REST transport demo provider so summary and detail projections come from the same canonical source.
- Added backend tests for transport projection listing and detail lookup, bringing the backend suite to 62 passing tests.
- Rewired the frontend transport board to fetch detailed route data per selected route and render stops, shipments, deliveries, and route metrics.
- Re-verified the backend suite, frontend production build, live transport endpoints, and browser route switching on the operator workspace.

## Sprint 21 - AI Workflow Integration

Status: Completed

Goal:
Connect the AI service to the operator workspace through a bounded recommendation workflow with explicit evidence and missing-data handling.

Deliverables:

- Protected AI recommendation endpoint in the API.
- Backend workflow client that forwards tenant-aware projections to the Python AI service.
- Local fallback recommendation path when the Python service is unavailable.
- Frontend AI workflow panel with question input, quick prompts, evidence, assumptions, actions, confidence, and missing-data views.
- Documentation and smoke-test updates.

Implemented notes:

- Added `POST /api/ai/recommendations` in `Orkystra.Api` and wired it to the current control-tower overview snapshot.
- Added public AI recommendation contracts plus a backend AI workflow client that can call the Python AI service or fall back locally.
- Added AI service base-url configuration and stack-compose wiring so the live Python service can be reached from the API container.
- Added a dedicated AI workflow surface in the frontend control tower with quick prompts and grounded recommendation rendering.
- Re-verified the backend suite at 64 passing tests, the frontend production build, the live AI service, the backend AI endpoint, and the browser AI workflow panel.

## Sprint 22 - Optimization Workflow Integration

Status: Completed

Goal:
Connect route optimization outputs to dispatcher-facing workflows so plans can be generated, compared, and reviewed from the product surface.

Deliverables:

- Protected optimization workflow endpoint in the API.
- Backend workflow client that forwards tenant-aware route projections to the Python optimization service.
- Local fallback optimization path when the Python service is unavailable.
- Frontend dispatcher review panel with current order, recommended order, explanation, and alternatives.
- Documentation and smoke-test updates.

Exit criteria:

- An operator can request a route optimization review from the product surface and compare the recommended sequence with the current remaining plan without depending on direct browser access to the Python service.

Implemented notes:

- Added `POST /api/transport/routes/{routeId}/optimization` in `Orkystra.Api` and wired it to the selected route detail projection plus current scenario id.
- Added public optimization workflow contracts plus a backend workflow client that can call the Python optimization service or fall back locally when the service is unavailable.
- Added optimization service base-url configuration and stack-compose wiring so the live Python optimization service can be reached from the API container.
- Added a dedicated optimization review surface in the frontend control tower with current route order, recommended order, explanation, constraint posture, and alternatives.
- Re-verified the backend suite at 66 passing tests, the frontend production build, the Python unit suite, Python compile checks, and the stack compose configuration.

## Sprint 23 - Centralized Persistence Foundations

Status: Completed

Goal:
Introduce durable storage for key projections and operational records so the product no longer relies only on in-memory and file-local state.

Deliverables:

- Structured operational persistence store in the API.
- Durable snapshot persistence for key read-model responses.
- Durable workflow-run persistence for AI and optimization requests.
- Protected observability endpoints for reading persisted snapshots and workflow runs.
- Tests and documentation updates.

Exit criteria:

- Key operator-facing projections and workflow envelopes are persisted durably through one structured backend store, and the team can inspect recent persisted state without opening local files directly.

Implemented notes:

- Added `OperationalPersistenceStore` in `Orkystra.Api` backed by a local SQLite database at `output/persistence/orkystra-operations.db`.
- Added persisted snapshot writes for control-tower overview, warehouse summaries and details, route summaries and details, and provider catalog responses.
- Added persisted workflow-run writes for `POST /api/ai/recommendations` and `POST /api/transport/routes/{routeId}/optimization`.
- Added `GET /observability/persistence/projections` and `GET /observability/persistence/workflows` so recent persisted state can be inspected through protected API routes.
- Added 2 persistence-focused backend tests, bringing the backend suite to 68 passing tests, and re-verified frontend build, Python checks, and stack compose configuration.

## Sprint 24 - Demo-Ready Operational Polish

Status: Completed

Goal:
Harden the full product for near-final demos with stronger operational flows, clearer UX, and a tighter end-to-end story.

Deliverables:

- Operator-facing product surface for recent persisted workflow and audit activity.
- Clearer demo story linking recommendations, optimizations, and persisted backend evidence.
- Local demo-stack revalidation on the latest backend code.
- Documentation and smoke-test updates.

Exit criteria:

- A demo operator can show not only the current logistics state, but also recent persisted workflow runs and protected activity evidence directly from the product surface.

Implemented notes:

- Added `frontend/web/src/services/observabilityApi.ts` to load persisted workflow runs, persisted projection snapshots, and recent audit entries from the protected API.
- Added an `Operational trace` surface in the frontend control tower that exposes recent AI/optimization runs, persisted read-model snapshots, and protected API audit activity.
- Wired the control tower to refresh that trace after workspace refreshes, AI recommendation requests, optimization requests, and provider configuration saves.
- Re-verified the backend suite at 68 passing tests, the frontend production build, Python checks, stack compose configuration, and live 200 responses from the new persistence observability endpoints after restarting the API.

## Sprint 25 - Live Provider Integration Foundations

Status: Completed

Goal:
Replace one demo-backed provider path with the first disciplined live integration flow so the platform begins crossing from simulation-ready product into customer-ready connectivity.

Deliverables:

- Runtime-aware provider registry composition in the API.
- Live-capable REST transport provider with disciplined fallback behavior.
- Placeholder-safe upstream validation so local demo defaults do not trigger broken outbound calls.
- Backend tests covering both live upstream reads and demo fallback behavior.
- Documentation updates for local live-provider bring-up.

Exit criteria:

- The transport connector can read from a valid upstream HTTP endpoint when configured locally, while preserving stable demo behavior when configuration is absent, disabled, or placeholder-only.

Implemented notes:

- Added `ProviderRegistryFactory` in `Orkystra.Api` so catalog, overview, warehouse, and transport services build connector registries from current runtime configuration instead of hardcoded provider instances.
- Added `RestTransportProviderConfiguration` plus HTTP-client-backed live-read support in `RestTransportProvider` for `/health`, `/routes`, and `/routes/details`.
- Kept the current control tower safe by treating `.invalid` placeholder hosts and missing base URLs as non-live configuration, which preserves deterministic demo fallback behavior.
- Added backend coverage for both valid live upstream reads and placeholder-driven fallback behavior, bringing the backend suite to 70 passing tests.
- Re-verified the backend suite on the updated registry and live-provider foundation.

## Sprint 26 - Live Provider Authentication And Supportability

Status: Completed

Goal:
Turn the first live provider foundation into a safer operator workflow with secret-aware configuration posture, clearer error diagnostics, and repeatable local support steps.

Deliverables:

- Secret-aware provider configuration posture for the live transport adapter.
- Safer local auth handling for API-key-style upstreams without exposing secrets in the UI.
- Clearer provider health and sync diagnostics for live-versus-fallback transport mode.
- Support-oriented documentation and smoke-test guidance for local live-provider bring-up.
- Focused backend tests for auth/header behavior and configuration edge cases.

Exit criteria:

- An operator or developer can configure a live transport endpoint locally, understand whether auth is still missing, and diagnose why the provider is live, degraded, disabled, or still in fallback mode without exposing secrets in product surfaces.

Implemented notes:

- Added `ProviderSecretStore` in `Orkystra.Api` to hold secrets separately from regular provider settings, loading from environment variables (`ORKYSTRA_PROVIDER_REST_TRANSPORT_ADAPTER_APIKEY`) first and falling back to an ignored `appsettings.Secrets.local.json` file.
- Added `PUT /api/providers/catalog/{providerId}/secrets` endpoint for operators to supply API keys without editing files manually.
- Updated `ProviderConfigurationSummaryReadModel` with `AuthMode` and `AuthConfigured` fields so the catalog exposes auth posture without leaking key values.
- Updated `ProviderRuntimeMetadata` with `GetSecretFields`, `IsSecretField`, and `GetAuthMode` helpers to separate secret fields from editable settings.
- Updated `ProviderRegistryFactory` to accept `ProviderSecretStore` and inject the resolved API key into `RestTransportProvider` at registry construction time.
- Updated `ProviderCatalogService` to compute `AuthMode` and `AuthConfigured` per provider and surface `Auth Key Missing` as a readiness state when auth is required but the key is absent.
- Improved `RestTransportProvider` health and sync diagnostics with explicit `auth-key-missing` and `auth-key-configured` signals, and a clear human-readable message describing how to supply the key.
- Added `ProviderSecretUpdateRequest` contract for the new secrets endpoint.
- Added 5 new `ProviderSecretStoreTests` covering presence checks, file persistence, cross-instance reload, and validation of unknown providers and non-secret fields.
- Added 3 new `RestTransportProviderLiveTests` covering auth-missing health signals, auth-missing sync status, and API key header forwarding when configured.
- Added 3 new `ProviderCatalogTests` covering auth mode surfacing, auth-configured reporting when key is present, and auth-key-missing readiness when key is absent.
- Extended the frontend catalog card with an auth posture badge (`API key: configured / not set`) and a `Set API key` password form that posts to the secrets endpoint and clears the value from the browser after saving.
- Updated `docs/development.md` with a live provider authentication section covering environment variable and API-based key supply workflows.
- Updated `docs/operations/smoke-test-checklist.md` with auth posture and secrets endpoint verification steps.
- Re-verified the backend suite at 81 passing tests and the frontend production build.

## Sprint 27 - MQTT Event Backbone Activation

Status: Completed

Goal:
Activate the first real MQTT event flow so the platform can publish, consume, and project live event traffic instead of stopping at contract-only backbone scaffolding.

Deliverables:

- MQTT-backed event publisher and consumer wiring in the API.
- First projection-driven simulation event flow through the broker.
- Protected API endpoints to publish demo scenario events and read MQTT-fed scenario projections.
- Event-backbone observability endpoint for support workflows.
- Focused backend tests for envelope serialization, projection dispatch, and event publication flow.

Exit criteria:

- At least one scenario event type can be published through MQTT, consumed idempotently, and surfaced again through an API-backed projection read without bypassing the existing envelope and projection abstractions.

Implemented notes:

- Added `MQTTnet` plus `EventBackboneOptions`, `MqttEventPublisher`, `MqttEventConsumerService`, and `EventBackboneMessageDispatcher` in `Orkystra.Api`.
- Added `MqttEnvelopeSerializer` so typed simulation event envelopes can be serialized onto MQTT and reconstructed for projection dispatch.
- Reused `IdempotentProjectionRunner`, `InMemoryInboxStateStore`, and `ScenarioSummaryProjection` as singleton runtime services, and extended the projection with thread-safe storage plus scenario listing.
- Added `GET /observability/event-backbone`, `GET /api/simulation/scenarios`, and `POST /api/simulation/scenarios/demo-events` in the API.
- Added `PublishScenarioEventsRequest` and `PublishScenarioEventsResponse` contracts for the new simulation event workflow.
- Added 3 backend tests covering envelope round-tripping, duplicate-safe projection dispatch, and scenario event publication sequencing, bringing the backend suite to 84 passing tests.
- Re-verified the backend suite on the activated MQTT backbone.

## Sprint 28 - GPS Telematics Stream Activation

Status: Completed

Goal:
Move the freshly activated MQTT backbone beyond simulation-only traffic by feeding the first connector-originated telemetry stream through it and surfacing that stream in operator-facing projections.

Deliverables:

- GPS telemetry publication workflow backed by the existing provider registry.
- MQTT-fed GPS position projection using canonical `GpsPositionSnapshot` payloads.
- Protected API endpoints to publish provider positions and read latest projected GPS positions.
- Serializer and dispatcher support for connector-originated GPS events.
- Focused backend tests for GPS event round-tripping, projection dispatch, and provider-driven publication flow.

Exit criteria:

- The GPS provider can publish canonical position snapshots through MQTT, those events are consumed idempotently, and operators can read the latest projected positions through an API-backed projection endpoint.

Implemented notes:

- Added `IntegrationEventEnvelopeFactory` in `Orkystra.Application` so non-domain connector payloads can reuse the same envelope model and routing metadata strategy as domain events.
- Added `GpsPositionProjection` as a new event projection over canonical `GpsPositionSnapshot` payloads.
- Added `GpsTelemetryWorkflowService` and `GpsProjectionService` in `Orkystra.Api`.
- Added `GET /api/gps/positions` and `POST /api/gps/positions/publish` so GPS telemetry can be published from the provider and read back from MQTT-fed projections.
- Extended the MQTT consumer subscription set to include the configured GPS stream topic from provider runtime settings.
- Extended `MqttEnvelopeSerializer` to support `GpsPositionReported` events carrying `GpsPositionSnapshot` payloads.
- Added 3 backend tests covering GPS event round-tripping, latest-position projection behavior, and provider-driven publication to the runtime stream topic, bringing the backend suite to 87 passing tests.
- Re-verified the backend suite on the GPS-backed MQTT flow.

## Sprint 29 - Transport Live Sync Snapshot Import

Status: Completed

Goal:
Use the now-active live REST transport path and MQTT backbone together by importing a first real transport snapshot and surfacing its operational changes with clearer projection freshness and sync evidence.

Implemented notes:

- Added `TransportSyncWorkflowService` in `Orkystra.Api` so the API can import and persist a transport snapshot through the existing provider registry.
- Added `POST /api/transport/sync` and `GET /api/transport/sync-status` so operators and future automation can trigger a sync and inspect the latest sync evidence.
- Added durable transport sync status persistence plus persisted route summary/detail reuse through `OperationalPersistenceStore` and `TransportProjectionService`.
- Added 3 transport-sync-focused backend tests, bringing the backend suite to 90 passing tests.
- Re-verified the backend suite on the new transport sync workflow.

## Sprint 30 - Transport operator visibility and workflow surfacing

Status: Completed

Goal:
Expose the new transport synchronization evidence in the operator product surface so live-vs-fallback posture, last import freshness, and imported route deltas are visible without leaving the frontend.

Implemented notes:

- Added a dedicated frontend transport sync API client for `GET /api/transport/sync-status` and `POST /api/transport/sync`.
- Added transport sync view-model mapping plus a local fallback shape in `frontend/web/src/data/controlTower.ts`.
- Extended the operator workspace with a transport sync connection card and an in-board sync surface that shows source posture, health, imported route count, route delta summary, imported references, and a manual `Import snapshot` action.
- Re-verified the frontend production build after the new transport sync surface.

## Sprint 31 - Transport timeline and delta storytelling

Status: Completed

Goal:
Make transport changes easier to understand over time by surfacing route-by-route sync deltas, change highlights, and operator-readable transition history around the imported transport snapshot.

Implemented notes:

- Extended operational observability summaries so `transport-sync-import` workflow runs and `transport-sync-status` projection snapshots read cleanly in the UI.
- Added a route storyline card in the transport board that explains selected-route presence in the latest import, delivery progress, sync freshness, and latest sync note.
- Added a transport sync timeline card that reuses persisted workflow runs to show recent import history in operator-friendly language.
- Re-verified the frontend production build after the transport storytelling surface.

## Sprint 32 - Browser-driven transport visual QA and interaction polish

Status: Completed

Goal:
Run a proper browser-facing QA pass on the transport surfaces, then tighten visual hierarchy, spacing, and interaction polish around sync, optimization, and route detail flows.

Implemented notes:

- Verified the transport surfaces in the in-app browser against the local app running on `http://127.0.0.1:4173/#`.
- Adjusted the workspace balance so the transport column gets more room on desktop.
- Converted transport sync metrics to an auto-fit grid and simplified storyline/detail sections into single-column reading flows, which removed the cramped multi-column feel from the transport slice.
- Re-verified the frontend production build and the browser-facing transport layout after the polish pass.

## Sprint 33 - Transport supportability and operator actions

Status: Completed

Goal:
Make the transport slice easier to operate under real demo pressure by adding small support-oriented actions and clearer transport recovery cues around sync, route selection, and optimization.

Implemented notes:

- Added a compact operator-action layer in the transport board with support shortcuts for snapshot import, sync refresh, route refresh, optimization rerun, and imported-route focus.
- Added transport recovery cues that translate stale or degraded transport posture into a recommended next action directly in the UI.
- Kept the implementation frontend-local in `frontend/web/src/App.vue`, reusing the existing transport sync, route projection, and optimization workflows instead of widening the backend surface.
- Updated development and smoke-test documentation so future sessions and alternate tooling flows know how to verify the new operator-support layer.
- Re-verified the frontend production build after the new supportability pass.

## Sprint 34 - Transport historical diff drill-down

Status: Completed

Goal:
Expose route-by-route before-and-after evidence across imported transport snapshots so operators can explain what changed, not just what the latest snapshot currently says.

Implemented notes:

- Added transport historical-diff contracts and a dedicated `TransportSyncHistoryService` in the API to compute route-level added, removed, and changed evidence by comparing the latest two sync imports.
- Added `GET /api/transport/sync-diff` plus workflow-payload enrichment in `POST /api/transport/sync` so each import stores both sync posture and route-summary evidence for future diffs.
- Added backend test coverage for historical diff behavior, including empty-history posture and added/removed/changed classification.
- Added frontend transport historical diff wiring and UI drill-down in the transport board, including latest-versus-previous counts and route-level change summaries.
- Re-verified backend build and backend tests, and re-verified the frontend production build after the Sprint 34 implementation.

## Sprint 35 - Transport diff triage and route focus

Status: Completed

Goal:
Turn the latest-vs-previous transport diff into a faster operator investigation tool by letting the user isolate relevant deltas and jump directly from diff evidence back into route detail.

Implemented notes:

- Added diff-triage controls in the transport board so operators can filter route deltas by changed, added, removed, selected-route, or all evidence.
- Added selected-route diff context so the board explains what changed for the currently focused route even when broader history is noisy.
- Added direct `Focus route` actions from diff evidence back into the route board, which shortens the loop between sync analysis and route inspection.
- Kept the increment frontend-local so the existing backend diff contract and workflow history remain stable.
- Re-verified the frontend production build after the Sprint 35 implementation.

## Sprint 36 - Transport sync freshness and escalation cues

Status: Completed

Goal:
Make stale or aging transport imports more explicit by surfacing stronger freshness posture and escalation cues before operators trust an outdated snapshot.

Implemented notes:

- Added a live freshness indicator to the transport sync card so the latest persisted snapshot reads as fresh, aging, stale, or expired with an approximate age.
- Added escalation cues that tell the operator when the transport snapshot should no longer be trusted at face value and when a refresh is worth doing before decision-making.
- Kept the implementation frontend-local in `frontend/web/src/App.vue`, building on the existing sync, support, and diff surfaces instead of widening the backend contract.
- Updated development and smoke-test documentation so the freshness posture and escalation states are explicitly verifiable.
- Re-verified the frontend production build after the Sprint 36 implementation.

## Sprint 37 - Transport freshness drill-through

Status: Completed

Goal:
Let operators move from a stale freshness warning into the exact sync, route, and diff evidence that explains why the snapshot is aging or expired.

Implemented notes:

- Added a dedicated transport freshness drill-through card in the frontend that links the freshness warning back to the last import, sync posture, latest comparable diff, selected-route evidence, and latest sync note.
- Added focused support actions so operators can refresh or import transport state, jump to the selected diff, or return to the current route from the freshness warning.
- Kept the increment frontend-local in `frontend/web/src/App.vue`, reusing the existing transport sync, diff, and route-detail workflows instead of widening the backend contract.
- Re-verified the frontend production build after the Sprint 37 implementation.

## Sprint 38 - Transport freshness lineage and causality

Status: Completed

Goal:
Expand the freshness story beyond the direct drill-through so operators can read how transport state evolved across more than one import and understand the root cause of staleness at a glance.

Deliverables:

- Freshness lineage summary in the transport board.
- Causality evidence that relates the last success, last attempt, and imported snapshot age.
- UI copy that distinguishes source posture from freshness posture.
- Frontend build verification.

Implemented notes:

- Added a freshness lineage card in the transport board that traces the source posture, last successful sync, last attempted sync, imported snapshot age, and a plain-language causality summary.
- Exported the UTC formatting helper from `frontend/web/src/data/controlTower.ts` so the lineage card can display the sync timestamps cleanly.
- Re-verified the frontend production build after the Sprint 38 implementation.

## Sprint 39 - Transport sync cadence summary

Status: Completed

Goal:
Expose the pace of transport syncing so operators can see whether freshness is decaying because imports are happening too slowly or because recent attempts are failing.

Deliverables:

- Sync cadence summary derived from the last successful and attempted sync timestamps.
- UI surface that explains the observed cadence in plain language.
- Frontend build verification.

Implemented notes:

- Added a sync cadence card in the transport board that classifies the rhythm as steady, slow, retry-heavy, sparse, or unknown based on the last successful and attempted sync timestamps.
- Kept the cadence calculation frontend-local in `frontend/web/src/App.vue` and reused the existing sync status model.
- Re-verified the frontend production build after the Sprint 39 implementation.

## Sprint 40 - Transport freshness trust badge

Status: Completed

Goal:
Add a compact trust indicator that tells operators how much confidence to place in the current transport snapshot before they read the route board.

Deliverables:

- Trust badge or trust summary tied to freshness posture and sync health.
- UI copy that explains why the trust level is high, medium, or low.
- Frontend build verification.

Implemented notes:

- Added a trust badge card in the transport board that combines freshness posture, sync health, and cadence posture into a compact confidence signal.
- Kept the trust calculation frontend-local in `frontend/web/src/App.vue` and reused the existing freshness and cadence summaries.
- Re-verified the frontend production build after the Sprint 40 implementation.

## Sprint 41 - Transport route freshness spotlight

Status: Completed

Goal:
Make the selected route's freshness relationship visible so operators can instantly see whether the chosen route is present in the latest imported snapshot or drifting outside it.

Deliverables:

- Selected-route freshness spotlight in the transport surface.
- Clear relationship text between the chosen route and the latest import.
- Frontend build verification.

Implemented notes:

- Added a selected-route spotlight card in the transport board that shows whether the current route is present in the latest imported snapshot and offers direct jump-back actions.
- Kept the spotlight calculation frontend-local in `frontend/web/src/App.vue` and reused the existing imported-route references.
- Re-verified the frontend production build after the Sprint 41 implementation.

## Sprint 42 - Transport freshness operator checklist

Status: Completed

Goal:
Turn the freshness story into a compact operator checklist so the next action is obvious when the transport snapshot is aging, stale, or expired.

Deliverables:

- Small action checklist for stale, aging, and expired transport states.
- Recommendation text tied to the current sync posture.
- Smoke-test and development doc updates where needed.
- Frontend build verification.

Implemented notes:

- Added an operator checklist card in the transport board that recommends the next action when the snapshot is fresh, aging, stale, or expired.
- Reused the existing freshness, cadence, trust, and route-spotlight calculations so the checklist is aligned with the current transport posture.
- Re-verified the frontend production build after the Sprint 42 implementation.

## Sprint 43 - Transport freshness visual QA and support-doc wrap-up

Status: Completed

Goal:
Recheck the transport freshness cluster in the browser, tighten any remaining layout issues, and align the operator documentation with the new five-card freshness story.

Deliverables:

- Browser-facing visual QA for the freshness cluster.
- Any final responsive or spacing fixes required by the new cards.
- Development and smoke-test documentation updates for the freshness story.
- Frontend build verification.

Implemented notes:

- Verified the transport freshness cluster in Chrome so the drill-through, lineage, cadence, trust, spotlight, and checklist cards remain readable together.
- Updated transport-facing documentation so the five-card freshness story is now described consistently in development and smoke-test guidance.
- Re-verified the frontend production build after the Sprint 43 implementation.

## Consolidated Sprint Block 44-72 - Transport freshness UX hardening

Status: Completed

Goal:
Consolidate the many small transport-freshness adjustments into one coherent body of UX hardening work instead of continuing to count them as independent product-capability sprints.

Deliverables:

- Mobile readability, spacing, and touch-target improvements for the transport freshness cluster.
- Accessibility, focus, action-label, and card-semantics hardening.
- Stronger interaction microstates, disabled-state logic, and compact posture messaging.
- A denser operator reading flow with timeline, digest, quick links, primary concern, and clearer wording.
- Documentation and smoke-test alignment for the final transport freshness operator experience.

Implemented notes:

- Consolidated the former Sprint 44 through Sprint 72 sequence into one roadmap block because it was mostly iterative UX hardening inside an already-existing transport freshness feature.
- Kept the product value of that work: mobile layout polish, accessibility semantics, touch-target baseline, disabled-action safety, shared timeline and digest, quick links, primary concern, urgency labels, and refined operator wording.
- Established a governance rule for future execution: a sprint does not count if it only delivers local polish in one UI file without adding a meaningful cross-layer or cross-surface capability.

## Sprint 73 - Structured transport sync history

Status: Completed

Goal:
Turn recent transport imports into a first-class cross-layer product surface instead of relying on generic operational activity entries and local `App.vue` storytelling only.

Deliverables:

- Dedicated transport sync history contracts in `Orkystra.Contracts`.
- A backend service path that computes recent structured import history with route-change counts versus the previous import.
- A protected `GET /api/transport/sync-history` endpoint with persisted projection output.
- Backend tests that verify structured history and route-change classification.
- Frontend transport sync history service, mapping layer, fallback state, and UI wiring.
- Roadmap and methodology updates that normalize the micro-sprint period and enforce the new sprint-governance rule.

Implemented notes:

- Added `TransportSyncHistoryReadModel` and `TransportSyncHistoryEntryReadModel` so the history view has an explicit contract instead of being inferred from generic workflow runs.
- Extended `TransportSyncHistoryService` to build recent import history entries with added, removed, and changed route counts relative to the previous snapshot.
- Added `GET /api/transport/sync-history` and persisted the resulting history projection for tenant reuse.
- Added backend test coverage that proves the latest import can be compared to the previous one with structured counts.
- Added frontend history mapping, API loading, fallback behavior, and a dedicated recent-imports card that now shows imported-route volume, diff counts, preview references, and import timestamps.

## Sprint 74 - Transport exception workbench

Status: Completed

Goal:
Move from freshness interpretation into operator action by giving the transport slice a focused exception workbench for routes that are delayed, drifted, or newly introduced by the latest import.

Deliverables:

- A cross-surface shortlist of transport exceptions derived from route state, import diff state, and sync posture.
- A compact API-backed exception model instead of UI-only derivation where needed.
- Operator actions that jump from an exception into route detail, import evidence, and optimization review.
- Verification for backend, frontend, and browser behavior.

Implemented notes:

- Added `TransportExceptionWorkbenchReadModel` plus item contracts so transport exceptions are now a first-class product surface.
- Added a backend workbench service and protected `GET /api/transport/exceptions-workbench` endpoint that prioritize sync posture issues, import deltas, and delayed or drifted routes.
- Added backend test coverage for prioritized exception generation across comparable imports and route health states.
- Added frontend exception workbench loading, fallback behavior, and direct action wiring so operators can jump into route focus, diff review, sync refresh, optimization rerun, or import history from one shortlist.

## Sprint 75 - Transport exception batch actions

Status: Completed

Goal:
Turn the new transport exception workbench into a faster operator surface by adding grouped exception actions and clearer completion loops for multi-route review.

Deliverables:

- A compact grouped view or filtering mode for sync, diff, and route exceptions.
- Batch-friendly operator actions for reviewing several exception routes in sequence.
- Verification for backend, frontend, and browser behavior.

Implemented notes:

- Extended the transport exception workbench contract with grouped exception summaries so the API now exposes filter-ready exception families.
- Added grouped filtering plus local review-queue tracking in the frontend so operators can isolate `Unreviewed`, `Route`, `Sync`, `Import Delta`, and other active groups quickly.
- Added `Review next exception` and `Reset review queue` actions so multi-route support passes can move through several exceptions in sequence without losing context.
- Re-verified backend tests and frontend production build after the grouped workbench and batch-action pass.

## Sprint 76 - Transport exception resolution ledger

Status: Completed

Goal:
Make the exception review loop auditable by persisting lightweight operator resolution notes and completion posture for reviewed transport exceptions.

Deliverables:

- A small persistence shape for reviewed or resolved transport exceptions.
- An API path to save and reload resolution posture for the current tenant.
- Frontend controls to mark an exception resolved with a short note.
- Verification for backend, frontend, and browser behavior.

Implemented notes:

- Added a dedicated transport exception resolution ledger with persisted entries for exception id, status, note, and updated timestamp.
- Added protected `GET /api/transport/exceptions-workbench/resolutions` and `PUT /api/transport/exceptions-workbench/resolutions` endpoints so tenant-local review posture can be reloaded and updated cleanly.
- Extended the exception workbench so exception items now carry resolution status, note, and timestamp directly in the main operator surface.
- Added frontend controls to save short review notes with `Reviewed` or `Resolved` posture without leaving the transport board.
- Re-verified backend tests and frontend production build after the resolution-ledger pass.

## Sprint 77 - Transport exception resolution filters

Status: Completed

Goal:
Make persisted resolution posture easier to operate by adding fast filtering between open, reviewed, resolved, and deferred transport exceptions.

Deliverables:

- Resolution-aware exception filters in the transport workbench.
- A clearer distinction between active and closed exception posture.
- Verification for backend, frontend, and browser behavior.

Implemented notes:

- Added a first-class resolution-state filter row to the exception workbench so operators can jump between `Open`, `Reviewed`, `Resolved`, `Deferred`, and `All` without losing the existing exception-family grouping.
- Reworked the workbench metrics to distinguish active open exceptions from reviewed and closed posture, while still surfacing deferred follow-up separately.
- Extended each exception row with inline resolution notes plus direct `Save review`, `Defer`, and `Resolve` actions so the persisted ledger is now operable from the main shortlist.
- Re-verified the sprint with backend tests, frontend production build, and a browser check of the updated workbench controls.

## Sprint 78 - Transport exception resolution history

Status: Completed

Goal:
Make exception closure auditable over time by exposing recent resolution-history entries and note chronology instead of only the latest saved posture.

Deliverables:

- A recent history view for persisted transport exception resolutions.
- A clearer distinction between the latest current posture and previous operator updates.
- Verification for backend, frontend, and browser behavior.

Implemented notes:

- Added append-only persisted resolution history so repeated saves for the same exception now preserve chronology instead of overwriting the earlier operator update trail.
- Added `GET /api/transport/exceptions-workbench/resolution-history` so the transport surface can load recent exception resolution events independently from the latest active ledger posture.
- Added a dedicated frontend resolution-history card that separates the focused exception's current posture from previous updates and broader recent activity.
- Re-verified the sprint with backend tests, frontend production build, and browser verification on a fresh local Vite instance.

## Sprint 79 - Transport exception follow-up queue

Status: Completed

Goal:
Turn deferred resolution outcomes into an explicit follow-up queue so transport exceptions can move from review into tracked pending commitments.

Deliverables:

- A follow-up queue derived from deferred exception outcomes and their latest notes.
- A clearer distinction between closed exceptions and deferred exceptions that still need operator return work.
- Verification for backend, frontend, and browser behavior.

Implemented notes:

- Added a backend follow-up queue derived from the latest deferred resolution posture, the active exception workbench, and recent resolution history.
- Added `GET /api/transport/exceptions-workbench/follow-up-queue` so the transport board can load deferred return work independently from the main exception shortlist.
- Added a dedicated frontend follow-up queue card that separates `Still active` deferred items from `Watchlist` items and keeps their latest note, update count, and action path visible.
- Re-verified the sprint with backend tests, frontend production build, and browser verification on a fresh local Vite instance.

## Sprint 80 - Transport exception follow-up commitments

Status: Completed

Goal:
Turn deferred follow-up items into explicit commitments by adding ownership and a target return window for each pending transport exception.

Deliverables:

- A lightweight commitment shape for follow-up owner and target return window.
- A clearer distinction between passive deferred notes and active follow-up commitments.
- Verification for backend, frontend, and browser behavior.

Implemented notes:

- Extended the persisted exception-resolution model so deferred items can now carry a follow-up owner and a target return window without creating a separate write path.
- Surfaced those commitment fields in the exception workbench, the resolution history, and the follow-up queue so the operator can see both the note and the planned return commitment.
- Added direct commitment editing and saving in the workbench and follow-up queue by reusing the existing deferred-resolution save flow.
- Re-verified the sprint with backend tests, frontend production build, and browser verification on a fresh local Vite instance.

## Sprint 81 - Transport exception commitment alerts

Status: Completed

Goal:
Prevent deferred transport work from disappearing into passive backlog by flagging ownerless or overdue follow-up commitments directly in the operator transport surface.

Deliverables:

- A compact alerting posture for missing owners and overdue target return windows.
- A clearer distinction between healthy deferred commitments and ones that now need escalation.
- Verification for backend, frontend, and browser behavior.

Implemented notes:

- Added queue-level commitment alert posture in the backend so deferred follow-up now reports overdue, ownerless, and healthy counts instead of only raw queue size.
- Added per-item alert severity and alert summary to the follow-up queue so the riskiest deferred commitments can be recognized without opening each note individually.
- Added frontend alert visibility in the follow-up queue with compact alert metrics and explicit item-level wording for overdue and ownerless commitments.
- Re-verified the sprint with backend tests, frontend production build, and browser verification on a fresh local Vite instance.

## Sprint 82 - Transport exception commitment sorting and focus

Status: Completed

Goal:
Make the follow-up queue more operational by automatically prioritizing risky commitments and surfacing the next best escalation target first.

Deliverables:

- Automatic sorting that pushes overdue and ownerless commitments ahead of healthy deferred items.
- A clearer focused escalation target in the follow-up queue itself.
- Verification for backend, frontend, and browser behavior.

Implemented notes:

- Added backend sorting so the follow-up queue now prioritizes overdue commitments first, then ownerless commitments, then healthier deferred work.
- Added a queue-level focus target that calls out the next best escalation pass instead of leaving operators to infer priority from the raw list order.
- Added frontend focus visibility directly in the follow-up queue above the metrics so the operator can jump into the riskiest remaining commitment immediately.
- Re-verified the sprint with backend tests, frontend production build, and browser verification on a fresh local Vite instance.

## Sprint 83 - Transport exception follow-up closure workflow

Status: Completed

Goal:
Make deferred follow-up work lifecycle-safe by letting operators close, reopen, and explicitly retire follow-up commitments without losing their history.

Deliverables:

- An explicit closure and reopen workflow for deferred follow-up commitments.
- A clearer distinction between active follow-up, retired follow-up, and unresolved exception posture.
- Verification for backend, frontend, and browser behavior.

Implemented notes:

- Added an explicit backend lifecycle for deferred follow-up commitments so operators can retire or reopen them without overwriting the underlying exception resolution history.
- Preserved follow-up note, owner, target return window, and append-only history entries across retire and reopen transitions.
- Added frontend lifecycle visibility and dedicated follow-up transition actions in the queue so active and retired commitments are easy to distinguish in-product.
- Re-verified the sprint with backend tests, frontend production build, and browser verification on a fresh local Vite instance.

## Sprint 84 - Transport exception follow-up SLA posture

Status: Completed

Goal:
Turn raw target return windows into clearer SLA-style posture bands so deferred commitments can be read as healthy, at risk, or overdue before they slip into silent escalation.

Deliverables:

- SLA-style posture bands for follow-up commitments around their target return window.
- A clearer distinction between healthy, at-risk, overdue, and retired follow-up lifecycle states.
- Verification for backend, frontend, and browser behavior.

Implemented notes:

- Added backend SLA posture shaping so every deferred follow-up item now reports `Healthy`, `At Risk`, `Overdue`, or `Retired` based on lifecycle state and target return timing.
- Added queue-level `AtRiskCount` plus per-item countdown data so the operator can see when a commitment is drifting before it becomes overdue.
- Surfaced the new posture directly in the frontend follow-up queue with posture wording, countdown labeling, and stronger focus text.
- Re-verified the sprint with backend tests, frontend production build, and browser verification on the local Vite surface.

## Sprint 85 - Transport follow-up queue lane filters

Status: Completed

Goal:
Make the follow-up queue operable as a real triage surface by letting operators switch quickly between the most important deferred-work lanes.

Deliverables:

- Lane filters for all, active, at-risk, overdue, ownerless, retired, and watchlist follow-up views.
- A clearer filtered queue that keeps the operator inside the current lane instead of forcing a full-list scan.
- Verification for frontend behavior and build stability.

Implemented notes:

- Added frontend follow-up lane filters for `All`, `Active`, `At risk`, `Overdue`, `Ownerless`, `Retired`, and `Watchlist`.
- Rewired the rendered queue list so all follow-up actions now operate inside the selected lane instead of only on the raw full queue.
- Kept queue focus, editing, and transition actions compatible with the filtered lane flow.
- Re-verified the sprint with frontend production build and browser verification on the local Vite surface.

## Sprint 86 - Transport follow-up history spotlight

Status: Completed

Goal:
Tighten deferred-work recovery by giving the operator a focused spotlight on one follow-up item with its latest history, note trail, and return timing.

Deliverables:

- A focused follow-up spotlight panel tied to the selected or highest-priority deferred item.
- Recent saved history visibility for the focused follow-up item.
- Verification for frontend behavior and build stability.

Implemented notes:

- Added a focused follow-up spotlight panel that highlights the selected deferred item, its SLA posture, current note, owner, and return countdown.
- Reused persisted resolution history so the spotlight now shows the latest saved update plus recent prior updates for the same exception.
- Added an explicit `Focus follow-up` action in the queue so operators can pin the history view on the item they are actively handling.
- Re-verified the sprint with frontend production build and browser verification on the local Vite surface.

## Sprint 87 - Transport follow-up ownership summary

Status: Completed

Goal:
Expose follow-up workload distribution so the team can see whether deferred return work is concentrated on one owner, scattered, or still unassigned.

Deliverables:

- Queue-level owner summary records in the backend contract.
- A frontend summary lane for top owners and the unassigned pool.
- Verification for backend, tests, and frontend behavior.

Implemented notes:

- Added backend owner-summary shaping so the queue now returns grouped follow-up counts by owner, including unassigned work.
- Included overdue, at-risk, active, and retired counts in each owner summary so the operator can read workload quality, not only volume.
- Added a frontend owner summary strip that surfaces the highest-pressure ownership lanes directly above the queue.
- Re-verified the sprint with backend tests, frontend production build, and browser verification on the local Vite surface.

## Sprint 88 - Transport follow-up escalation digest

Status: Completed

Goal:
Summarize deferred-work pressure into one compact escalation digest so the operator can read the queue's overall urgency before drilling into individual items.

Deliverables:

- A backend escalation digest for overdue, at-risk, ownerless, near-due, and retired posture.
- A frontend digest summary that reads clearly at a glance.
- Verification for backend, tests, and frontend behavior.

Implemented notes:

- Added a backend escalation digest read model with overdue, at-risk, ownerless, due-within-24-hours, due-within-72-hours, and retired counts.
- Added digest summary wording that changes with the riskiest current follow-up posture instead of repeating raw queue totals.
- Surfaced that digest summary in the frontend follow-up queue so escalation posture is readable before scanning the item list.
- Re-verified the sprint with backend tests, frontend production build, and browser verification on the local Vite surface.

## Sprint 89 - Transport follow-up handoff pack

Status: Completed

Goal:
Turn the deferred follow-up queue into a cleaner shift-handoff surface by packaging the riskiest active items into a compact handoff view with owner, deadline, latest note, and route context.

Deliverables:

- A handoff-ready shortlist derived from the highest-priority active follow-up items.
- A clearer distinction between what needs immediate action this shift and what can wait.
- Verification for backend, frontend, and browser behavior.

Implemented notes:

- Added a backend handoff pack derived from the active deferred follow-up queue so the next shift can see a compact shortlist without re-reading the whole queue.
- Added a dedicated frontend `Shift handoff` card that surfaces that shortlist above the broader follow-up queue.
- Re-verified the sprint with backend tests, frontend production build, and browser verification on the local Vite surface.

## Sprint 90 - Transport handoff readiness posture

Status: Completed

Goal:
Make each handoff item operationally trustworthy by showing whether the next shift has enough context to take over without guesswork.

Deliverables:

- Readiness posture per handoff item.
- Clear distinction between missing owner, missing note, missing route, missing target, and ready posture.
- Verification for backend, frontend, and browser behavior.

Implemented notes:

- Added backend readiness posture and readiness summary shaping for each handoff item.
- Added frontend readiness wording so each shortlist item now explains what is missing before the handoff is truly ready.
- Re-verified the sprint with backend tests and frontend production build.

## Sprint 91 - Transport handoff shift windows

Status: Completed

Goal:
Separate urgent follow-up from near-future follow-up by exposing whether active deferred work lands in the current shift, the next shift, or later.

Deliverables:

- Immediate, this-shift, and next-shift counts for active handoff work.
- Clear shift-summary wording for the handoff pack.
- Verification for backend, frontend, and browser behavior.

Implemented notes:

- Added backend shift-window shaping with immediate, current-shift, and next-shift counts.
- Added frontend handoff metrics so timing pressure can be read before drilling into each item.
- Re-verified the sprint with backend tests, frontend production build, and browser verification on the local Vite surface.

## Sprint 92 - Transport handoff owner lane headline

Status: Completed

Goal:
Show who is carrying the active deferred workload at handoff time so the next shift can see whether pressure is concentrated on one owner or stuck in an unassigned pool.

Deliverables:

- Owner-lane headline for the handoff pack.
- Clear distinction between owned and unassigned active deferred work.
- Verification for backend, frontend, and browser behavior.

Implemented notes:

- Added backend owner-lane headline shaping from the active owner summary set.
- Surfaced that owner headline in the frontend handoff card so operator load concentration is visible at a glance.
- Re-verified the sprint with backend tests and frontend production build.

## Sprint 93 - Transport handoff briefing lines

Status: Completed

Goal:
Give the team a concise, reusable text summary for shift transition so deferred work can be handed over without reconstructing the queue verbally.

Deliverables:

- Briefing lines for the top active deferred handoff items.
- A frontend surface that shows those short lines clearly.
- Verification for backend, frontend, and browser behavior.

Implemented notes:

- Added backend briefing-line generation for the highest-priority active deferred handoff items.
- Added a frontend briefing-lines section that exposes compact reusable handoff text for the next operator pass.
- Re-verified the sprint with backend tests, frontend production build, and browser verification on the local Vite surface.

## Sprint 94 - Transport follow-up handoff acknowledgements

Status: Completed

Goal:
Close the handoff loop by letting the next shift acknowledge that a follow-up item was explicitly received, without resolving or retiring it yet.

Deliverables:

- Lightweight acknowledgement posture for active handoff items.
- A clearer distinction between handed over, acknowledged, and still-unclaimed deferred work.
- Verification for backend, frontend, and browser behavior.

Implemented notes:

- Added acknowledgement-aware transport follow-up contracts and ledger updates so active deferred handoff items can record explicit acknowledgement without changing resolution state.
- Added an `acknowledge` transition in the follow-up queue workflow and surfaced acknowledgement posture in the handoff pack and follow-up queue UI.
- Added frontend acknowledgement actions and visible acknowledgement status text for both the handoff pack and deferred follow-up list.
- Re-verified the backend build, backend test suite, and frontend production build after the acknowledgement workflow changes.

## Sprint 95 - Transport handoff acknowledgement metrics

Status: Completed

Goal:
Make acknowledgement posture readable at a glance inside the handoff pack so the next shift can see how much of the deferred load is still waiting for explicit receipt.

Deliverables:

- Handoff-level acknowledged versus unacknowledged counts.
- Clear acknowledgement posture wording in the handoff pack.
- Verification for backend, frontend, and build stability.

Implemented notes:

- Added backend shaping for `AcknowledgedCount` and `UnacknowledgedCount` in the transport follow-up handoff pack.
- Surfaced those counters in the frontend `Shift handoff` card so explicit receipt posture is readable before drilling into item rows.
- Re-verified the backend test suite and frontend production build after the acknowledgement metrics pass.

## Sprint 96 - Transport handoff acknowledgement detail

Status: Completed

Goal:
Make acknowledged handoff items auditable by showing who acknowledged them and when, instead of exposing only a generic acknowledged state.

Deliverables:

- Acknowledged-by and acknowledged-at handoff item detail.
- Consistent acknowledgement detail in the deferred follow-up queue.
- Verification for backend, frontend, and build stability.

Implemented notes:

- Added backend and contract support for `AcknowledgedBy`, `AcknowledgedAtUtc`, and `NeedsAcknowledgement` on handoff items.
- Surfaced acknowledgement actor and timestamp detail in both the handoff pack and deferred follow-up queue UI.
- Extended backend tests to verify acknowledged follow-up items preserve acknowledgement identity and timestamp data.

## Sprint 97 - Transport handoff acknowledgement focus

Status: Completed

Goal:
Guide the next shift toward the most important unreceived handoff item by calling out the best next acknowledgement target directly in the handoff card.

Deliverables:

- A backend-selected acknowledgement focus target.
- A frontend focus action that jumps into the selected deferred item.
- Verification for backend, frontend, and build stability.

Implemented notes:

- Added backend prioritization for the next acknowledgement focus target using acknowledgement posture, SLA pressure, and target-return timing.
- Added acknowledgement focus summary text plus a direct focus action in the frontend `Shift handoff` card.
- Re-verified the backend test suite and frontend production build after the focus-target pass.

## Sprint 98 - Transport handoff acknowledgement lanes

Status: Completed

Goal:
Make the handoff shortlist easier to operate by letting the next shift filter active deferred handoff items by acknowledgement and timing lanes.

Deliverables:

- Handoff lane filters for all, awaiting acknowledgement, acknowledged, immediate, and ready items.
- Filter-aware handoff list rendering that keeps actions inside the selected lane.
- Verification for frontend and build stability.

Implemented notes:

- Added frontend handoff lane filters for `All`, `Awaiting ack`, `Acknowledged`, `Immediate`, and `Ready`.
- Rewired the visible handoff list so acknowledgement actions and focus flow operate inside the selected handoff lane.
- Re-verified the frontend production build after the handoff-lane pass.

## Sprint 99 - Transport acknowledgement chronology visibility

Status: Completed

Goal:
Close the acknowledgement audit loop by making follow-up lifecycle, owner, return window, and acknowledgement details visible in the resolution-history trail itself.

Deliverables:

- Richer resolution-history lines for follow-up and acknowledgement detail.
- Clear acknowledgement chronology in the focused and recent history trail.
- Verification for frontend, backend tests, and build stability.

Implemented notes:

- Extended the frontend resolution-history card so current, previous, and recent exception updates now show follow-up lifecycle, owner, return window, acknowledgement status, acknowledgement actor, and acknowledgement timestamp.
- Normalized interpunct rendering in the affected transport handoff and follow-up UI strings.
- Re-verified the backend test suite and frontend production build after the chronology-visibility pass.

## Sprint 100 - GPS fleet board foundation

Status: Completed

Goal:
Turn the existing MQTT-backed GPS projection into a real operator-facing product slice instead of leaving it as a backend-only capability.

Deliverables:

- A dedicated GPS fleet board API read model.
- A frontend GPS fleet surface inside the transport board.
- Verification for backend, frontend, and build stability.

Implemented notes:

- Added `GpsFleetBoardReadModel` and `GpsFleetPositionReadModel` plus a dedicated `GET /api/gps/board` endpoint.
- Added a frontend GPS fleet board with local fallback data and API-backed loading.
- Re-verified the backend test suite and frontend production build after the fleet-board foundation pass.

## Sprint 101 - GPS freshness posture

Status: Completed

Goal:
Make telemetry age operationally legible by surfacing fresh, aging, and stale posture directly in the GPS fleet board.

Deliverables:

- Freshness posture shaping in the backend.
- Frontend visibility for fresh-versus-stale GPS readings.
- Verification for backend, frontend, and build stability.

Implemented notes:

- Added backend freshness shaping, age-in-minutes data, and summary wording for fresh, aging, and stale readings.
- Added frontend GPS metrics for projected, route-linked, stale, and speeding posture plus row-level freshness visibility.
- Re-verified the backend test suite and frontend production build after the GPS freshness pass.

## Sprint 102 - GPS route-linked telemetry focus

Status: Completed

Goal:
Correlate projected truck positions with active routes so the operator can compare live truck telemetry against planned transport execution.

Deliverables:

- Route-linked GPS telemetry correlation.
- A selected-route telemetry spotlight in the transport board.
- Verification for backend, frontend, and build stability.

Implemented notes:

- Added backend route correlation from truck reference to active route summary data in the GPS fleet board.
- Added a selected-route telemetry spotlight and GPS map markers that can jump back into the matching route context.
- Re-verified the backend test suite and frontend production build after the route-linking pass.

## Sprint 103 - GPS movement risk posture

Status: Completed

Goal:
Make telemetry behavior more actionable by surfacing movement and risk posture instead of only raw coordinates and speed.

Deliverables:

- Movement posture such as moving, idle, or stopped.
- Risk posture such as healthy, warning, or critical based on stale, speeding, or delayed-idle signals.
- Verification for backend, frontend, and build stability.

Implemented notes:

- Added backend movement posture, alert posture, alert summary, and focus-target prioritization for the GPS fleet board.
- Enriched the GPS provider demo data so the fleet board now includes fresh, aging, stale, moving, idle, and speeding examples across live route-linked trucks.
- Extended backend tests to validate fleet-board counts, route correlation, and the highest-priority telemetry focus target.

## Sprint 104 - GPS operator filters and publish workflow

Status: Completed

Goal:
Make the GPS fleet board operable in-product by letting operators publish fresh telemetry and filter the fleet view by the most useful lanes.

Deliverables:

- A `Publish GPS` support action tied to the existing publish workflow.
- Fleet-board lane filters for all, attention, stale, moving, and route-linked telemetry.
- Verification for backend, frontend, and build stability.

Implemented notes:

- Added a frontend `Publish GPS` action that triggers `/api/gps/positions/publish`, then refreshes the fleet board and operational trace.
- Added GPS lane filters for `All telemetry`, `Needs attention`, `Stale`, `Moving`, and `Route-linked`.
- Added GPS map overlays and telemetry list rendering that stay inside the existing transport board without replacing the route-planning surface.
- Re-verified the backend test suite and frontend production build after the GPS operator workflow pass.

## Open-Source Production Track

The next optimization target is no longer "more features in general". It is a narrower and more valuable goal:

- make the platform installable by a third party
- make core workflows durable and restart-safe
- make local self-hosting realistic
- make verification, docs, and release flow credible for open-source adopters

The recommended remaining path is now organized as a focused production-ready OSS track of 12 sprints.

## Sprint 105 - Postgres operational persistence

Status: Completed

Goal:
Replace the current SQLite-only operational persistence path with a Postgres-backed default suitable for multi-session self-hosted deployments.

Deliverables:

- Postgres operational persistence implementation.
- Configuration switch between local SQLite and Postgres.
- Migration/update notes for self-hosters.

Exit criteria:

- Core persisted projections and workflow runs survive restarts under a Postgres configuration.

Implemented notes:

- Added `IOperationalPersistenceStore` interface in `Orkystra.Api.Persistence` so the persistence path is DI-switchable.
- Renamed `OperationalPersistenceStore` to `SqliteOperationalPersistenceStore` (SQLite implementation of the interface).
- Added `PostgresOperationalPersistenceStore` using Npgsql 9.0.3 with auto-create tables for `projection_snapshots` and `workflow_runs`.
- Added `OperationalPersistenceOptions.Provider` ("sqlite" default / "postgres") and `ConnectionString` configuration.
- Updated `appsettings.json` with the operational persistence config section.
- Updated DI in Program.cs to register the correct store based on the Provider setting.
- Updated all service files (`TransportProjectionService`, `TransportSyncWorkflowService`, `TransportSyncHistoryService`, `TransportExceptionResolutionLedgerService`) to use `IOperationalPersistenceStore`.
- Updated all test files to use `SqliteOperationalPersistenceStore`.
- Build passes, 94 backend tests pass.
- Updated `docs/development.md` with Postgres configuration notes for self-hosters.

## Sprint 106 - Durable outbox, inbox, and replay recovery

Status: Completed

Goal:
Harden the event backbone so MQTT publish/consume posture is restart-safe, replayable, and no longer tied to in-memory inbox state.

Deliverables:

- Durable inbox state.
- Durable outbox records for key published workflows.
- Replay and recovery notes for operators.

Exit criteria:

- Duplicate-safe event projection and replay recovery still hold after process restarts.

Implemented notes:

- Replaced `InMemoryInboxStateStore` with `DurableInboxStateStore` in `Orkystra.Api.Eventing` that persists processed message IDs to the operational persistence database (SQLite or Postgres). The inbox table (`inbox_messages`) auto-creates with `(consumer_name, message_id)` primary key and survives process restarts.
- Added `EventOutboxStore` in `Orkystra.Api.Eventing` with `outbox_messages` table tracking `message_id`, `event_type`, `topic`, `payload_json`, `status` (pending/published/failed), timestamps, and error messages. Supports `RecordPendingAsync`, `MarkPublishedAsync`, `MarkFailedAsync`, `GetPendingEntriesAsync`, and `GetRecentEntriesAsync`.
- Added `OutboxEventPublisher` decorator that wraps `MqttEventPublisher` to record every published event in the outbox before publishing and update the status on success or failure.
- Added `GET /observability/event-backbone/outbox` to inspect recent outbox entries (protected endpoint).
- Added `POST /observability/event-backbone/replay` to replay pending/failed outbox entries through the MQTT publisher using the existing `MqttEnvelopeSerializer` for envelope reconstruction.
- Updated DI in Program.cs: `DurableInboxStateStore` registered as `IInboxStateStore`, `EventOutboxStore` as singleton, `OutboxEventPublisher` wrapping `MqttEventPublisher`.
- Added 10 backend tests (5 for `DurableInboxStateStore`, 5 for `EventOutboxStore`) covering persistence, idempotency, cross-consumer scoping, restart survival, pending/published/failed lifecycle, and reverse-chronological reads.
- Backend suite at 104 passing tests; frontend build passes.
- Updated `docs/development.md` with outbox and replay endpoint documentation.

## Sprint 107 - Self-hosted configuration and secret hardening

Status: Completed

Goal:
Make self-hosted configuration safer and clearer for open-source operators without introducing enterprise-only complexity.

Deliverables:

- `ValidateOnStart`-style manual validation on startup for `Security__ApiKey` (required, fail-fast with critical log) and `OperationalPersistence__ConnectionString` (required when provider is `postgres`).
- Startup diagnostic log line reporting effective configuration (API key presence, persistence provider, event backbone enabled state).
- `.env.example` file in the repository root documenting every configurable setting with the `__` separator convention.
- Comprehensive environment variable documentation section added to `docs/development.md` with a reference table of required/optional settings, provider secret naming convention (`ORKYSTRA_PROVIDER_*`), and PowerShell/Linux quick-start examples.
- All `Configure` calls consistently use `Bind` internally (not `GetSection` overload) so the `Microsoft.Extensions.Options.ConfigurationExtensions` package is not a runtime dependency.
- Build verified: API, domain, application, contracts, and test projects compile cleanly with 0 warnings.
- 104 backend tests pass.
- Frontend production build passes (Vite, 0 errors).

Exit criteria:

- A new self-hoster can configure the stack without guessing which values are required or accidentally committing secrets.
- Missing required settings cause a visible startup failure with a clear message instead of failing silently at first use.
- All settings are documented with their environment-variable names, defaults, and descriptions.

## Sprint 108 - Seeded demo bootstrap and install sanity checks

Status: Completed

Goal:
Make first-run setup feel reliable by giving self-hosters a deterministic seeded demo bootstrap and a repeatable sanity-check path.

Deliverables:

- `POST /api/bootstrap/demo` — protected endpoint that creates a deterministic seeded scenario, publishes events through the MQTT backbone, persists a bootstrap workflow record, and returns scenario/warehouse/route/GPS summary. Accepts optional `BootstrapDemoRequest` (name, seed, advanceMinutes, includeDisruption) with a `Default` preset (seed 42, 15 min advance, with disruption).
- `GET /health/sanity` — anonymous endpoint that runs component-level connectivity checks: API (always healthy), MQTT broker (connects and disconnects), persistence store (SQLite or Postgres read probe), and frontend (HTTP GET to configurable URL). Returns per-component status with duration and a top-level `AllHealthy` flag.
- `BootstrapService` — orchestrates demo bootstrap by calling the existing `ScenarioEventWorkflowService` to publish demo events and persist workflow records.
- `SanityCheckService` — runs isolated component checks with per-component duration tracking, graceful MQTT connect/disconnect, and frontend HTTP probe with 5s timeout.
- `SanityCheckResponse`, `SanityComponentStatus`, `BootstrapDemoRequest`, `BootstrapDemoResponse` contracts in `Orkystra.Contracts.Bootstrap`.
- `docs/development.md` updated with a Demo Bring-Up (First-Run) section showing the full one-step bootstrap flow.
- 4 new backend tests covering default request values, sanity response all-healthy/unhealthy states, and API check always-healthy behavior.
- Build: 0 warnings. Backend suite: 108 passing tests. Frontend build passes.

Exit criteria:

- A fresh clone can be brought up into a coherent seeded demo state with a short documented procedure.

## Sprint 109 - Integration test harness for core flows

Status: Completed

Goal:
Move beyond narrow unit-only confidence by adding automated integration coverage for the platform's main operational workflows.

Deliverables:

- Integration test project with WebApplicationFactory, SQLite config, stubbed MQTT/audit.
- Integration tests for health endpoints: liveness, readiness, sanity.
- Integration tests for persistence-backed API flows: control tower overview, warehouses, transport routes, provider catalog.
- Integration tests for transport sync: sync status, import, persisted workflow run.
- Integration test for GPS positions endpoint.
- Integration test for bootstrap demo endpoint.
- Registered direct singletons for EventBackboneOptions and OperationalPersistenceOptions so DI validation passes in test host.
- 12 integration tests passing; 108 existing unit tests still passing; 0 build warnings.

Exit criteria:

- Core multi-component flows can fail in CI before regressions reach users.

Implemented notes:

- Created `tests/backend/Orkystra.Integration.Tests` project with xUnit + `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory).
- Added `InternalsVisibleTo` in `Orkystra.Api.csproj` and `public partial class Program { }` in `Program.cs` so the test project can reference the API host.
- Added `OrkystraWebApplicationFactory` that overrides configuration with integration-test API key, SQLite persistence with unique temp path, disabled MQTT backbone, stubbed IEventBackbonePublisher, and in-memory IAuditStore (avoiding file-contention issues across parallel test classes).
- Added `IntegrationTestBase` abstract class with `IClassFixture<OrkystraWebApplicationFactory>`, common HTTP client setup (X-Api-Key + X-Tenant-Id headers), and JSON response helpers.
- Registered direct `EventBackboneOptions` and `OperationalPersistenceOptions` singletons in Program.cs so services that take concrete options (BootstrapService, SanityCheckService) resolve correctly during DI validation.
- 5 test classes with 12 total tests: `HealthEndpointsTests` (3), `PersistenceApiFlowsTests` (4), `TransportSyncTests` (3), `GpsEndpointsTests` (1), `BootstrapIntegrationTests` (1).
- Build: 0 warnings. Backend suite: 120 passing tests (108 existing + 12 new). Frontend build passes.

## Sprint 110 - Browser smoke automation for operator workflows

Status: Completed

Goal:
Turn critical UI slices into repeatable browser smoke checks instead of relying mainly on manual inspection.

Deliverables:

- Browser smoke automation for control-tower load.
- Browser smoke automation for transport sync and GPS board.
- Browser smoke automation for provider configuration and key operational actions.

Exit criteria:

- Major operator workflows can be verified automatically on a local or CI-ready stack.

## Sprint 111 - Warehouse exception and action surface

Status: Completed

Goal:
Bring the warehouse slice closer to the operational maturity already reached in transport by adding a first exception-and-action workflow.

Deliverables:

- Warehouse exception/readiness shortlist.
- Actionable warehouse operator cues.
- Cross-surface parity with the transport operational model.

Exit criteria:

- The warehouse slice has at least one real operational handling loop instead of only projection visibility.

Implemented notes:

- Added `WarehouseWorkbenchReadModel`, `WarehouseWorkbenchItemReadModel`, and `WarehouseWorkbenchGroupReadModel` contracts in `Orkystra.Contracts.Warehouse`.
- Added `WarehouseWorkbenchService` in `Orkystra.Api.ControlTower` that builds a prioritized exception shortlist from warehouse projection data — zone critical/watch status, zone pressure (>85% utilization), occupancy critical (>85%)/elevated (>70%), dock saturation (100% occupied), and dock pressure (≥75% occupied).
- Registered `WarehouseWorkbenchService` as a singleton and added `GET /api/warehouses/workbench` endpoint in `Program.cs`.
- Added frontend warehouse workbench view types, API types, fallback builder, and API-to-view mapping in `frontend/web/src/data/controlTower.ts`.
- Added `frontend/web/src/services/warehouseWorkbenchApi.ts` with `loadWarehouseWorkbench()` that calls the API endpoint and falls back to local mock data.
- Wired warehouse workbench into `App.vue`: imports, reactive state, `applyWarehouseWorkbenchResult`, `refreshWarehouseWorkbench`, integration in `refreshWorkspace`, and a template section rendering exception cards with severity, evidence, and focus-warehouse actions.
- Added 11 backend unit tests for `WarehouseWorkbenchService` covering zone items, occupancy items, ordering, grouping, item cap (12), summary, evidence, actions, and generated-at timestamp.
- Added 2 E2E smoke tests for warehouse workbench rendering (mocked API and fallback paths) in `frontend/web/e2e/warehouse-workbench.spec.ts`.
- Backend suite at 119 passing tests; frontend production build passes.

## Sprint 112 - Transport longitudinal history and archival navigation

Status: Completed

Goal:
Deepen the transport slice for real operations by making historical sync evidence and past state comparisons more navigable over time.

Deliverables:

- Multi-import navigation beyond only latest-vs-previous: extended `GET /api/transport/sync-diff` with optional `previousRunId` and `currentRunId` query params for arbitrary comparison.
- Archived sync evidence browsing: added `GET /api/transport/sync-history/{runId}` returning full import snapshot (routes, status, health, timestamps).
- Better historical drill-through for support workflows: added `ReadWorkflowRunByIdAsync` to persistence interface (SQLite + Postgres), `TransportSyncImportDetailReadModel` contract, `BuildImportDetailAsync` and `BuildDiffBetweenImportsAsync` service methods.
- Frontend import detail card with route-level table, comparison selector dropdowns, and click-to-drill-through from history entries.
- Backend: `TransportSyncImportDetailView`/`ApiTransportSyncImportDetail` types, mapping function, fallback builder, API service with 404 handling, and arbitrary comparison in diff API service.
- App.vue: import detail panel, comparison selector (two `<select>` dropdowns for picking any import pair), clear-comparison action, CSS for selected entries and select dropdowns.
- Added 4 backend unit tests (import detail positive/not-found, specific-import diff, nonexistent-run diff). Backend suite at 123 passing tests.

## Sprint 113 - AI provider abstraction and grounding hardening

Status: Completed

Goal:
Make the AI slice more open-source-ready by separating provider choice from workflow shape and tightening grounding behavior.

Deliverables:

- Created `IAiProvider` interface in `Orkystra.Api/AI/IAiProvider.cs` with single `BuildRecommendationAsync` method.
- Created `HttpAiProvider` — HTTP to Python AI service with local fallback on error.
- Created `LocalAiProvider` — fully deterministic, never calls external services.
- Created `DisabledAiProvider` — returns clear disabled message telling operators how to enable AI.
- Created `AiRecommendationBuilder` — shared static utility for deterministic fallback logic.
- Updated `AiEvidenceReadModel` with `Grounding` field (string: `"projection_data"`, `"assumption"`, `"heuristic"`, `"unknown"`).
- Updated `AiRecommendationEnvelope` with `ProviderName` field.
- Updated `AiServiceOptions` with `Provider` config switch (`"http"` default, `"local"`, `"disabled"`).
- Refactored `AiWorkflowService` to accept `IAiProvider` instead of `HttpClient`.
- Updated DI registration in `Program.cs` to resolve provider from config via singleton factory.
- Updated frontend types: `AiEvidenceView.grounding`, `AiRecommendationEnvelopeView.providerName`.
- Removed frontend-side fallback for backend error responses (network errors only).
- Updated `App.vue` template to surface `providerName` in route-detail-meta and `grounding` on each evidence item.
- Updated `.env.example` with `AiService__Provider` documentation.
- 2 new backend tests (local provider, disabled provider) + 2 existing tests refactored for provider pattern = 125 total passing backend tests.
- Frontend production build passes.
- Backend build passes with 0 warnings.

Exit criteria:

- Self-hosters can understand how to swap or disable AI providers without breaking the product shell.

Implemented notes:

- Created `IAiProvider` interface defining `BuildRecommendationAsync` for backend AI recommendation requests, enabling clean provider swapping.
- Added `HttpAiProvider` that calls the Python AI service via HTTP and falls back to `AiRecommendationBuilder` on error or unavailable service.
- Added `LocalAiProvider` that uses `AiRecommendationBuilder` only, with no external HTTP dependencies — fully deterministic.
- Added `DisabledAiProvider` that returns a clear disabled-ai message informing operators how to enable AI via the `AiService__Provider` config.
- Created `AiRecommendationBuilder` as a shared static utility that generates deterministic fallback recommendations from projection data, reused by both `HttpAiProvider` (fallback path) and `LocalAiProvider` (primary path).
- Extended `AiEvidenceReadModel` with a `Grounding` property to distinguish evidence sourced from projection data, assumptions, heuristics, or unknown origin.
- Extended `AiRecommendationEnvelope` with a `ProviderName` property so the frontend can surface which provider generated the recommendation.
- Extended `AiServiceOptions` with a `Provider` configuration switch supporting `"http"` (default), `"local"`, and `"disabled"` modes.
- Refactored `AiWorkflowService` to depend on `IAiProvider` instead of `HttpClient`, removing direct HTTP coupling from the workflow layer.
- Updated `Program.cs` DI registration with a singleton `IAiProvider` factory that reads the `AiService:Provider` config setting and resolves the correct provider at startup.
- Updated frontend `AiEvidenceView` and `AiRecommendationEnvelopeView` types with `grounding` and `providerName` fields.
- Removed the frontend-side recommendation fallback for backend error responses — the frontend now only falls back for network errors, relying on the backend provider chain for business-logic fallback.
- Updated `App.vue` to display `providerName` in the route-detail-meta area and `grounding` label on each evidence item.
- Updated `.env.example` with documentation for the `AiService__Provider` configuration setting.
- Added 2 new backend unit tests (`LocalAiProvider` generates deterministic response, `DisabledAiProvider` returns disabled message). Refactored 2 existing AI workflow tests to use the provider abstraction. Backend suite at 125 passing tests.

## Sprint 114 - Build hardening and testing support for local/disabled providers

Status: Completed

Goal:
Harden the AI provider abstraction by adding build-level verification and E2E coverage for provider configuration and UI interaction.

Deliverables:

- Build hardening and testing support for local/disabled providers.
- Add E2E tests for provider config UI.

Exit criteria:

- The local and disabled AI providers are verified by automated tests, and the provider config UI has browser-level smoke coverage.

Implemented notes:

- Added 13 new backend tests across `AiWorkflowTests.cs` and `AiRecommendationBuilderTests.cs`: 8 unit tests for `AiRecommendationBuilder` (intent classification keywords, warehouse/dispatcher/unknown response paths, empty warehouse/routes handling, null question); 5 edge-case tests for `HttpAiProvider` (null response body, malformed JSON) and `LocalAiProvider` (empty overview, ambiguous question); enhanced `DisabledAiProvider` test with full assertion coverage.
- Created `tests/backend/Orkystra.Domain.Tests/AiRecommendationBuilderTests.cs` as a dedicated test file for the static utility class, isolating builder logic from workflow orchestration.
- Added `frontend/web/e2e/ai-recommendation.spec.ts` with 4 E2E tests: provider name display (local provider), provider name display (http provider), grounding label on evidence items, and unreachable API fallback message.
- Backend suite at 138 passing tests; frontend production build passes.

## Sprint 115 - Open-source docs, contribution flow, and deployment guides

Status: Completed

Goal:
Package the project like a real open-source product, not just an evolving codebase.

Deliverables:

- Installation guide.
- Self-host deployment guide.
- Contributor guide and architecture entrypoints.
- Example environment templates and safer defaults.

Exit criteria:

- A new user can install, run, understand, and contribute without relying on chat history.

Implemented notes:

- Created `INSTALL.md` at repo root: prerequisites table, quick-start (clone, infrastructure, backend, frontend, demo bootstrap, Python services), self-host deployment (Docker Compose full stack, manual dotnet publish), full configuration reference (required vars, persistence, MQTT, AI, optimization, provider secrets), verification commands, and troubleshooting section.
- Created `CONTRIBUTING.md` at repo root: architecture entrypoints (key concepts, repository layout, context boundaries table), development workflow (sprint model, rules), code standards, testing commands, pull request flow, and links to key documents.
- Created `LICENSE` (MIT) at repo root.
- Updated `.env.example`: changed empty `Security__ApiKey=` to `change-me-to-a-unique-secret` placeholder, added `See INSTALL.md for a full configuration reference` footer.
- Updated `README.md`: added English intro section with quick-start and links to INSTALL.md/CONTRIBUTING.md, expanded Core Documents section, updated test count from 95+ to 138+.
- Backend suite at 138 passing tests; frontend production build passes (no code changes).

## Sprint 116 - Release candidate hardening and OSS release process

Status: Completed

Goal:
Close the current maturity gap by preparing a first credible open-source release candidate.

Deliverables:

- Final cross-slice hardening pass.
- Release checklist and versioning flow.
- Known limitations list and supported deployment posture.

Exit criteria:

- The project can be published as a serious OSS release candidate with explicit support boundaries.

Implemented notes:

- Added `docs/operations/release-candidate.md` with the final release checklist, semantic versioning flow, supported deployment posture, known limitations, and support boundaries.
- Linked the release-candidate guide from `README.md` and the smoke-test checklist so the release process is visible from the main docs entrypoints.
- Repeated the final release verification commands and aligned `PROJECT_STATUS.md` to the completed OSS release-candidate track.

## Sprint 117 - Release notes and changelog baseline

Status: Completed

Goal:
Turn the release-candidate process into concrete publishable artifacts instead of leaving it as checklist-only guidance.

Deliverables:

- Root changelog.
- Versioned release-notes document for the first release candidate.
- Documentation links from the main onboarding and release guides.
- Verification that the documented candidate still matches a passing local build posture.

Exit criteria:

- The repository contains a release-oriented changelog and a concrete `v0.1.0-rc.1` release-notes file.
- The main README and release docs point to those artifacts.
- The documented candidate posture is re-verified before publication.

Implemented notes:

- Added `CHANGELOG.md` at the repository root with a first `0.1.0-rc.1` entry covering platform scope, packaging posture, and verification commands.
- Added `docs/operations/releases/v0.1.0-rc.1.md` as the first versioned release-notes artifact with candidate summary, supported posture, verification results, and known limitations.
- Updated `README.md`, `INSTALL.md`, `docs/operations/release-candidate.md`, and `docs/operations/smoke-test-checklist.md` so the release artifacts are discoverable from the main documentation paths.
- Re-ran `dotnet test backend/Orkystra.slnx --configuration Release /p:UseSharedCompilation=false /nodeReuse:false`, `npm run build` in `frontend/web`, and `python -m compileall python-services` while preparing the release materials.

## Sprint 118 - Python service packaging and runtime verification

Status: Completed

Goal:
Close the self-host and CI gap in the Python slice by making the AI and optimization services installable through the documented editable workflow and verifying their runtime endpoints automatically.

Deliverables:

- Editable package configuration for both Python service packages.
- Runtime endpoint tests for the FastAPI apps.
- CI verification of installed-package imports plus Python tests.
- Documentation updates for the supported Python install and verification workflow.

Exit criteria:

- `pip install -e ".[dev]"` from `python-services/` exposes both `orkystra_ai_service` and `orkystra_optimization_service`.
- Python tests cover service runtime endpoints in addition to in-process business logic.
- CI verifies Python imports and `pytest`, not only `compileall`.

Implemented notes:

- Updated `python-services/pyproject.toml` to use `setuptools`, publish both nested `src/` packages, and expose a `dev` extra with test/runtime verification dependencies.
- Added `python-services/tests/test_ai_service_app.py` and `python-services/tests/test_optimization_service_app.py` covering health, capability/graph, and demo workflow endpoints through `FastAPI` `TestClient`.
- Updated `.github/workflows/ci.yml` to set up Python 3.13, install `python-services` with `.[dev]`, run an import smoke test, run `pytest`, and keep the compile check.
- Updated `README.md`, `INSTALL.md`, `docs/development.md`, and `docs/operations/smoke-test-checklist.md` to document the editable install workflow and Python verification commands.
- Verified `python -c "import orkystra_ai_service, orkystra_optimization_service"`, `python -m pytest python-services`, and `python -m compileall python-services`.

## Sprint 119 - Universal agent prompt visibility and release preflight

Status: Completed

Goal:
Make the project's continuation protocol explicit for Codex, Copilot, and other IDE agents while reducing release friction with one consistent preflight command.

Deliverables:

- Recommended universal AI agent prompt linked from the main documentation and autopilot entrypoint.
- Release preflight script that validates release-memory files and dirty-worktree posture before tagging.
- Release guide updated to include the preflight step in the candidate publication flow.

Exit criteria:

- A new agent can discover the recommended prompt immediately from the repository entrypoints.
- The release-candidate flow includes an executable preflight command instead of relying only on prose.
- The sprint is documented in the roadmap and project status.

Implemented notes:

- Updated `prompts/CODEX_AUTOPILOT.md` so the recommended entrypoint is now `prompts/ORKYSTRA_UNIVERSAL_AGENT_PROMPT_FR.md`, and removed the stale special-casing that was still anchored to Sprint 105 through Sprint 116.
- Updated `README.md` with a visible `AI Agent Continuation` section plus direct links to the universal prompt and Codex/Copilot autopilot prompt.
- Added `infrastructure/scripts/release-preflight.ps1` to validate required release files, candidate-version references, autopilot alignment, and clean-tree readiness before cutting a release tag.
- Updated `docs/operations/release-candidate.md` so the documented release flow now includes the preflight script ahead of the final checklist/tagging steps.

## Sprint 120 - Clean release publication flow and tag readiness

Status: Completed

Goal:
Turn the current release-candidate documentation into a repeatable publication flow with cleaner final verification and artifact handoff.

Deliverables:

- Final release publication checklist aligned to the exact `v0.1.0-rc.1` artifact set.
- Version/tag readiness review across docs, changelog, and release notes.
- Explicit handoff notes for the manual publication step.

Exit criteria:

- The release can be cut from a clean tree with no ambiguity about the last manual steps.

Implemented notes:

- Added `docs/operations/releases/v0.1.0-rc.1-manifest.json` as the exact release-artifact manifest for the current candidate, including verification commands and publication sequence.
- Added `docs/operations/releases/v0.1.0-rc.1-publish-checklist.md` as the explicit final manual handoff for tagging and publishing the release candidate.
- Extended `infrastructure/scripts/release-preflight.ps1` so it now validates the manifest, publish checklist, candidate-version alignment, and every artifact listed in the release manifest.
- Updated `docs/operations/release-candidate.md`, `docs/operations/releases/v0.1.0-rc.1.md`, and `README.md` so the release flow now points to the manifest and handoff checklist instead of relying only on prose.

## Sprint 121 - Self-host baseline packaging and one-command bring-up

Status: Completed

Goal:
Reduce OSS onboarding friction by tightening the self-host path around a clearer one-command or one-sequence local bring-up flow.

Deliverables:

- Improved self-host bootstrap path.
- Packaging and command-path cleanup for local evaluators.
- Documentation alignment for first-run bring-up.

Exit criteria:

- A third-party evaluator can bring up the supported stack with fewer manual cross-file jumps.

Implemented notes:

- Added `infrastructure/scripts/bring-up-selfhost.ps1` as a packaged first-run helper for self-host evaluation. It starts the Docker Compose stack, waits for `GET /health/sanity`, bootstraps deterministic demo data, and prints the resulting frontend/API entrypoints plus evaluation headers.
- Hardened the helper with an explicit Docker-engine availability check so the script now fails fast with a clear message when Docker Desktop or the Docker daemon is not running, instead of only timing out later on API health.
- Updated `README.md`, `INSTALL.md`, `docs/development.md`, and `docs/operations/smoke-test-checklist.md` so the one-command self-host bring-up path is visible from the main onboarding, development, and operational verification entrypoints.

## Sprint 122 - Postgres-first persistence hardening for serious self-hosting

Status: Completed

Goal:
Strengthen the non-demo deployment story by making the Postgres operational persistence path easier to verify and trust.

Deliverables:

- Sharper Postgres verification path.
- Documentation and configuration cleanup around persistence selection.
- Targeted runtime checks for the supported self-host profile.

Exit criteria:

- The recommended serious self-host posture is clearer and better verified than the local SQLite fallback path.

Implemented notes:

- Added `PersistenceDiagnosticsService` plus protected `GET /observability/persistence/provider` in the API so operators and self-hosters can verify the active persistence provider, posture (`local-default` vs `self-host-recommended`), safe connection target, and live health without exposing the Postgres password.
- Added backend unit coverage in `tests/backend/Orkystra.Domain.Tests/PersistenceDiagnosticsServiceTests.cs` for both SQLite and PostgreSQL diagnostics shaping, including redaction-safe Postgres connection reporting.
- Added integration coverage in `tests/backend/Orkystra.Integration.Tests/HealthEndpointsTests.cs` so the observability endpoint is now verified end to end in the local test host.
- Updated `INSTALL.md`, `docs/development.md`, and `docs/operations/smoke-test-checklist.md` so the serious self-host verification path now explicitly includes `/observability/persistence/provider` as the runtime trust check for Postgres-backed operational persistence.

## Sprint 123 - Demo/bootstrap repeatability and reset workflow

Status: Completed

Goal:
Make demo and evaluation sessions easier to reset, reseed, and reproduce without manual archaeology.

Deliverables:

- Repeatable demo reset guidance and support flow.
- Clearer bootstrap documentation and guardrails.
- Better separation between demo-state recovery and release-state verification.

Exit criteria:

- A maintainer can reliably rebuild the evaluation state without hidden local steps.

Implemented notes:

- Added `infrastructure/scripts/reset-demo-state.ps1` as a local reset helper for repeatable demo and evaluation rebuilds. By default it removes the SQLite operational persistence database and audit output, and it can optionally stop Compose services, drop Docker volumes, remove ignored local runtime/secrets files, and chain directly back into `bring-up-selfhost.ps1`.
- Hardened the reset helper for safe operator use with `SupportsShouldProcess`, so `-WhatIf` now previews file removals and rebootstrap actions without touching local state.
- Updated `README.md`, `INSTALL.md`, `docs/development.md`, and `docs/operations/smoke-test-checklist.md` so the reset-and-rebuild flow is visible next to the existing bring-up path instead of living only in chat history.

## Sprint 124 - Event backbone recovery automation

Status: Completed

Goal:
Close one of the current operational maturity gaps by reducing manual recovery work for the inbox/outbox event backbone.

Deliverables:

- Automated or guided recovery flow beyond manual replay only.
- Updated operational guidance and verification notes.
- Targeted tests for the recovery path.

Exit criteria:

- Event recovery is no longer documented as a purely manual operator action.

Implemented notes:

- Added `OutboxRecoveryService` plus `OutboxRecoveryWorker` so pending and failed outbox entries are now retried automatically in bounded background batches when the event backbone is enabled.
- Added new `EventBackbone` settings in `appsettings.json` and runtime docs: `AutoReplayEnabled`, `AutoReplayIntervalSeconds`, and `AutoReplayBatchSize`.
- Extended event-backbone telemetry with recovery counters and the last recovery timestamp, so `/observability/event-backbone` now makes automatic replay activity visible to operators.
- Kept `POST /observability/event-backbone/replay` as a manual operator override, but refactored it to use the same recovery path as the background worker.
- Fixed a latent outbox replay bug by storing the serialized MQTT envelope shape in outbox persistence instead of a replay-incompatible anonymous payload object.
- Added backend unit coverage for automatic/manual recovery behavior in `OutboxRecoveryServiceTests.cs`.

## Sprint 125 - Connector writeback safety and dry-run posture

Status: Completed

Goal:
Prepare the connector layer for more serious real-world use by introducing a safer posture for future writeback operations.

Deliverables:

- Dry-run or guarded writeback posture.
- Clear operator-facing safety wording.
- Documentation and test updates for connector risk boundaries.

Exit criteria:

- Future writeback flows have an explicit safety envelope instead of only read-oriented assumptions.

Implemented notes:

- Extended `ProviderConfigurationSummaryReadModel` so the provider catalog now exposes `writebackMode`, `writebackReadiness`, and `writebackSummary` in addition to auth posture and configuration readiness.
- Updated `ProviderRuntimeMetadata`, `ProviderRuntimeStore`, and `ProviderCatalogService` so the REST transport adapter now supports an explicit local `writebackMode` runtime field with a safe default of `dry-run`, while non-writeback providers are surfaced as `Read-only`.
- Added backend coverage in `ProviderCatalogTests`, `ProviderRuntimeStoreTests`, and `PersistenceApiFlowsTests` so the new writeback posture is verified through unit and integration paths.
- Updated the frontend provider catalog mapping, fallback data, E2E mock data, and the catalog card in `App.vue` so operators can see the new writeback posture directly in the product shell.
- Updated development and smoke-test documentation so the writeback safety envelope is visible as part of normal connector operations guidance.

## Sprint 126 - GPS and map-facing operator maturity

Status: Completed

Goal:
Raise the GPS slice from telemetry plumbing toward a more operator-usable visualization and correlation workflow.

Deliverables:

- Stronger operator-facing GPS board or map path.
- Better route/vehicle correlation visibility.
- Verification of the user-facing telemetry workflow.

Exit criteria:

- The GPS slice becomes a real operator aid, not only an integration proof point.

Implemented notes:

- Reconnected the backend GPS operator surface by registering `GpsFleetBoardService` in the API and adding protected `GET /api/gps/board`, which now persists the shaped fleet board as an operational projection snapshot.
- Added integration coverage in `PersistenceApiFlowsTests.cs` so the GPS board endpoint is verified end to end alongside the existing transport and provider catalog flows.
- Replaced the frontend GPS fallback-only mapping with a real `GpsFleetBoardReadModel` mapper in `frontend/web/src/data/controlTower.ts`, including normalized freshness, movement, alert posture, focus summary, and route-correlation fields.
- Upgraded the GPS E2E mock payload shape and added a dedicated operator-facing GPS surface in `App.vue` with connection-state visibility, publish-telemetry action, projected truck counts, focus summary, a simple truck map, and route-linked telemetry rows.
- Updated development and smoke-test documentation so `/api/gps/board` and the new fleet telemetry surface are part of the normal verification path.

## Sprint 127 - Observability export and support bundle

Status: Completed

Goal:
Improve maintainability for self-hosters by making it easier to extract the relevant health, workflow, and audit evidence when debugging.

Deliverables:

- Support/export bundle definition.
- Better observability handoff surface.
- Documentation for issue reproduction and evidence collection.

Exit criteria:

- A self-hoster can collect meaningful troubleshooting evidence without digging through the repo manually.

Implemented notes:

- Added `SupportBundleService` plus protected `GET /observability/support-bundle` so the API now exports one aggregated JSON support snapshot containing runtime context, request metrics, persistence diagnostics, event-backbone telemetry, recent projection snapshots, recent workflow runs, and recent audit entries.
- Added integration coverage in `HealthEndpointsTests.cs` so the new support bundle endpoint is verified end to end in the test host.
- Added `infrastructure/scripts/export-support-bundle.ps1` so self-host maintainers can collect the same bundle from PowerShell without hand-stitching multiple endpoint calls.
- Added `frontend/web/src/services/supportBundleApi.ts` and extended `App.vue` so the operational trace surface now exposes a support-bundle summary, export action, and collection hints directly in the product shell.
- Updated development and smoke-test documentation so the support bundle is now part of the standard debugging and issue-reproduction workflow.

## Sprint 128 - Open-source production readiness consolidation

Status: Completed

Goal:
Close the current roadmap block with a consolidation sprint focused on the final open-source maturity gap before broader commercialization or heavier scale work.

Deliverables:

- Final gap review against supported OSS posture.
- Consolidated verification pass across release, install, runtime, and supportability.
- Updated estimate of the remaining path beyond the release-candidate block.

Exit criteria:

- The repository has a clearly documented post-release-candidate maturity checkpoint and a refreshed next-phase estimate.

Implemented notes:

- Added `docs/operations/post-release-candidate-checkpoint.md` to capture the supported OSS posture, explicit non-goals, consolidated verification gate, and refreshed remaining estimate in one place.
- Added `infrastructure/scripts/verify-oss-readiness.ps1` as a reusable consolidated maturity check that runs the backend build/test pass, frontend build, and Python test/bytecode verification from one command.
- Linked the new checkpoint from `README.md`, `INSTALL.md`, and `docs/operations/release-candidate.md` so maintainers can jump directly to the post-rc maturity gate from the main entrypoints.
- Re-ran the consolidated readiness verification successfully after the checkpoint was added.
- Refreshed the remaining estimate beyond the current release-candidate block to roughly 15 to 25 more consistent sprints, assuming the team continues prioritizing cross-cutting product maturity and supportability.

## Sprint 129 - Support handoff and issue reproduction flow

Status: Completed

Goal:
Turn the new maturity checkpoint and support bundle into a repeatable support workflow that third parties can use when filing or reproducing issues.

Implemented notes:

- Added `docs/operations/support-handoff-and-reproduction.md` as the canonical issue reproduction and support evidence guide.
- Added `.github/ISSUE_TEMPLATE/bug_report.md` so maintainers get consistent bug reports with environment, reproduction, and evidence details on the first pass.
- Linked the support handoff guidance from `README.md`, `docs/operations/post-release-candidate-checkpoint.md`, `prompts/CODEX_AUTOPILOT.md`, and `.github/copilot-instructions.md` so the support flow is visible from the main project entrypoints.
- Refreshed the post-release-candidate checkpoint and project status to point at the new support intake workflow.

Exit criteria:

- A self-hoster or contributor can file a useful issue with the right evidence on the first pass.

## Sprint 130 - Support intake workflow hardening

Status: Completed

Goal:
Turn the documentation-first support flow into a lightweight product surface that helps operators capture and export the minimum reproducible evidence without hunting through the stack.

Implemented notes:

- Added a support intake editor directly into the frontend support bundle surface so operators can capture summary, reproduction steps, expected result, actual result, and evidence notes without leaving the product.
- Added a reusable issue-draft builder in `frontend/web/src/services/supportBundleApi.ts` that maps the live tenant, persistence posture, selected scenario, selected route, and support bundle summary into the GitHub bug-report structure.
- Added one-click clipboard export for the generated issue draft plus a visible preview so maintainers can review the final report before filing it.
- Updated the support handoff documentation to include the new operator-side intake flow.

Exit criteria:

- A maintainer can move from symptom to actionable issue report with less manual assembly and fewer missing fields.

## Sprint 131 - Support artifact packaging and escalation cues

Status: Completed

Goal:
Reduce the last bits of ambiguity in support escalation by packaging the right supporting artifacts and making the next operator action more obvious.

Implemented notes:

- Extended the backend support bundle summary with `escalationTarget` and `artifactChecklist`, so the API now classifies whether the operator is looking at a likely product issue, a configuration gap, a dependency/backbone problem, or still-thin evidence.
- Added integration coverage to verify the support bundle endpoint returns the new escalation and artifact fields.
- Re-verified the backend integration slice around `HealthEndpointsTests`.

Exit criteria:

- A self-hoster can prepare a high-signal issue packet with fewer manual interpretation steps and fewer incomplete escalations.

## Sprint 132 - Operator-facing escalation cues

Status: Completed

Goal:
Expose the new backend escalation signals directly in the operator workspace so support triage becomes visible before an issue is filed.

Implemented notes:

- Extended `frontend/web/src/services/supportBundleApi.ts` to map `escalationTarget` and `artifactChecklist`.
- Added a visible escalation badge plus an artifact checklist inside the support bundle panel so the operator can tell whether the next action is product triage, configuration review, dependency recovery, or more evidence collection.
- Included the escalation target and artifact checklist in the generated issue draft so the support packet now carries the same triage framing as the UI.

Exit criteria:

- The operator can see the right escalation posture and required artifacts without reading raw JSON first.

## Sprint 133 - Local support issue packet packaging

Status: Completed

Goal:
Make local support issue preparation reproducible even outside the frontend by packaging the minimum useful evidence into one folder.

Implemented notes:

- Added `infrastructure/scripts/package-support-issue.ps1` to collect or reuse a support bundle, prepare an `ISSUE_DRAFT.md`, and write a `SUPPORT_MANIFEST.json`.
- Updated the support handoff guide and README so maintainers can discover the packet flow from the main support entrypoints.
- Syntax-validated the new packaging script with the PowerShell parser.

Exit criteria:

- A self-hoster can prepare a support issue packet locally without manually collecting every file by hand.

## Sprint 134 - Support artifact naming normalization

Status: Completed

Goal:
Normalize support artifact naming across the UI and local scripts so issue evidence remains traceable over longer debugging loops.

Implemented notes:

- Added a reusable support artifact stem builder in `frontend/web/src/services/supportBundleApi.ts`.
- Updated the frontend support bundle export and issue draft download flow to emit stable, tenant-aware filenames instead of generic timestamps.
- Updated `package-support-issue.ps1` so the generated packet folder includes the tenant-aware support naming pattern.
- Re-verified the frontend production build after the naming flow was added.

Exit criteria:

- Support artifacts from the frontend and local scripts are easier to correlate and reuse during longer support investigations.

## Sprint 135 - Support packet release handshake

Status: Completed

Goal:
Close the remaining supportability gap by making the support packet easier to validate against release posture and easier to hand off between maintainers.

Implemented notes:

- Added `infrastructure/scripts/validate-support-issue.ps1` to check required support packet files, issue draft headings, manifest shape, support bundle summary fields, and first-line evidence quality.
- The validator writes `SUPPORT_VALIDATION.json` with `Accepted`, `AcceptedWithWarnings`, or `Rejected`, so maintainers can quickly accept or bounce a packet before deeper debugging.
- Rehearsed the validator on a local support packet fixture and confirmed it returns `Accepted` for a complete minimal packet.

Exit criteria:

- A maintainer can quickly accept or reject a support packet on completeness before deeper debugging starts.

## Sprint 136 - Packaged maintainer handoff

Status: Completed

Goal:
Turn support packet creation into a stronger maintainer handoff flow instead of a raw file dump.

Implemented notes:

- Extended `infrastructure/scripts/package-support-issue.ps1` so it now invokes the validator automatically after packaging.
- Added automatic generation of `MAINTAINER_HANDOFF.md` and `SUPPORT_VALIDATION.json` inside the packaged issue folder.
- Tested the packaging flow end to end against a local packet fixture.

Exit criteria:

- A packaged support issue now carries its own validation verdict and maintainer handoff summary.

## Sprint 137 - Maintainer packet review guide

Status: Completed

Goal:
Document the maintainer review path so packet acceptance becomes consistent across sessions and contributors.

Implemented notes:

- Added `docs/operations/support-packet-review.md` with acceptance, rejection, warning, and release-posture review rules.
- Updated `docs/operations/support-handoff-and-reproduction.md` and `README.md` so the review guide and validation command are visible from the main support entrypoints.

Exit criteria:

- A maintainer has one obvious place to follow when judging packet completeness.

## Sprint 138 - Operator packet readiness cues

Status: Completed

Goal:
Expose packet completeness directly in the operator workspace before the issue is handed off.

Implemented notes:

- Added packet-readiness evaluation logic in `frontend/web/src/services/supportBundleApi.ts`.
- Added a visible packet-readiness block in the support panel so operators can see whether the packet is ready, missing runtime evidence, or still missing operator detail.
- Re-verified the frontend production build after the readiness cues were added.

Exit criteria:

- The operator can see whether the packet is handoff-ready before leaving the product.

## Sprint 139 - Release posture handshake cues

Status: Completed

Goal:
Make the packet readiness decision explicitly aware of the current OSS release posture.

Implemented notes:

- Added release-handshake guidance in `frontend/web/src/services/supportBundleApi.ts` so readiness and escalation target now combine into a concrete handoff recommendation.
- Added a release-handshake block in the support panel so the operator can see whether to hold the packet, route it to config review, dependency review, or maintainer handoff.
- Re-verified the frontend production build after the release-handshake cues were added.

Exit criteria:

- The operator can tell whether a support packet fits the current supported OSS posture before escalating it.

## Sprint 140 - Support packet operational reset loop

Status: Completed

Goal:
Close the next supportability gap by making it easier to refresh, rebuild, and revalidate a packet after the operator retries or resets the local environment.

Implemented notes:

- Added `infrastructure/scripts/refresh-support-issue.ps1` so an existing packet can be refreshed after a retry, reset, or rebootstrap instead of forcing a full packet restart.
- The refresh flow can reuse a provided support bundle, reuse a provided issue draft, or collect a fresh bundle through the protected API before revalidating the packet.
- Tested the refresh flow end to end against a local packet fixture.

Exit criteria:

- A self-hoster can retry an issue, refresh the packet, and keep the handoff trail coherent instead of starting over.

## Sprint 141 - Packet attempt history continuity

Status: Completed

Goal:
Preserve a coherent handoff history across repeated retries so support packets do not lose context after refreshes.

Implemented notes:

- Extended `package-support-issue.ps1` so new packets now start with `packetSchemaVersion`, attempt counters, refresh metadata, and `SUPPORT_ATTEMPTS.json`.
- Extended `refresh-support-issue.ps1` so refreshed packets increment `currentAttempt`, record `lastRefreshReason`, and append a new attempt entry.
- Rebuilt the maintainer handoff summary so it now exposes the current attempt and latest refresh reason.

Exit criteria:

- A maintainer can see whether a packet is an initial capture or a later retry without diffing files manually.

## Sprint 142 - Validation-driven packet action guidance

Status: Completed

Goal:
Turn packet validation into an actual operating decision instead of a passive report.

Implemented notes:

- Extended `validate-support-issue.ps1` so it now validates packet attempt history and emits a `recommendedAction` of `Reuse`, `Refresh`, or `Regenerate`.
- Hardened the validator against older packet shapes and variable PowerShell JSON object forms through normalized numeric and attempt-history parsing.
- Re-validated the refreshed packet fixture and confirmed the accepted packet now recommends `Reuse`.

Exit criteria:

- A maintainer can tell whether to keep, refresh, or discard the current packet without interpreting raw warnings manually.

## Sprint 143 - Support packet refresh-loop documentation

Status: Completed

Goal:
Document the reset and retry lifecycle so operators and maintainers follow one shared support refresh path.

Implemented notes:

- Added `docs/operations/support-packet-refresh-loop.md` with explicit rules for when to reuse, refresh, or regenerate a packet.
- Updated `README.md`, `docs/operations/support-handoff-and-reproduction.md`, `docs/operations/support-packet-review.md`, `docs/operations/post-release-candidate-checkpoint.md`, and `docs/operations/release-candidate.md` so the refresh loop is visible from the main support and release entrypoints.
- Added the direct validation command to the support handoff guide.

Exit criteria:

- A self-hoster can follow one obvious reset and packet-refresh path without relying on session memory.

## Sprint 144 - Operator retry guidance surface

Status: Completed

Goal:
Expose support packet retry guidance directly in the operator workspace so reset-versus-reuse decisions are visible before handoff.

Implemented notes:

- Extended `frontend/web/src/services/supportBundleApi.ts` with `buildSupportPacketRetryGuidance`.
- Added an `After reset or retry` guidance block to the support panel in `frontend/web/src/App.vue`.
- Re-verified the frontend production build after the retry guidance surface was added.

Exit criteria:

- The operator can tell from the product UI whether to reuse the current packet, refresh it after a retry, or tighten the issue narrative first.

## Sprint 145 - Support packet artifact pruning and archive hygiene

Status: Completed

Goal:
Reduce long-run support clutter by making packet refreshes easier to keep tidy and easier to archive without losing the useful handoff trail.

Implemented notes:

- Added `infrastructure/scripts/archive-support-issue.ps1` so the current packet state can be archived before it is overwritten by a retry refresh.
- Extended `refresh-support-issue.ps1` with `-ArchiveCurrentStateBeforeRefresh`, so support retries can preserve the previous evidence trail automatically.
- Rehearsed the archive-before-refresh path successfully on a local support packet fixture.

Exit criteria:

- Repeated support retries do not produce confusing packet sprawl or ambiguous evidence ownership.

## Sprint 146 - Support archive index continuity

Status: Completed

Goal:
Track archived packet states as first-class support artifacts instead of leaving them as opaque folders.

Implemented notes:

- Added `SUPPORT_ARCHIVE_INDEX.json` and archive metadata updates through `archive-support-issue.ps1`.
- Extended `SUPPORT_MANIFEST.json` with `archiveCount`, archive timestamps, and archive reasons.
- Preserved archive metadata alongside attempt history so the handoff trail now covers both retries and archived packet snapshots.

Exit criteria:

- A maintainer can see which archived packet snapshots still matter without opening every archive folder manually.

## Sprint 147 - Archive pruning command

Status: Completed

Goal:
Make repeated packet refreshes operationally sustainable by providing a bounded pruning workflow.

Implemented notes:

- Added `infrastructure/scripts/prune-support-issue-archives.ps1` so older archive folders can be reduced to the latest kept set.
- The prune flow updates both `SUPPORT_ARCHIVE_INDEX.json` and `SUPPORT_MANIFEST.json` after cleanup.
- Rehearsed the prune command in the archive-hygiene fixture flow.

Exit criteria:

- Longer support investigations can reduce archive clutter without losing the latest relevant retry trail.

## Sprint 148 - Archive hygiene documentation

Status: Completed

Goal:
Document when to archive, when to prune, and when to keep older packet snapshots.

Implemented notes:

- Added `docs/operations/support-packet-archive-hygiene.md`.
- Updated `README.md`, `docs/operations/support-packet-refresh-loop.md`, and `docs/operations/support-handoff-and-reproduction.md` so the archive and prune flows are visible from the main support entrypoints.
- Clarified the archive and pruning commands in the operator-facing support documentation.

Exit criteria:

- A self-hoster can follow an explicit archive-versus-prune rule instead of improvising packet cleanup during longer debugging loops.

## Sprint 149 - Operator archive guidance surface

Status: Completed

Goal:
Expose archive hygiene guidance directly in the operator workspace so packet cleanup decisions are visible before the folder becomes noisy.

Implemented notes:

- Added `buildSupportPacketArchiveGuidance` to `frontend/web/src/services/supportBundleApi.ts`.
- Added an `Archive hygiene` guidance block to the support panel in `frontend/web/src/App.vue`.
- Re-verified the frontend production build after the archive guidance surface was added.

Exit criteria:

- The operator can see from the product UI when older packet snapshots should be kept, archived, or pruned.

## Sprint 150 - Support packet lifecycle summary and export manifest tightening

Status: Completed

Goal:
Make the packet lifecycle even easier to hand off by tightening the summary of active packet state versus archived packet state.

Implemented notes:

- Added `infrastructure/scripts/summarize-support-issue.ps1` to generate `SUPPORT_LIFECYCLE.json` and `SUPPORT_LIFECYCLE.md`.
- The lifecycle summary now exposes the current attempt, refresh count, archive count, validation status, recommended action, latest attempt, latest archive, and canonical packet state.
- Rehearsed the lifecycle summary in the packet fixture flow so the active handoff state is now explicit instead of inferred.

Exit criteria:

- A maintainer can identify the canonical current packet state at a glance even after multiple retries and archived snapshots.

## Sprint 151 - Manifest lifecycle tightening

Status: Completed

Goal:
Make the export manifest itself clearer about the active packet state and its lifecycle contract.

Implemented notes:

- Extended `SUPPORT_MANIFEST.json` with `lifecycleSummaryFile` and `canonicalPacketState`.
- Updated the lifecycle summarizer so it keeps the manifest aligned with the current canonical handoff state.
- Tightened the refresh flow so archive metadata remains correct after archive-before-refresh execution.

Exit criteria:

- The manifest points directly at the lifecycle summary and no longer leaves the canonical handoff state implicit.

## Sprint 152 - Lifecycle-aware validation

Status: Completed

Goal:
Treat the lifecycle summary as part of the support packet contract instead of an optional extra.

Implemented notes:

- Extended `validate-support-issue.ps1` so it now requires `SUPPORT_LIFECYCLE.json`.
- Added lifecycle-aware validation checks for `lifecycleSummaryFile` and `canonicalPacketState`.
- Re-validated the lifecycle fixture flow and confirmed the packet still validates as `Accepted`.

Exit criteria:

- A packet that claims lifecycle clarity must actually carry the lifecycle summary and consistent manifest metadata.

## Sprint 153 - Lifecycle summary documentation

Status: Completed

Goal:
Document how maintainers should read the active-versus-archived packet story without reconstructing it manually.

Implemented notes:

- Added `docs/operations/support-packet-lifecycle-summary.md`.
- Updated `README.md` and `docs/operations/support-handoff-and-reproduction.md` so the lifecycle summary command and guide are visible from the main support entrypoints.
- Clarified the role of the lifecycle summary in the broader support packet workflow.

Exit criteria:

- A maintainer has one obvious document that explains how to read the current packet state versus historical packet state.

## Sprint 154 - Operator canonical handoff guidance

Status: Completed

Goal:
Expose the canonical packet idea directly in the operator workspace so the active packet state is clear before escalation.

Implemented notes:

- Added `buildSupportPacketLifecycleGuidance` to `frontend/web/src/services/supportBundleApi.ts`.
- Added a visible `Canonical handoff` block to the support panel in `frontend/web/src/App.vue`.
- Re-verified the frontend production build after the canonical handoff guidance surface was added.

Exit criteria:

- The operator can tell from the product UI whether the current packet should be treated as the main maintainer handoff state.

## Sprint 155 - Support packet timeline and delta narration

Status: Completed

Goal:
Close the next maturity gap by making the packet timeline easier to narrate across multiple retries and archived snapshots.

Implemented notes:

- Extended `infrastructure/scripts/summarize-support-issue.ps1` so `SUPPORT_LIFECYCLE.json` and `SUPPORT_LIFECYCLE.md` now include a timeline and explicit delta narration.
- The lifecycle summary now tells the maintainer what the latest active attempt is, what the latest archived snapshot is, and how the current packet differs from that archived state.
- Rehearsed the lifecycle timeline flow through package, archive, refresh, summarize, and validate on the support packet fixture.

Exit criteria:

- A maintainer can reconstruct the packet history faster without reading every raw metadata file in full.

## Sprint 156 - Canonical handoff file contract tightening

Status: Completed

Goal:
Make the packet contract more explicit about which files define the active maintainer handoff.

Implemented notes:

- Extended `SUPPORT_LIFECYCLE.json` with `canonicalHandoffFiles`.
- Extended `SUPPORT_MANIFEST.json` with `activeHandoffFiles` and `archiveIndexFile`.
- Tightened the summarizer so the manifest is updated with the canonical handoff contract on every summary refresh.

Exit criteria:

- A maintainer can identify the canonical active handoff file set without guessing from the packet folder contents.

## Sprint 157 - Timeline-aware lifecycle validation

Status: Completed

Goal:
Treat timeline and delta narration as required lifecycle artifacts instead of optional storytelling extras.

Implemented notes:

- Extended `validate-support-issue.ps1` so it now requires `SUPPORT_LIFECYCLE.md`.
- Added lifecycle validation checks for timeline fields, delta narration, canonical handoff files, and lifecycle markdown sections.
- Re-validated the timeline fixture flow and confirmed the packet still validates as `Accepted`.

Exit criteria:

- A packet cannot claim mature lifecycle narration unless the structured and human-readable lifecycle outputs are both present and coherent.

## Sprint 158 - Lifecycle reading-order documentation

Status: Completed

Goal:
Make the maintainer reading order for support packets faster and more consistent.

Implemented notes:

- Updated `docs/operations/support-packet-lifecycle-summary.md` with the preferred reading order and clearer explanation of timeline and delta interpretation.
- Refined the lifecycle-summary guidance so it now explains the canonical handoff rule and what questions the summary should answer.
- Updated `README.md` so the lifecycle summary command remains discoverable from the main support entrypoint.

Exit criteria:

- A maintainer can approach the packet in a predictable order without reconstructing the packet story manually.

## Sprint 159 - Operator timeline and delta guidance surface

Status: Completed

Goal:
Expose the packet story more clearly in the operator workspace before the issue reaches a maintainer.

Implemented notes:

- Added `buildSupportPacketTimelineGuidance` and `buildSupportPacketDeltaGuidance` to `frontend/web/src/services/supportBundleApi.ts`.
- Added visible `Timeline story` and `Latest delta` blocks to the support panel in `frontend/web/src/App.vue`.
- Re-verified the frontend production build after the timeline and delta guidance surface was added.

Exit criteria:

- The operator can explain the active packet story and the most recent change in posture before escalation.

## Sprint 160 - Support packet classification and triage shortcuts

Status: Completed

Goal:
Reduce the remaining support friction by making packet class, triage lane, and likely next owner more obvious at a glance.

Deliverables:

- Added `buildSupportPacketClassification` and `buildSupportPacketTriageShortcut` in `frontend/web/src/services/supportBundleApi.ts`.
- Added visible `Packet class` and `Triage shortcut` blocks to the support panel in `frontend/web/src/App.vue`.
- Re-verified the frontend production build after the triage-first operator surface was added.

Exit criteria:

- A maintainer or operator can tell the initial triage lane faster without reading the full packet story first.

## Sprint 161 - Lifecycle triage metadata contract

Status: Completed

Goal:
Make triage metadata part of the packet contract itself instead of leaving it as UI-only interpretation.

Deliverables:

- Extended `summarize-support-issue.ps1` so lifecycle outputs now include `packetClass`, `triageLane`, `nextOwner`, and `triageChecks`.
- Kept the triage metadata aligned in `SUPPORT_MANIFEST.json` through summary generation.
- Added a `Triage shortcuts` section to `SUPPORT_LIFECYCLE.md`.

Exit criteria:

- A support packet can carry its own triage lane and next-owner hint even outside the frontend.

## Sprint 162 - Validation-aware triage consistency

Status: Completed

Goal:
Prevent support packets from drifting between manifest, lifecycle summary, and maintainer reading order.

Deliverables:

- Extended `validate-support-issue.ps1` so triage metadata fields are required and cross-checked.
- Validation now requires the lifecycle markdown `Triage shortcuts` section.
- Re-ran the local packet validation flow against the strengthened contract.

Exit criteria:

- Triaging metadata mismatches are visible before a packet is handed to a maintainer.

## Sprint 163 - Maintainer handoff triage cues

Status: Completed

Goal:
Make the packet handoff file itself actionable without forcing a maintainer to infer who should move next.

Deliverables:

- Extended `package-support-issue.ps1` and `refresh-support-issue.ps1` so `MAINTAINER_HANDOFF.md` now includes packet class, triage lane, next owner, and triage shortcuts.
- Included `SUPPORT_LIFECYCLE.json` in the visible handoff file list.
- Rehearsed packet packaging with triage-aware handoff output.

Exit criteria:

- A maintainer can open `MAINTAINER_HANDOFF.md` and know the likely next owner and first review lane immediately.

## Sprint 164 - Triage documentation and singleton history hardening

Status: Completed

Goal:
Document the new support triage contract and harden packet summary behavior for minimal one-attempt histories.

Deliverables:

- Updated `docs/operations/support-packet-review.md`, `support-handoff-and-reproduction.md`, and `support-packet-lifecycle-summary.md` with triage-first reading guidance.
- Hardened support packet JSON history normalization so single-attempt histories still produce a valid latest-attempt summary.
- Re-ran the local packet packaging smoke flow and confirmed triage metadata plus latest-attempt narration are preserved.

Exit criteria:

- The support packet docs and scripts stay coherent even when the packet has only one attempt and no archive history yet.

## Sprint 165 - Release context capture in support packets

Status: Completed

Goal:
Reduce maintainer back-and-forth by attaching explicit repository, release, and runtime context to each support packet.

Deliverables:

- Extended `package-support-issue.ps1` and `refresh-support-issue.ps1` so new packets now capture repository branch, commit, tag hint, dirty-worktree posture, and runtime shell metadata in the manifest.
- Added release posture and runtime capture objects to the packet contract instead of leaving repo context as free-form maintainer follow-up.
- Re-ran the packet smoke flow and confirmed the captured packet now carries explicit repo and shell context.

Exit criteria:

- A maintainer can tell which repo state or release posture produced the packet without asking the operator for a second follow-up.

## Sprint 166 - Lifecycle and handoff release context surface

Status: Completed

Goal:
Make the captured repo and runtime context readable in the two files maintainers open first.

Deliverables:

- Extended `summarize-support-issue.ps1` so `SUPPORT_LIFECYCLE.json` and `SUPPORT_LIFECYCLE.md` now surface `releaseContext` and `runtimeContext`.
- Extended `MAINTAINER_HANDOFF.md` generation during package and refresh flows with dedicated `Release context` and `Runtime context` sections.
- Re-checked the generated handoff output to confirm branch, commit hint, and runtime source are visible at a glance.

Exit criteria:

- A maintainer can open the lifecycle summary or handoff file and see the reproducing repo and runtime posture immediately.

## Sprint 167 - Validation-aware release context contract

Status: Completed

Goal:
Keep release and runtime capture aligned across manifest, lifecycle summary, and markdown sections.

Deliverables:

- Extended `validate-support-issue.ps1` so release and runtime context blocks are required in the manifest and lifecycle summary.
- Added lifecycle markdown checks for `Release context` and `Runtime context` sections.
- Added consistency warnings for key release and runtime context fields when manifest and lifecycle drift apart.

Exit criteria:

- Packets that lose release context or drift between manifest and lifecycle are flagged before maintainer handoff.

## Sprint 168 - Operator-visible release context guidance

Status: Completed

Goal:
Help the operator capture the same release context in the UI and issue narrative before the packet leaves the workspace.

Deliverables:

- Added `buildSupportPacketReleaseContextGuidance` in `frontend/web/src/services/supportBundleApi.ts`.
- Added a visible `Release context` block in `frontend/web/src/App.vue`.
- Extended the generated issue draft template so branch, commit, and release posture are explicitly recorded in the `Environment` section.

Exit criteria:

- The operator-facing support workflow now prompts for the same repo and release context that the packet contract expects.

## Sprint 169 - Attempt-only release context narration hardening

Status: Completed

Goal:
Keep the lifecycle story accurate when multiple attempts exist even before an archive snapshot has been preserved.

Deliverables:

- Hardened `summarize-support-issue.ps1` so delta narration now distinguishes between a single attempt and multiple attempts without archives.
- Updated the support packet docs to mention release and runtime context during triage-first review.
- Re-ran the packet package and refresh smoke flow to confirm the refined narration and context sections stay coherent.

Exit criteria:

- Maintainers do not get a misleading lifecycle story when release context evolved across attempts without archived snapshots.

## Sprint 170 - Release context drift cues across attempts

Status: Completed

Goal:
Make it obvious when successive attempts were produced from different repo or runtime states so maintainers can distinguish bug drift from environment drift.

Deliverables:

- Extended `SUPPORT_ATTEMPTS.json` entries so each attempt now carries its own `releaseContext` and `runtimeContext` snapshot.
- Preserved per-attempt context snapshots through both package and refresh flows.
- Re-ran the packet drift smoke flow and confirmed successive attempts can now be compared on real context snapshots.

Exit criteria:

- A maintainer can tell whether the latest retry represents the same code and runtime state or a materially different one.

## Sprint 171 - Lifecycle context drift summary

Status: Completed

Goal:
Turn raw per-attempt context snapshots into a readable drift diagnosis.

Deliverables:

- Extended `summarize-support-issue.ps1` with a `contextDrift` summary object and `previousAttempt` snapshot.
- Added drift-aware delta narration so multi-attempt packets now call out whether the latest retry changed repo or runtime posture.
- Added `Context drift` and `Previous attempt` sections to `SUPPORT_LIFECYCLE.md`.

Exit criteria:

- The lifecycle summary tells a maintainer not just that retries happened, but whether the reproducing context moved.

## Sprint 172 - Validation-aware context drift contract

Status: Completed

Goal:
Keep drift analysis aligned across manifest, attempt history, lifecycle JSON, and lifecycle markdown.

Deliverables:

- Extended `validate-support-issue.ps1` so the latest attempt must carry context snapshots strongly enough to evaluate drift.
- Added lifecycle checks for `contextDrift` plus markdown sections for `Context drift` and `Previous attempt`.
- Added warnings when the latest attempt history is missing or partially missing the context snapshots needed for drift analysis.

Exit criteria:

- A packet that cannot support reliable drift analysis is flagged before maintainer handoff.

## Sprint 173 - Maintainer and operator drift guidance surfaces

Status: Completed

Goal:
Expose context drift guidance where operators and maintainers actually look first.

Deliverables:

- Added a visible `Attempt drift` guidance block in `frontend/web/src/App.vue`.
- Added `buildSupportPacketDriftGuidance` in `frontend/web/src/services/supportBundleApi.ts`.
- Extended `MAINTAINER_HANDOFF.md` generation with a dedicated `Context drift` section.

Exit criteria:

- Both the operator and the maintainer get an explicit reminder to compare the last two reproductions before assuming the failure is unchanged.

## Sprint 174 - Drift review documentation and contract hardening

Status: Completed

Goal:
Document the drift-first review habit and harden the history serializer so context snapshots survive round-trips.

Deliverables:

- Updated support packet docs with context-drift review guidance.
- Hardened attempt-history serialization helpers so nested release and runtime context snapshots survive PowerShell object-shape differences.
- Re-ran the drift-specific smoke flow and confirmed the packet now surfaces concrete changed fields such as branch, commit, host, shell, and capture source.

Exit criteria:

- Support packets keep context snapshots intact and maintainers have a documented way to interpret drift before deeper debugging.

## Sprint 175 - Evidence provenance and attachment anchors

Status: Completed

Goal:
Reduce support ambiguity further by making each packet point explicitly to the evidence artifacts and references that should travel with the handoff.

Deliverables:

- Added `evidenceProvenance.readingOrder` to the lifecycle summary so packet artifacts now have an explicit order, role, and canonical/optional status.
- Added a visible `Attachment anchors` guidance block in `frontend/web/src/App.vue`.
- Re-ran the support packet smoke flow and confirmed the packet now marks the issue draft and support bundle as the primary evidence pair.

Exit criteria:

- A maintainer can tell which packet artifacts are mandatory, which are optional, and in what order to read them without asking for clarification.

## Sprint 176 - Lifecycle evidence provenance contract

Status: Completed

Goal:
Carry evidence provenance as a first-class lifecycle structure instead of leaving it implicit in generated prose.

Deliverables:

- Extended `summarize-support-issue.ps1` so lifecycle outputs now include `evidenceProvenance` with reading order, primary files, optional files, and comparison-context posture.
- Added `Evidence provenance`, `Reading order`, and `Optional comparison context` sections to `SUPPORT_LIFECYCLE.md`.
- Ensured the lifecycle summary now distinguishes canonical evidence from comparison-only artifacts.

Exit criteria:

- The lifecycle summary exposes explicit evidence provenance in both JSON and markdown form.

## Sprint 177 - Validation-aware evidence provenance checks

Status: Completed

Goal:
Prevent packets from silently losing their attachment anchors or reading-order guidance.

Deliverables:

- Extended `validate-support-issue.ps1` so lifecycle evidence provenance fields are required.
- Added markdown checks for the evidence-provenance sections.
- Kept the packet contract strict enough that missing reading-order guidance is flagged before maintainer handoff.

Exit criteria:

- A packet without explicit provenance or attachment ordering is flagged as incomplete.

## Sprint 178 - Handoff evidence reading-order surface

Status: Completed

Goal:
Make the maintainer entrypoint echo the same artifact ordering that the lifecycle summary already knows.

Deliverables:

- Extended `package-support-issue.ps1` and `refresh-support-issue.ps1` so `MAINTAINER_HANDOFF.md` now includes `Reading order` and `Optional comparison context`.
- Synced the generated handoff file with the post-write lifecycle rerun so the handoff and lifecycle stay aligned on artifact presence.
- Re-ran the packet packaging flow and confirmed the handoff now marks `MAINTAINER_HANDOFF.md` as present in its own reading-order view.

Exit criteria:

- The maintainer handoff tells the reader exactly which files to open first and which ones are comparison-only context.

## Sprint 179 - Provenance review documentation and serializer cleanup

Status: Completed

Goal:
Document the attachment-anchor habit and clean up the serializer path so evidence provenance stays stable.

Deliverables:

- Updated support packet docs so `evidenceProvenance` is part of triage-first review.
- Kept the provenance ordering stable by normalizing the reading-order serializer and post-write summary refresh.
- Re-ran the packet smoke flow and confirmed canonical, optional, and absent artifacts are differentiated consistently.

Exit criteria:

- Maintainers and operators have a documented way to interpret packet artifact provenance, and the generated outputs stay internally consistent.

## Sprint 180 - Support packet evidence gap scoring

Status: Completed

Goal:
Make evidence quality more actionable by scoring which parts of the packet are still too thin for a confident maintainer handoff.

Deliverables:

- Extended `validate-support-issue.ps1` so packets now produce an `evidenceGapScore` with categories, severities, and next actions.
- Added a visible `Weakest evidence` block in `frontend/web/src/App.vue`.
- Re-ran a thin-packet smoke flow and confirmed the validator now prioritizes categories like audit or workflow evidence instead of only emitting generic warnings.

Exit criteria:

- A maintainer or operator can see not just that the packet is incomplete, but exactly which evidence category should be strengthened first.

## Sprint 181 - Lifecycle evidence gap summary

Status: Completed

Goal:
Carry evidence-gap scoring into the lifecycle layer so it becomes part of the support packet story.

Deliverables:

- Extended `summarize-support-issue.ps1` so lifecycle outputs now include `evidenceGapScore`.
- Added `Evidence gap score` and `Evidence gap categories` sections to `SUPPORT_LIFECYCLE.md`.
- Ensured lifecycle summaries now expose the top-priority next action alongside the score distribution.

Exit criteria:

- The lifecycle summary explains which proof category is strongest, weakest, or absent instead of relying on raw warnings alone.

## Sprint 182 - Validation-aware evidence gap contract

Status: Completed

Goal:
Prevent packets from silently dropping their evidence-gap analysis.

Deliverables:

- Extended `validate-support-issue.ps1` so lifecycle `evidenceGapScore` fields are required.
- Added markdown checks for the evidence-gap sections.
- Kept the packet contract strict enough that missing evidence-gap scoring is visible before handoff.

Exit criteria:

- A packet without explicit evidence-gap scoring is flagged as incomplete.

## Sprint 183 - Handoff evidence gap prioritization

Status: Completed

Goal:
Expose the scoring where maintainers actually start reading.

Deliverables:

- Extended `package-support-issue.ps1` and `refresh-support-issue.ps1` so `MAINTAINER_HANDOFF.md` now includes the evidence-gap score and categories.
- Re-ran the thin-packet smoke flow and confirmed the handoff now surfaces the top-priority category and next action directly.
- Kept the handoff aligned with lifecycle scoring after post-write summary refresh.

Exit criteria:

- A maintainer opening the handoff file can immediately see which evidence category should be strengthened first.

## Sprint 184 - Evidence gap review documentation

Status: Completed

Goal:
Document the scoring habit so operators and maintainers know how to react to weak or absent proof categories.

Deliverables:

- Updated support packet docs so `evidenceGapScore` is part of triage-first review.
- Clarified that packet improvement should target the highest-priority missing category before broad attachment collection.
- Re-verified the package flow after the documentation and scoring pass.

Exit criteria:

- Operators and maintainers have a documented method for reading the score and choosing the next highest-value evidence action.

## Sprint 185 - Support packet remediation checklist synthesis

Status: Completed

Goal:
Turn the scored evidence gaps into a short synthesized remediation checklist that can be executed without re-reading the entire packet.

Deliverables:

- Generate a compact prioritized checklist from the packet score and posture.
- Keep the checklist aligned with the packet class and next owner.
- Make the packet easier to improve in one focused pass.

Exit criteria:

- An operator can open the packet and see a short actionable checklist for the next remediation pass.

## Sprint 186 - Validation-aware remediation checklist contract

Status: Completed

Goal:
Make the remediation checklist part of the support packet lifecycle contract so packets cannot silently lose the new planning layer.

Deliverables:

- Require `remediationChecklist` in the lifecycle JSON.
- Require dedicated remediation sections in the lifecycle markdown.
- Validate checklist structure alongside evidence-gap scoring.

Exit criteria:

- Validation fails when lifecycle remediation guidance is missing or structurally incomplete.

## Sprint 187 - Maintainer handoff remediation surface

Status: Completed

Goal:
Expose the remediation checklist directly in maintainer-facing packet files so the next pass is immediately actionable.

Deliverables:

- Add remediation checklist and stop conditions to `MAINTAINER_HANDOFF.md`.
- Keep packaged and refreshed packet handoffs aligned with the lifecycle summary.
- Preserve packet-class and next-owner cues inside the handoff.

Exit criteria:

- A maintainer can open the handoff and see the next focused remediation pass without opening raw JSON first.

## Sprint 188 - Operator remediation guidance in the support intake UI

Status: Completed

Goal:
Surface the same focused-remediation logic in the frontend support panel so the operator sees the next best pass before exporting a packet.

Deliverables:

- Add a visible `Remediation pass` block beside packet readiness and weakest evidence.
- Keep the guidance aligned with readiness, packet class, triage lane, and next owner.
- Add exit criteria so the operator knows when to stop the pass.

Exit criteria:

- The support panel shows a short remediation plan that is more actionable than a generic warning state.

## Sprint 189 - Remediation workflow documentation and smoke verification

Status: Completed

Goal:
Document the new remediation workflow and verify it on a thin local support packet.

Deliverables:

- Update packet review, lifecycle summary, and handoff docs with remediation-checklist guidance.
- Re-run the frontend build and PowerShell parser checks.
- Re-run a thin-packet smoke flow and confirm remediation sections exist in lifecycle and handoff outputs.

Exit criteria:

- The docs explain the remediation flow and the smoke packet proves the new sections are generated correctly.

## Sprint 190 - Release-aware evidence capture shortcuts

Status: Completed

Goal:
Make the next remediation pass even faster by generating targeted capture shortcuts per packet lane and release posture.

Deliverables:

- Suggest lane-specific capture commands or evidence actions from packet class and release context.
- Distinguish workflow, dependency, and configuration review shortcuts.
- Keep the new cues visible in lifecycle and maintainer handoff outputs.

Exit criteria:

- The packet tells the next owner not just what is missing, but the fastest capture move to collect it.

## Sprint 191 - Capture guidance validation contract

Status: Completed

Goal:
Make the new capture guidance part of the support packet contract so lifecycle summaries cannot drop it silently.

Deliverables:

- Require `captureGuidance` in lifecycle JSON.
- Require capture guidance sections in lifecycle markdown.
- Validate shortcuts, checks, and exit criteria together.

Exit criteria:

- Validation fails when capture guidance is missing or structurally incomplete.

## Sprint 192 - Maintainer handoff capture surface

Status: Completed

Goal:
Expose the capture shortcuts directly in maintainer-facing packet files so the next evidence move is immediately visible.

Deliverables:

- Add capture guidance, shortcuts, checks, and exit criteria to `MAINTAINER_HANDOFF.md`.
- Keep package and refresh outputs aligned with the lifecycle summary.

Exit criteria:

- A maintainer can open the handoff and see the fastest capture move without opening raw JSON first.

## Sprint 193 - Operator capture surface in the support intake UI

Status: Completed

Goal:
Surface the same capture-shortcut logic in the frontend support panel so the operator sees the next evidence move before exporting a packet.

Deliverables:

- Add a visible `Capture shortcut` block beside remediation guidance.
- Keep the guidance aligned with packet class, triage lane, and next owner.
- Make the shortcut feel distinct from generic remediation advice.

Exit criteria:

- The support panel shows the fastest capture move for the current packet class and release posture.

## Sprint 194 - Capture workflow documentation and smoke verification

Status: Completed

Goal:
Document the capture guidance workflow and verify it on a thin local support packet.

Deliverables:

- Update packet review, handoff, and lifecycle docs with capture-shortcut guidance.
- Re-run the frontend build and PowerShell parser checks.
- Re-run a thin-packet smoke flow and confirm capture sections exist in lifecycle and handoff outputs.

Exit criteria:

- The docs explain the capture flow and the smoke packet proves the new sections are generated correctly.

## Sprint 195 - Release-aware evidence preset refinement

Status: Completed

Goal:
Refine the capture shortcuts into a small set of reusable evidence presets so the next packet can be generated with less operator effort.

Deliverables:

- Group capture shortcuts by packet class and release posture.
- Make the presets easy to reuse from UI, lifecycle, and handoff outputs.
- Keep the presets focused on the fastest high-signal evidence move.

Exit criteria:

- The packet can suggest a preset-style capture path instead of only a one-off reminder.

Implemented notes:

- Added reusable capture preset metadata and action lists in `frontend/web/src/services/supportBundleApi.ts`.
- Exposed capture preset identity and actions in the support panel capture block in `frontend/web/src/App.vue`.
- Extended `infrastructure/scripts/summarize-support-issue.ps1` so lifecycle outputs now include capture preset contract fields and a dedicated `Capture preset actions` markdown section.
- Extended `infrastructure/scripts/package-support-issue.ps1` and `infrastructure/scripts/refresh-support-issue.ps1` so `MAINTAINER_HANDOFF.md` includes capture preset identity and actions.
- Extended `infrastructure/scripts/validate-support-issue.ps1` so lifecycle validation enforces the capture preset section and JSON fields.
- Re-verified the frontend production build, script parser checks, and a local packet package smoke flow with preset sections present.

## Sprint 196 - Evidence preset documentation and smoke verification

Status: Completed

Goal:
Document the new reusable evidence presets and verify the capture preset contract on a thin local packet flow.

Deliverables:

- Update support packet review, handoff, and lifecycle docs with capture preset workflow guidance.
- Re-run frontend build and PowerShell parser checks.
- Re-run a thin packet smoke flow and confirm capture preset sections remain present in lifecycle and maintainer handoff outputs.

Exit criteria:

- The docs explain how to apply capture presets by class and release posture, and smoke artifacts prove the preset contract stays intact.

Implemented notes:

- Updated `docs/operations/support-packet-review.md` with capture preset action groups (configuration, dependency, operator, release-aware), reading guidance during review (lifecycle JSON/markdown fields, preset match checks), and mismatch detection rules.
- Updated `docs/operations/support-handoff-and-reproduction.md` with preset identity (presetId, presetLabel), how presets appear in the packet (lifecycle JSON/markdown, maintainer handoff, frontend), automatic preset selection during packaging, and validation expectations.
- Updated `docs/operations/support-packet-lifecycle-summary.md` with a full capture guidance field reference table (label, summary, presetId, presetLabel, releasePosture, triageLane, nextOwner, presetActions, shortcuts, checks, exitCriteria), the four preset groups, and the required preset markdown sections.
- Verified frontend production build passes (39 modules, 0 errors).
- Verified all 11 PowerShell scripts in infrastructure/scripts pass syntax parsing.
- Verified backend suite at 143 passing tests (no code changes).
- Created a minimal test support packet with lifecycle JSON + markdown containing all four required capture preset sections, validated through `validate-support-issue.ps1` — capture preset JSON fields and markdown sections pass validation.

## Sprint 197 - CI workflow for Python services, backend, and frontend

Status: Completed

Goal:
Create a GitHub Actions CI workflow that verifies all three product components on push and pull request to main.

Deliverables:

- `.github/workflows/ci.yml` with three jobs: python-services, backend, frontend.
- Python job: Python 3.12, pip install with dev extras, pytest (17 tests).
- Backend job: .NET 9, dotnet test on Domain.Tests (143) and Integration.Tests (15).
- Frontend job: Node 22, npm ci, vite build (39 modules).

Exit criteria:

- The workflow triggers on push and pull_request to main.
- Each job is independent so failures in one do not block the others.
- All jobs are verified locally before the workflow is created.

Implemented notes:

- Created `.github/workflows/ci.yml` with `actions/checkout@v4`, `setup-python@v5` (3.12), `setup-dotnet@v4` (9.0.x), `setup-node@v4` (22).
- Python job uses `python -m pytest -v` for portability.
- Frontend job uses `npm ci` for deterministic install via lockfile.
- Backend tests are split into two `dotnet test` commands (one per project) for clear failure reporting.
- All three jobs verified locally: 17 Python tests pass, 158 backend tests pass (143 + 15), frontend build produces 39 modules.

## Sprint 198 - CI E2E Playwright coverage

Status: Completed

Goal:
Extend the CI frontend job with Playwright browser tests so E2E operator workflows are verified automatically.

Deliverables:

- Install Playwright Chromium browser in CI.
- Start a Vite preview server, run 13 Playwright tests across 5 spec files, tear down the server.
- Upload Playwright HTML reports as artifacts on failure.

Exit criteria:

- The CI workflow runs `npx playwright test` in the `frontend` job after the production build.
- Tests exercise all major operator surfaces: control tower, transport, provider config, AI recommendations, warehouse workbench.
- Playwright's CI-aware config (retries=2, workers=1, chromium only) is leveraged.

Implemented notes:

- Added `npx playwright install --with-deps chromium` after the frontend build step in the `frontend` job.
- Added a single script step that starts `vite preview` in the background, polls `http://127.0.0.1:4173` with `curl` for up to 20 seconds, runs `npx playwright test`, captures the exit code, kills the server, and exits with the test exit code.
- Added `actions/upload-artifact@v4` on `failure()` to upload `playwright-report/` with 7-day retention.
- The existing Playwright config already sets `forbidOnly: !!CI`, `retries: 2` in CI, `workers: 1` in CI, and uses only the `chromium` project — no config changes needed.
- 13 E2E tests across 5 spec files (control-tower: 3, transport: 2, provider-config: 2, ai-recommendation: 4, warehouse-workbench: 2) all use mocked API responses via `page.route()`.
- E2E tests could not run locally due to environment port constraints, but all other component checks pass: YAML valid, frontend build (39 modules), backend (158 tests), Python (17 tests).

## Sprint 199 - Release tag preparation and CI verification

Status: Completed

Goal:
Fix the release preflight so it passes cleanly, and verify all three CI jobs pass locally in preparation for the first remote push and release tag.

Deliverables:

- Rename prompt files to match preflight expectations.
- Update internal references to point to renamed files.
- Add AI Agent Continuation section to README.
- Verify all CI jobs pass locally.
- Verify release preflight passes.

Exit criteria:

- The release preflight script passes with all checks green.
- All three CI jobs (Python, Backend, Frontend) pass locally.
- Documentation is updated to reflect the current release readiness.

Implemented notes:

- Renamed `prompts/AUTOPILOT.md` → `prompts/CODEX_AUTOPILOT.md` and `prompts/PROJECT_PROMPT.md` → `prompts/ORKYSTRA_UNIVERSAL_AGENT_PROMPT_FR.md` to match the release preflight expectations.
- Updated internal references in `prompts/CODEX_AUTOPILOT.md` to point to the renamed universal prompt file.
- Updated `PROJECT_STATUS.md` Sprint 119 entry to reference `prompts/CODEX_AUTOPILOT.md`.
- Added the AI Agent Continuation section to `README.md` with direct links to the universal prompt and autopilot entrypoint.
- Verified all three CI jobs pass locally: Python 17 tests, Backend 158 tests (143 domain + 15 integration), Frontend 39-module build.
- Release preflight now passes with all checks green.
- Remaining Risks: The CI workflow has not yet been validated on a real remote GitHub Actions push. The next step is to push the workflow and confirm all three jobs pass on an actual push, then cut the `v0.1.0-rc.1` tag from a clean tree.

## Sprint 200 - Release reproducibility and verification completeness

Status: Completed

Goal:
Make the `v0.1.0-rc.1` release reproducible from a clean clone by tracking the release-required memory files, and make the solution-level backend test command cover every test project.

Deliverables:

- Un-ignore the release-required memory files in `.gitignore`.
- Add the integration test project to the backend solution.
- Re-verify the release preflight and the full component verification set.

Exit criteria:

- The release-required files are tracked (not gitignored) so a fresh clone contains them.
- `dotnet test backend/Orkystra.slnx` runs both the domain and integration test projects.
- The release preflight passes and all three component checks are green.

Implemented notes:

- Removed `prompts/`, `constitution/`, `PROJECT_STATUS.md`, and `IMPLEMENTATION_ROADMAP.md` from `.gitignore` so they are tracked; kept `docs/blueprints/`, `docs/methodology/`, and `docs/adr/0001-project-execution-model.md` ignored as genuinely-private authoring artifacts.
- Added `Orkystra.Integration.Tests` to `backend/Orkystra.slnx` so the manifest's `dotnet test backend/Orkystra.slnx` command now runs 158 tests (143 domain + 15 integration) instead of only 143.
- Verified release preflight passes with all checks green, including the manifest artifact set.
- Verified backend (158 tests), frontend (39 modules), and Python (17 tests) all pass.

## Sprint 201 - Commit release-memory files and first real remote CI push

Status: Completed with a documented divergence blocker

Goal:
Commit the release-required memory files and confirm the CI workflow on a real remote GitHub Actions push.

Deliverables:

- Commit the release-memory files from Sprint 200.
- Verify the release preflight on a clean tree.
- Push to the remote and confirm the three CI jobs pass.

Exit criteria:

- The release-memory commit exists on the remote for the canonical lineage.
- All three remote CI jobs (Python, Backend, Frontend) pass on that push.

Implemented notes:

- Committed the release-required memory files (`.gitignore` un-ignore, `AGENTS.md`, `CURRENT_STATE.md`, `README.md`, `backend/Orkystra.slnx`, `IMPLEMENTATION_ROADMAP.md`, `PROJECT_STATUS.md`, `constitution/`, `prompts/`) as `4453898`.
- Verified the release preflight passes with `-AllowDirtyWorktree` before the commit and without it on the clean tree after the commit.
- The push to `origin/main` was rejected: the remote main had been force-updated to an unrelated lineage (`ea529f6`, FleetOps hosted-demo history, 2026-09-30) with no common ancestor with the Orkystra history.
- Per user decision, the commit was pushed to the new remote branch `release/rc-preflight-cleanup`; remote history was not rewritten.
- Remote CI confirmation is deferred to Sprint 202 because the canonical lineage is undecided, and the remote FleetOps `main` CI is currently failing on its own latest push.

## Sprint 202 - Resolve remote lineage divergence and confirm CI on the canonical lineage

Status: Completed

Goal:
Decide which history is canonical (local Orkystra release-candidate lineage versus remote FleetOps hosted-demo lineage), reconcile the repository accordingly, and confirm CI on the chosen lineage.

Deliverables:

- Human decision on the canonical lineage.
- If Orkystra is canonical: restore/repoint `main` from `release/rc-preflight-cleanup` (or equivalent) and confirm CI.
- If FleetOps is canonical: adopt it locally, archive the Orkystra lineage, and confirm CI.
- Then cut the `v0.1.0-rc.1` tag from a clean tree.

Exit criteria:

- One canonical lineage is agreed and CI is green on it.
- The release-memory work of Sprint 201 is not lost.

Implemented notes:

- Per user decision, the remote FleetOps history was adopted as canonical; the local Orkystra lineage was archived on `archive/orkystra-rc-lineage` and remote `release/rc-preflight-cleanup` without rewriting remote history.
- Local `main` was reset to `origin/main`; the running FleetOps CI on `main` was red, so Sprint 33's false completion was repaired: the missing `scripts/agent/verify_state_consistency.py` (silently blocked by a `.gitignore` rule) was restored with unit tests, Android setup moved to `android-actions/setup-android@v4`, and the removed MinIO community images were replaced with digest-pinned `bitnamilegacy` images plus SSE-S3 KMS and media identity provisioning (D-024).
- The canonical pipeline is green on `main`: runs `36993883034` (`bd41472`) and `36994528056` (`f039a81`).
- The Orkystra `v0.1.0-rc.1` tag is superseded by the lineage decision and must not be cut from the archive.
