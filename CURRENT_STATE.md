# Current State

Last updated: 2026-07-14

## Canonical Continuation Command

`Continue the Orkystra project.`

Optional supervised batch command:

`Continue the Orkystra project for 5 sprints.`

## Current Sprint

Sprint 200 - Release reproducibility and verification completeness

Status: Completed

## Previous Sprint

Sprint 199 - Release tag preparation and CI verification

Status: Completed

## Current Product Posture

Orkystra is currently a production-usable open-source release candidate for local and self-hosted evaluation, with the maturity work centered on supportability, evidence quality, and repeatable maintainer handoff. The release preflight now passes cleanly, all CI jobs are verified locally, and the release-required memory files are tracked so the candidate can be reproduced from a clean clone.

## Recently Completed

- Sprint 200: Fixed release reproducibility by un-ignoring the release-required memory files (`prompts/`, `constitution/`, `PROJECT_STATUS.md`, `IMPLEMENTATION_ROADMAP.md`) in `.gitignore`, keeping the genuinely-private authoring artifacts (`docs/blueprints/`, `docs/methodology/`, `docs/adr/0001-project-execution-model.md`) ignored. Added `Orkystra.Integration.Tests` to `backend/Orkystra.slnx` so the manifest's `dotnet test backend/Orkystra.slnx` command now covers all 158 backend tests (143 domain + 15 integration) instead of only the 143 domain tests. Verified release preflight passes and all three component checks are green.

## Remaining Risks

- The CI workflow has not yet been validated on a real remote GitHub Actions push.
- End-to-end browser validation still depends on a running local dev server in CI.
- `docs/blueprints/`, `docs/methodology/`, and `docs/adr/0001-project-execution-model.md` remain gitignored even though `PROJECT_STATUS.md` references the blueprint as a source of truth; this is a minor dangling-reference risk, not release-blocking.

## Relevant Files For The Next Sprint

- `.github/workflows/ci.yml` (needs monitoring on first real remote push)
- `docs/operations/release-candidate.md`
- `docs/operations/releases/v0.1.0-rc.1-publish-checklist.md`
- `infrastructure/scripts/release-preflight.ps1`

## Decisions That Still Matter

- Continue working in bounded sprints instead of broad autonomous rewrites.
- Treat `PROJECT_STATUS.md` and `IMPLEMENTATION_ROADMAP.md` as historical memory.
- Use `CURRENT_STATE.md` as the compact restart brief for new sessions.
- Keep release-candidate maturity work focused on cross-cutting product value rather than isolated local polish.

## Next Exact Action

The release preflight passes and all CI jobs are verified locally. The release-required files are now tracked. The next step is to commit the release-memory files, push the CI workflow to a remote, and confirm all three jobs pass on an actual GitHub Actions push, then cut the `v0.1.0-rc.1` tag from a clean tree.
