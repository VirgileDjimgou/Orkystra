# Release Candidate Guide

This guide defines the final OSS release-candidate posture for Orkystra.

## Purpose

The release candidate should be good enough for a third party to install, verify, and understand without reading sprint chat history. It is still a self-hosted OSS release candidate, not a blanket production guarantee.

## Supported deployment posture

The supported posture for the release candidate is:

- local single-tenant development with SQLite persistence
- local or self-hosted deployment with Docker Compose
- self-hosted PostgreSQL persistence for multi-session runs
- MQTT-backed event flow through the included broker
- AI and optimization services running through the packaged Python containers or local fallback providers

The following are not first-class support targets yet:

- managed SaaS multi-tenancy
- external identity-provider integration
- enterprise secret-manager integration
- zero-downtime upgrade automation
- long-lived HA/DR topologies

## Known limitations

- API-key authentication is the current security baseline; there is no external identity provider yet.
- Tenant isolation is functional, but the stack is still centered on a single self-hosted deployment pattern.
- SQLite remains the simplest local path; PostgreSQL is the better supported choice for multi-session self-hosting.
- The event backbone, audit trail, and operational persistence are designed for local and self-hosted use, not a full enterprise compliance platform.
- AI and optimization workflows are bounded and fall back locally when their Python services are unavailable.
- Live connector support depends on local configuration and upstream health; demo fallback paths still exist by design.
- The 3D control tower is an operator visualization, not the authoritative source of warehouse or transport truth.

## Versioning flow

Use semantic versioning for releases:

- `MAJOR` for incompatible product or contract changes
- `MINOR` for additive, user-visible capability
- `PATCH` for fixes and release-candidate hardening

For the release-candidate track, tag builds as:

- `vMAJOR.MINOR.PATCH-rc.1`
- `vMAJOR.MINOR.PATCH-rc.2`
- and so on until the candidate is stable

Release flow:

1. Finish the sprint and update `PROJECT_STATUS.md` and `IMPLEMENTATION_ROADMAP.md`.
2. Run `powershell -ExecutionPolicy Bypass -File infrastructure/scripts/release-preflight.ps1 -Version vMAJOR.MINOR.PATCH-rc.N` to confirm the release docs, prompts, and worktree posture are aligned before tagging.
3. Confirm the supported deployment posture and known limitations are documented.
4. Run the full release verification checklist.
5. Cut a clean release-candidate tag from `main`.
6. Publish the artifact set, manifest, and release notes together (`CHANGELOG.md`, `docs/operations/releases/vMAJOR.MINOR.PATCH-rc.N-manifest.json`, and `docs/operations/releases/vMAJOR.MINOR.PATCH-rc.N.md`).
7. If a fix is needed, apply it on `main`, rerun the preflight and checklist, and cut the next `-rc.N` tag.

## Release checklist

- [ ] `dotnet build backend/Orkystra.slnx --configuration Release /p:UseSharedCompilation=false /nodeReuse:false`
- [ ] `dotnet test backend/Orkystra.slnx --configuration Release /p:UseSharedCompilation=false /nodeReuse:false`
- [ ] `npm run build` in `frontend/web`
- [ ] `cd python-services && python -m pytest`
- [ ] `python -m compileall python-services`
- [ ] `docker compose -f infrastructure/docker-compose.stack.yml up -d --build` starts the packaged stack cleanly
- [ ] `docs/operations/smoke-test-checklist.md` passes for the supported release posture
- [ ] `CHANGELOG.md` contains the release entry for the candidate version
- [ ] `docs/operations/releases/vMAJOR.MINOR.PATCH-rc.N.md` exists and mentions the known limitations and supported deployment posture
- [ ] `docs/operations/releases/vMAJOR.MINOR.PATCH-rc.N-manifest.json` exists and matches the exact artifact set to publish
- [ ] `docs/operations/releases/vMAJOR.MINOR.PATCH-rc.N-publish-checklist.md` exists and reflects the final manual handoff sequence
- [ ] `powershell -ExecutionPolicy Bypass -File infrastructure/scripts/release-preflight.ps1 -Version vMAJOR.MINOR.PATCH-rc.N` passes without `-AllowDirtyWorktree`
- [ ] The version tag and release artifacts match the documented versioning flow

## Support boundaries

The release candidate is intended to be:

- installable by a third party
- understandable from the docs alone
- resilient enough for local demos and self-hosted evaluation
- explicit about what is and is not covered

It is not yet intended to promise:

- fully managed production operations
- enterprise SSO and governance features
- opaque one-click cloud hosting
- unlimited connector compatibility

For support retries and reset-driven issue reproduction after this release-candidate block, use:

- [support-handoff-and-reproduction.md](support-handoff-and-reproduction.md)
- [support-packet-review.md](support-packet-review.md)
- [support-packet-refresh-loop.md](support-packet-refresh-loop.md)

For the next maturity checkpoint after this release-candidate block, see [post-release-candidate-checkpoint.md](post-release-candidate-checkpoint.md). It captures the current supported posture, unsupported areas, and the consolidated verification pass that now acts as the open-source maturity gate.
