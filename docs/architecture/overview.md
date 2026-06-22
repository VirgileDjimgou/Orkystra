# Architecture Overview

Orkystra is designed as a simulation-first, reality-ready logistics platform with provider-based integration, event-driven flow, and projection-oriented UI contracts.

## Sprint 0 Structure

```text
backend/
  Orkystra.slnx
  src/
    Orkystra.Api/
    Orkystra.Application/
    Orkystra.Contracts/
    Orkystra.Domain/
frontend/
  web/
python-services/
  ai-service/
  optimization-service/
infrastructure/
  docker-compose.yml
tests/
  backend/
```

## Dependency Direction

Backend dependencies must point inward:

```text
Api -> Application -> Domain
Api -> Contracts
Application -> Contracts
```

The domain layer must not depend on infrastructure, persistence, HTTP, MQTT, AI tooling, or UI code.

## Service Boundaries

- `backend/src/Orkystra.Api`: ASP.NET Core API and composition root.
- `backend/src/Orkystra.Application`: use case orchestration and ports.
- `backend/src/Orkystra.Domain`: pure domain model and invariants.
- `backend/src/Orkystra.Contracts`: DTOs and integration contracts.
- `frontend/web`: Vue 3 control tower client.
- `python-services/ai-service`: FastAPI and LangGraph AI service for grounded logistics recommendations.
- `python-services/optimization-service`: FastAPI and OR-Tools-preferred optimization service for bounded dispatcher workflows.
- `infrastructure`: local development dependencies and deployment assets.

## Sprint 1 Entry Point

Sprint 1 should add core domain primitives to `Orkystra.Domain` and tests under `tests/backend/Orkystra.Domain.Tests`.

## Domain Foundation

The first domain foundation includes:

- strong identifier types backed by `Guid`
- immutable value objects validated through factory methods
- `Result` and `DomainError` for explicit validation outcomes
- `Entity<TId>` and `AggregateRoot<TId>` abstractions
- base `DomainEvent` support for traceable state changes

## Warehouse Slice

Sprint 2 introduces the first warehouse aggregate:

- `Warehouse` is the aggregate root for zones, racks, slots, docks, and pallets.
- warehouse mutations emit explicit events such as `WarehouseCreated`, `ZoneCreated`, `PalletStored`, `PalletMoved`, `DockOccupied`, and `DockReleased`
- pallet storage enforces slot occupancy and max-weight checks
- dock operations enforce single active occupancy

The first read-model boundary for this context lives in `Orkystra.Contracts/Warehouse/WarehouseSummaryReadModel.cs`.

## Transport Slice

Sprint 3 introduces the first transport aggregate:

- `Route` is the aggregate root for truck assignment, driver assignment, stops, shipments, and deliveries
- transport mutations emit explicit lifecycle events such as `RouteCreated`, `DriverAssigned`, `ShipmentLoaded`, `TruckDeparted`, `TruckDelayed`, `TruckArrived`, and `DeliveryCompleted`
- departure requires a driver, at least one stop, at least one shipment, and all shipments loaded
- shipment assignment enforces truck capacity constraints

The first read-model boundary for this context lives in `Orkystra.Contracts/Transport/RouteSummaryReadModel.cs`.

## Simulation Foundation

Sprint 4 introduces the first deterministic simulation foundation:

- `Scenario` is the aggregate root for seeded simulation runs and virtual-time lifecycle
- `SimulationClock` supports deterministic advancement with a configurable speed multiplier
- synthetic world generation produces deterministic warehouse, order, and truck blueprints from a seed
- synthetic disruption generation produces deterministic injected events from a seed and aggregate references

The first read-model boundary for this context lives in `Orkystra.Contracts/Simulation/ScenarioSummaryReadModel.cs`.

## Event Backbone

Sprint 5 introduces the first event-distribution backbone without binding the codebase to a broker implementation yet:

- `Orkystra.Contracts/Eventing` now defines the shared event envelope, MQTT-aligned topic conventions, and outbox/inbox transport records
- `DomainEventEnvelopeFactory` turns domain events into routed envelopes using namespace and aggregate-id conventions
- `IdempotentProjectionRunner` and `IInboxStateStore` establish consumer-side duplicate protection
- `ScenarioSummaryProjection` is the first projection skeleton that consumes simulation events and updates a read model

This keeps event routing, transport contracts, and projection behavior ready for a future broker adapter while preserving the inward dependency rule.

## Control Tower UI

Sprint 6 introduces the first visible operator shell in `frontend/web`:

- the Vue client now renders a dense control-tower workspace instead of a generic starter shell
- scenario, warehouse, transport, and alert views are driven by stable read-model-shaped demo data
- a lightweight Three.js warehouse placeholder provides a first interactive digital twin surface
- simulation controls remain UI-local for now, which keeps the frontend contract-focused until API and broker adapters arrive

This gives the project an explorable demo surface without coupling the UI to unfinished backend delivery mechanisms.

## AI Layer 1

Sprint 7 introduces the first grounded AI service skeleton in `python-services/ai-service`:

- a FastAPI service now exposes recommendation endpoints over projection-shaped inputs
- the supervisor routes requests to a warehouse or dispatcher specialist and preserves a strict answer contract
- responses explicitly separate evidence, assumptions, recommendations, confidence, and missing data
- a LangGraph-shaped orchestration skeleton exists, with a deterministic fallback when LangGraph is unavailable at runtime

The AI service remains projection-first and bounded. It explains what it can support from the provided state and refuses to invent missing operational facts.

Sprint 21 connects that service to the operator workspace through a backend workflow endpoint:

- `Orkystra.Api` now exposes `POST /api/ai/recommendations`
- the endpoint collects the current tenant-aware overview snapshot before calling the AI service
- the browser sees one bounded recommendation envelope instead of having to talk to the Python service directly
- the workflow falls back to a local planner when the Python service is unreachable, keeping the operator experience usable during local development

## Optimization Layer

Sprint 8 introduces the first explainable optimization service in `python-services/optimization-service`:

- the service now accepts a canonical route optimization request with depot, vehicle, stops, time windows, matrices, and constraints
- the solver returns route order, ETA projections, load distribution, objective score, constraint violations, and human-readable explanation
- alternative route plans are part of the output contract so dispatch and AI layers can compare trade-offs
- OR-Tools is used when available, with a deterministic fallback to keep the project runnable in lighter environments

This keeps optimization separate from route projection while still producing outputs that the dispatcher and AI layers can explain upstream.

Sprint 22 connects that service to the operator workspace through a backend workflow endpoint:

- `Orkystra.Api` now exposes `POST /api/transport/routes/{routeId}/optimization`
- the endpoint collects the current tenant-aware route detail projection before calling the optimization service
- the browser sees one bounded optimization envelope instead of having to talk to the Python service directly
- the workflow falls back to a local comparison plan when the Python service is unreachable, keeping dispatcher review usable during local development

## Connector Layer

Sprint 9 introduces the first provider-facing connector layer in `Orkystra.Application` and `Orkystra.Contracts`:

- provider-neutral contracts now cover health, capabilities, sync status, schema description, and GPS position snapshots
- a `ProviderRegistry` resolves providers by identifier, domain, and capability instead of binding application logic to concrete adapters
- the first adapter skeletons cover CSV warehouse import, REST transport ingestion, and GPS telematics reads
- domain-facing outputs stay canonical by returning read-model DTOs and transport-neutral connector contracts

This proves that simulation and real adapters can sit behind the same application boundary without leaking vendor vocabulary into the core domain.

## Production Hardening

Sprint 10 introduces the first MVP hardening pass across API and deployment assets:

- API key authentication, tenant resolution, audit logging, and request metrics are now scaffolded in `Orkystra.Api`
- liveness, readiness, metrics, and operational-context endpoints provide a first observability surface
- Dockerfiles now exist for the API, frontend, AI service, and optimization service
- a stack-level compose file and smoke-test checklist make the full demo environment easier to run and verify repeatedly

This is intentionally a baseline, not a full enterprise security or operations layer, but it raises the project from prototype code to a more disciplined demo-ready stack.

## API To UI Integration

The first post-MVP increment connects the control tower frontend to the backend API:

- `Orkystra.Api` now exposes `GET /api/control-tower/overview`
- the endpoint aggregates canonical scenario, warehouse, route, alert, and event-feed projections into one UI-facing payload
- the frontend loads this overview through a service layer and falls back to local demo data if the API is unavailable
- connector-backed data and UI enrichment now coexist without forcing unfinished backend concerns into the visual layer

This is the first real bridge between projection-serving backend code and the operator workspace.

## Data Source Observability

The second post-MVP increment makes the API/UI bridge more transparent to operators and future developers:

- the control tower overview contract now includes `generatedAtUtc` and provider status snapshots
- the backend assembles provider health and sync metadata alongside the scenario, warehouse, and transport overview
- the frontend now distinguishes live API mode from fallback mode, exposes loading and fallback states, and surfaces provider health in the operator workspace

This keeps the demo resilient while making data provenance and connector health visible, which is essential before deeper live integrations arrive.

## Broader Overview Coverage

The third post-MVP increment widens the actual overview payload instead of only refining the connection around it:

- the warehouse demo provider now serves more than one canonical warehouse summary
- the transport demo provider now serves multiple canonical route summaries with different operational statuses
- the control tower overview service now derives alerts and event-feed entries from the current route and warehouse projections

This keeps the current architecture intact while making the operator workspace feel closer to a real multi-node logistics day.

## Connector Catalog Visibility

The next connector-focused increment turns the registry into a visible catalog:

- `GET /api/providers/catalog` exposes the current provider inventory
- each provider includes health, sync status, capabilities, schema description, and supported canonical read models
- the frontend now has a dedicated catalog surface for operator-facing connector inspection

This moves the project one step closer to live provider configuration and operational support workflows.

## Local Audit Persistence

The next operational increment moves audit from log-only scaffolding to a locally persisted operational trail:

- audit entries are appended to a local JSONL store
- `GET /observability/audit` exposes recent audit events through a protected endpoint
- correlation id, tenant, reason, and response status are now inspectable after the fact instead of only being emitted to logs

This improves demo support and operator troubleshooting while leaving room for a future centralized audit sink.

## Centralized Persistence Foundations

Sprint 23 introduces the first structured operational persistence layer in `Orkystra.Api`:

- `OperationalPersistenceStore` now writes key read-model snapshots and workflow envelopes into one SQLite database
- control-tower overview, warehouse projections, transport projections, and provider catalog responses are persisted as latest snapshots per tenant and projection key
- AI recommendation and route optimization requests are persisted as append-only workflow-run records for later inspection
- protected observability endpoints now expose recent persisted snapshots and workflow runs without asking developers to open ad hoc local files

This is intentionally a foundation rather than the final storage architecture. It centralizes persistence behavior and makes it queryable, while still leaving room for a future Postgres-backed or event-driven evolution.

## Demo-Ready Operational Polish

Sprint 24 turns that persistence foundation into a visible operator story:

- the frontend now exposes an `Operational trace` surface that shows recent workflow runs, persisted snapshots, and audit entries
- workspace refreshes, AI requests, optimization requests, and provider-configuration saves now naturally feed that trace
- the control tower can now demonstrate not only decisions and recommendations, but also the persisted evidence trail behind them

This makes the product feel more supportable and more believable in a near-final demo, even though the underlying providers are still mostly demo-backed.

## Live Provider Integration Foundations

Sprint 25 starts the transition from demo-only connectors toward real upstream transport data:

- `ProviderRegistryFactory` now builds connector registries from current runtime configuration, so API services stop hardcoding provider instances
- the REST transport adapter can now probe `/health` and read `/routes` plus `/routes/details` from a configured upstream HTTP endpoint
- placeholder or incomplete runtime settings intentionally keep the adapter in demo fallback mode, which preserves local stability while allowing disciplined live bring-up
- backend tests now cover both successful live reads and safe fallback behavior

This is still an early integration slice rather than a full enterprise connector, but it is the first real bridge between Orkystra's canonical transport projections and an external provider surface.

## MQTT Event Backbone Activation

Sprint 27 turns the earlier event-backbone scaffolding into a real brokered runtime path:

- `Orkystra.Api` now hosts an MQTT-backed publisher and consumer around the existing event envelope contracts
- simulation events can be published through MQTT, consumed back into the API, and dispatched through `IdempotentProjectionRunner`
- `ScenarioSummaryProjection` is now used as a singleton runtime projection that can be queried through `GET /api/simulation/scenarios`
- the backbone itself now exposes a small observability surface through `GET /observability/event-backbone`

This is still a narrow first slice rather than the final event architecture, but it proves the broker path end to end without bypassing the existing contract and projection abstractions.

## GPS Telematics Stream Activation

Sprint 28 extends that broker path to the first connector-originated telemetry flow:

- `GpsTelematicsProvider` now feeds canonical `GpsPositionSnapshot` payloads into MQTT through an integration-event envelope factory
- the API consumer subscribes to the configured GPS stream topic alongside simulation event traffic
- `GpsPositionProjection` keeps the latest known position per truck and exposes those positions through `GET /api/gps/positions`
- `POST /api/gps/positions/publish` provides a bounded workflow for publishing the latest provider positions into the broker

This is still demo-backed telemetry rather than a true live telematics vendor, but it proves that provider-originated MQTT traffic can now enter the platform and come back out as stable read-model projections.

## Transport Live Sync Snapshot Import

Sprint 29 connects the live REST transport path to the durable projection layer:

- `POST /api/transport/sync` imports the current transport route snapshot from the configured transport provider
- imported route summaries and route details are persisted into the operational SQLite store and can be served back without immediately re-pulling the upstream
- `GET /api/transport/sync-status` exposes the latest sync evidence, including imported route count, route references, source posture, and provider health
- `TransportProjectionService` now prefers the most recent persisted transport snapshot for a tenant when one exists

This gives the transport slice its first explicit synchronization boundary, which is a better long-term bridge between live connector reads, operational evidence, and future writeback or scheduling workflows.
