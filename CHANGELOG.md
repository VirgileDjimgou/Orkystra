# Changelog

All notable changes to Orkystra are documented in this file.

The format is intentionally lightweight and release-oriented for this repository stage.

## [Unreleased]

- No unreleased entries are recorded yet.

## [0.1.0-rc.1] - 2026-07-02

First open-source release candidate for self-hosted evaluation and local operational demos.

### Added

- .NET 9 backend API with warehouse, transport, simulation, provider-catalog, observability, bootstrap, GPS, AI, and optimization workflows.
- Vue 3 control-tower frontend with warehouse twin, transport board, AI workflow, provider runtime editing, and operational trace surfaces.
- Python AI and optimization services with deterministic fallback behavior for local-first operation.
- MQTT-backed event backbone plus operational persistence support for SQLite and PostgreSQL.
- Installation, contribution, release-candidate, and smoke-test documentation for third-party setup and evaluation.

### Changed

- Open-source packaging posture now includes explicit supported deployment boundaries and known limitations.
- Local configuration guidance now covers API keys, provider secrets, persistence selection, and AI-provider modes.
- Release governance now includes a root changelog and a versioned release-notes artifact for candidate publishing.

### Verification

- `dotnet test backend/Orkystra.slnx --configuration Release /p:UseSharedCompilation=false /nodeReuse:false`
- `npm run build` in `frontend/web`
- `python -m compileall python-services`

### Notes

- Detailed candidate notes: `docs/operations/releases/v0.1.0-rc.1.md`
- Support boundaries and checklist: `docs/operations/release-candidate.md`
