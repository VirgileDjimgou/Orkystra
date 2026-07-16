# Changelog

Toutes les modifications fonctionnelles significatives sont documentées ici.

## Unreleased

### Added

- Architecture initiale, roadmap, sprints et simulateur GPS minimal.
- Multi-tenant identity baseline with organizations, roles, JWT authentication, audit logs, and seeded demo users.
- Admin-only user management shell for the Vue application.
- Fleet registry for tenant-scoped vehicles, drivers, GPS devices, active/inactive lifecycle, historized device assignments, and idempotent CSV imports.
- Vue fleet registry screens for vehicles, drivers, and GPS devices with role-aware controls and CSV import panels.
- Live tracking with persisted telemetry history, current-position snapshots, duplicate/out-of-order protection, tenant-scoped metrics, and a multi-vehicle simulator.
- Vue live tracking map with synchronized vehicle list, paged history, SignalR connection status, and operator-facing telemetry UX.
- Dispatch missions with ordered stops, explicit lifecycle transitions, conflict-aware driver and vehicle assignment, delay simulation, and audited mission timelines.
- Vue dispatch board for mission planning, assignment, status progression, and mission-to-map navigation.
- Android driver workflow with secure login, offline mission cache, Room persistence, idempotent command outbox, and background sync.
- Pre-departure inspections, delivery proof capture, resumable private media uploads, and operator-facing proof review.
- Deterministic alerting, compliance document tracking, light maintenance planning, worker re-scan support, and a role-aware alert center.
- Partner and device integration APIs with scoped API keys, signed webhooks, SQL outbox retries, sandbox receipts, immutable audit enforcement, and an admin integration console.
- Production hardening and pilot readiness with administrator MFA, tenant lifecycle export/purge, OTLP observability, readiness checks, pilot Docker packaging, and SQL backup/restore scripts.

### Changed

- Validation locale Sprint 00 réalisée pour Docker, backend .NET, EF Core, Web et simulateur GPS.
- Migration EF Core initiale ajoutée et appliquée sur SQL Server local.
- Quality gate durcie avec vérifications Docker, .NET, Web, simulateur et health check API.
- Visible product branding aligned on `Orkystra FleetOps` and `FleetOps Driver`.
- Android Sprint 00 rendu reproductible avec wrapper Gradle versionné, JDK/SDK détectés et build `testDebugUnitTest assembleDebug` vert.
- Tracking APIs and SignalR flow are now authenticated and tenant-scoped.
- Tracking persistence moved from an in-memory latest-position cache to versioned telemetry ingestion plus current-position snapshots.
- Public README updated in English with architecture, modules, stack, fleet registry flow, and Mermaid diagrams.
- README expanded with dispatch architecture, mission flow, and Sprint 04 validation scope.
- README expanded with Sprint 05 mobile architecture, offline sync flow, and Android stack details.
- README, validation, and project status refreshed for Sprint 06 inspections, delivery proof, and signed private media access.
- README expanded with professional Sprint 09 architecture notes and real screenshots for admin, operator, isolated tenant, and Android surfaces.
- Android driver app now tolerates numeric enum contracts from the backend and allows local emulator loopback cleartext access for reproducible demos.
