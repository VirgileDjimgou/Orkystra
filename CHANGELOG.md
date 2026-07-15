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

### Changed

- Validation locale Sprint 00 réalisée pour Docker, backend .NET, EF Core, Web et simulateur GPS.
- Migration EF Core initiale ajoutée et appliquée sur SQL Server local.
- Quality gate durcie avec vérifications Docker, .NET, Web, simulateur et health check API.
- Branding visible mis à jour vers `Zynro Fleet` et `Zynro Drive` sans renommage technique massif.
- Android Sprint 00 rendu reproductible avec wrapper Gradle versionné, JDK/SDK détectés et build `testDebugUnitTest assembleDebug` vert.
- Tracking APIs and SignalR flow are now authenticated and tenant-scoped.
- Tracking persistence moved from an in-memory latest-position cache to versioned telemetry ingestion plus current-position snapshots.
- Public README updated in English with architecture, modules, stack, fleet registry flow, and Mermaid diagrams.
