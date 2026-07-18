# Changelog

Toutes les modifications fonctionnelles significatives sont documentées ici.

## Unreleased

### Added

- A modular full-project simulator that drives Admin, Operator, Driver, and device behavior through real APIs for three fictitious tenant sectors, produces clearly labelled reports, validates cross-tenant isolation, and captures deterministic Web and connected-Android walkthrough evidence.
- Reproducible simulation seeds for Northwind Logistics, Southridge Transport, and Westland Field Services, each with isolated fleet assets and all three human roles.
- Sprint 20 measured alpha pilot controls: explicit tenant administrator consent, privacy-minimal and idempotent daily operational aggregates, support-incident tracking, evidence export, and auditable `GO`/`SIMPLIFY`/`PIVOT`/`STOP` niche decisions.
- Administrator Pilot review workspace with aggregate-only evidence, consent-aware collection, support workaround tracking, and downloadable tenant-scoped evidence packages.
- Sprint 19 dispatch productivity: tenant-scoped route templates, draft duplication, replay-safe mission imports with preview and confirmation, a paged day/week planning board, saved views, deterministic resource suggestions, and atomic bulk assignment validation with maintenance, compliance, overlap, concurrency, and audit checks.
- A web daily-planning workspace for capacity-board refresh, template reuse, and explicit import confirmation.

- Sprint 18 compliance campaigns: tenant-configured document types and assignment policy, optional four-eyes review, replacement history, 30/14/7-day expiry horizons, coverage matrix, audit CSV export, and auditable Admin overrides.
- Targeted vehicle inspection campaigns with private tenant-scoped state and an Android Room/WorkManager queue that preserves idempotent offline driver submissions.

- Sprint 17 tenant-scoped maintenance work orders with source-key deduplication, optimistic concurrency, scheduled repair windows, stable decimal labour/parts costs, private media evidence, and auditable completion reasons.
- Automatic work-order creation for critical inspection defects and maintenance alerts, dispatch protection for immobilised vehicles, and overdue maintenance items in the operations exception queue.
- Maintenance backlog navigation for Admin and Operator users, plus an additive EF Core migration for work-order persistence.

- Sprint 16 S3-compatible private media storage with bucket-scoped credentials, server-side encryption requests, opaque tenant keys, durable SHA-256 manifests, and Production fail-fast configuration.
- Tenant-authorized short-lived media capabilities, audited logical revocation, deferred retention deletion, quarantine and abandoned-object cleanup, tenant export metadata, and SQL recovery coverage.
- Replayable filesystem-to-object migration through the Worker with per-asset JSON reporting, checksum verification, idempotent retries, and source-preserving rollback.
- Sprint 15 guided tenant activation with resumable CSV preview/confirmation, line-level validation, idempotent imports, role invitations linked to driver profiles, short-lived one-use Android pairing, removable sample data, readiness diagnostics, and privacy-minimal activation metrics.
- Tenant-scoped onboarding persistence and EF Core migrations for import sessions, invitation and pairing concurrency, activation events, and exact sample-data cleanup.
- Web and Playwright coverage for empty-tenant activation through first completed mission value, plus Android pairing entry and connected-device validation.
- Sprint 14 field workflow with an action-focused driver home, route progress, explicit sync state, external navigation links, CameraX evidence capture, system photo-picker fallback, controlled image compression, and handwritten recipient signatures.
- Durable delivery-proof evidence records that retain private media payloads and acknowledged chunk offsets across Room database reopen/process restart, with server-side enforcement that delivery photo and signature evidence are both present.
- Connected-device instrumentation coverage for workflow recovery and isolated Room test storage.
- Sprint 13 operations center with a unified exception queue across alerts, mission delays, critical inspection defects, and blocked driver synchronization incidents.
- Saved personal/team operations views, deterministic exception prioritization, tenant-safe search and filters, and bulk triage actions.
- SignalR-backed operations queue refresh, operator assignment/acknowledgment/snooze/resolve flows, and audited mission timeline write-back for exception resolution.
- Backend contracts, persistence entities, and migration support for operator exception state and blocked driver sync incidents.
- Backend integration coverage for operations queue tenant safety, saved views, bulk actions, and concurrency checks.
- Web operations center view with focus panel, live connection state, triage controls, and route-first landing experience after sign-in.
- Sprint 12 server-side sessions with tenant/user binding, immediate revocation checks, rotation, current/global logout, session listing, and administrator revocation.
- HttpOnly/SameSite Web authentication with in-memory CSRF proof, legacy localStorage JWT removal, cookie-backed SignalR, and expired-session recovery events.
- Android Keystore AES-GCM credential storage plus non-destructive Room migrations that remove the plaintext access-token column.
- Versioned `/api/v1/auth` and `/api/v1/admin` contracts, explicit deprecation headers on historical aliases, and a centralized role-to-operation policy matrix.
- Configurable driver-media limits, JPEG/PNG magic-byte validation, malware-test signature scanning, pre-publication quarantine, and sensitive-operation audit records.
- API CSP, anti-framing, MIME-sniffing, referrer, and permissions security headers.
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

- Driver mission summaries no longer misclassify processed idempotency receipts as unsynchronized device commands.
- Fixed Android dark-theme contrast for the app bar, mission cards, sync summary, stops, timeline, inspection, and delivery-proof panels.
- The mandatory PowerShell quality gate now executes the full multi-tenant API scenario in addition to the focused GPS dry run.
- Sprint 15 is closed with a `GO` gate after 115 fast backend tests, 3 SQL Server/Testcontainers proofs, 19 Web tests, 5 Playwright flows, and 5 connected Android tests on a Samsung SM-G975F.
- Sprint 14 is closed after Android unit tests, five connected Android instrumentation tests on a Samsung SM-G975F, backend proof authorization tests, and the full repository quality gate.
- Sprint 13 is closed after a green quality gate with 107 fast backend tests, 3 SQL Server/Testcontainers tests, 17 Web tests, 4 Playwright flows, API health/readiness validation, GPS dry run, and Android build validation.
- The default authenticated Web landing page now routes operators to the operations center, while the previous dashboard remains available under `/overview`.
- Playwright critical flows now assert the Sprint 13 operations-center landing behavior instead of the historical overview page.
- Sprint 12 is closed after a no-skip quality gate with 104 fast backend tests, 3 SQL Server/Testcontainers tests, 16 Web tests, 4 Playwright flows, and 4 connected Android tests on a physical device.
- Playwright network traces are disabled because they can retain HttpOnly `Set-Cookie` credentials; screenshots and videos remain failure-only diagnostic artifacts.
- Web administration calls now use the supported versioned Admin APIs, while Android login now uses `/api/v1/auth/login`.
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
