# Changelog

Toutes les modifications fonctionnelles significatives sont documentées ici.

## Unreleased

### Added

- AndroidX instrumentation runner dependency for connected Sprint 11 Android test execution on real devices.
- Sprint 11 SQL Server Testcontainers harness with migration, tenant-uniqueness, concurrency, and recovery coverage.
- Playwright critical-flow suite for sign-in, dispatch progression, proof consultation, and tenant isolation.
- Android instrumentation tests for Room offline persistence and unique WorkManager scheduling behavior.
- Complete evidence-based product, architecture, security, QA, UX, and commercial audit.
- Evidence-gated Sprint 00–30 roadmap preserving the eleven completed sprints and adding exactly twenty dense value sprints for reliability, operations, driver UX, maintenance, compliance, telematics, reporting, support, and commercial validation.
- Safe first-organization bootstrap options for non-demo environments.
- Responsive, role-aware Web navigation with contextual page headings, mobile drawer, keyboard focus, and reduced-motion support.
- Android driver sync summary, next-action guidance, branded Material 3 theme, release-safe credential defaults, protected password rendering, and application icon.

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

- Connected Android quality-gate execution now distinguishes test failures from transient UTP package-install stalls and can temporarily manage ADB-only verification with automatic restoration on physical devices.
- Sprint 11 is closed after restoring the local Docker Desktop Linux engine and executing all three SQL Server/Testcontainers proofs without skips.
- The SQL integration factory now explicitly replaces the captured EF Core registration with the dynamic Testcontainers connection string.
- Sprint 11 browser proof now explicitly verifies that a second tenant cannot discover or mutate a Northwind mission through the dispatch API or UI.
- Sprint 11 validation now includes successful `connectedDebugAndroidTest` execution on a connected physical Android device.
- Sprint 11 governance was activated and is now closed after every mandatory proof passed.
- Local and CI quality gates now distinguish fast backend checks, Docker-backed SQL tests, Playwright browser E2E, and Android instrumentation APK compilation.
- Production now rejects known or missing JWT/media signing keys, local connection defaults, and demo-data seeding.
- Login now uses per-address rate limiting and ASP.NET Identity lockout tracking.
- SQL backup/restore PowerShell scripts parse correctly and the quality gate validates them.
- CI and the local gate now include stricter format, lint, dependency, recovery-script, and Android lint checks.

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
