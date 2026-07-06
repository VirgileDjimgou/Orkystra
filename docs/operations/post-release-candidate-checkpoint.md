# Post-Release-Candidate Maturity Checkpoint

This checkpoint summarizes the supported open-source posture after the current release-candidate block and gives maintainers a compact way to judge whether the repository is ready for heavier public evaluation.

## Supported posture

Orkystra is considered supported for:

- local single-tenant development with SQLite
- Docker Compose self-hosted evaluation
- PostgreSQL-backed self-hosting for multi-session runs
- MQTT-backed event flow through the packaged broker
- packaged AI and optimization services, with deterministic local fallback when needed
- operator-visible observability for persistence, event backbone, audit, GPS, and support bundles

## Not yet supported

Orkystra is not yet positioned as a full promise for:

- managed SaaS multi-tenancy
- external identity-provider integration
- centralized secret-manager integration
- zero-downtime upgrades
- HA/DR topologies
- unlimited connector compatibility

## Consolidated verification

Run the following checks as the practical maturity gate:

```powershell
dotnet build backend/Orkystra.slnx --configuration Release /p:UseSharedCompilation=false /nodeReuse:false
dotnet test backend/Orkystra.slnx --configuration Release /p:UseSharedCompilation=false /nodeReuse:false
cd frontend/web
npm run build
cd ../../python-services
python -m pytest
python -m compileall python-services
```

For a packaged self-host evaluation, also confirm:

- `infrastructure/scripts/bring-up-selfhost.ps1` succeeds on a clean local machine with Docker available
- `infrastructure/scripts/reset-demo-state.ps1 -Rebootstrap` can rebuild the demo state
- `infrastructure/scripts/export-support-bundle.ps1 -ApiKey ...` can capture a useful support artifact
- `GET /observability/support-bundle` returns aggregated support evidence

For incident intake and reproduction, use:

- [docs/operations/support-handoff-and-reproduction.md](support-handoff-and-reproduction.md)
- [.github/ISSUE_TEMPLATE/bug_report.md](../../.github/ISSUE_TEMPLATE/bug_report.md)
- [docs/operations/support-packet-refresh-loop.md](support-packet-refresh-loop.md)

## Current assessment

The repository has crossed the line from prototype behavior into a credible self-hostable OSS product shell, but it still needs the next maturity block before broader commercialization or heavier-scale deployment claims are fair.

## Refreshed estimate

At this checkpoint, the remaining path to a genuinely mature and comfortably commercializable product is roughly:

- 1 to 7 more consistent sprints

That estimate assumes we continue to prioritize cross-cutting product maturity, supportability, and release hygiene over narrow UI polish or isolated feature additions.
