# Current State

Last updated: 2026-07-14

## Canonical Continuation Command

`Continue the Orkystra project.`

Optional supervised batch command:

`Continue the Orkystra project for 5 sprints.`

## Current Sprint

Sprint 197 - CI workflow for Python services, backend, and frontend

Status: Completed

## Previous Sprint

Sprint 196 - Evidence preset documentation and smoke verification

Status: Completed

## Current Product Posture

Orkystra is currently a production-usable open-source release candidate for local and self-hosted evaluation, with the maturity work centered on supportability, evidence quality, and repeatable maintainer handoff.

## Recently Completed

- Sprint 197: Created `.github/workflows/ci.yml` with three independent jobs: `python-services` (Python 3.12 + pytest, 17 tests passing), `backend` (dotnet test on Domain.Tests and Integration.Tests, 158 tests passing), and `frontend` (npm ci + npm run build, 39 modules). All jobs verified locally. The workflow triggers on push/PR to main.

## Remaining Risks

- Python service dependencies are declared but not fully exercised in CI on every maturity pass.
- End-to-end browser validation still depends on a running local dev server.

## Relevant Files For The Next Sprint

- `.github/workflows/ci.yml` (new — needs monitoring on first real push)
- `docs/operations/support-packet-review.md`
- `docs/operations/support-handoff-and-reproduction.md`
- `docs/operations/support-packet-lifecycle-summary.md`

## Decisions That Still Matter

- Continue working in bounded sprints instead of broad autonomous rewrites.
- Treat `PROJECT_STATUS.md` and `IMPLEMENTATION_ROADMAP.md` as historical memory.
- Use `CURRENT_STATE.md` as the compact restart brief for new sessions.
- Keep release-candidate maturity work focused on cross-cutting product value rather than isolated local polish.

## Next Exact Action

The CI workflow is in place but has never run on an actual push. The next maturity increment should push the workflow to a remote and confirm all three jobs pass, then either add CI coverage for the remaining gap (Playwright E2E tests) or begin the formal release tag.
