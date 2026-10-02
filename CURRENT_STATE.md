# Current State

Last updated: 2026-10-02

## Canonical Continuation Command

`Continue the Orkystra project.`

Optional supervised batch command:

`Continue the Orkystra project for 5 sprints.`

## Current Sprint

Sprint 201 - Commit release-memory files and first real remote CI push

Status: Completed with a documented divergence blocker

## Previous Sprint

Sprint 200 - Release reproducibility and verification completeness

Status: Completed

## Current Product Posture

Orkystra is a production-usable open-source release candidate for local and self-hosted evaluation, with the maturity work centered on supportability, evidence quality, and repeatable maintainer handoff. The release-memory files are now committed (4453898) and the release preflight passes on a clean tree. The first real remote CI push could not be confirmed on remote `main` because `origin/main` was force-updated to an unrelated lineage (`ea529f6`, the FleetOps hosted-demo history, dated 2026-09-30) with no common ancestor. The Orkystra release-memory commit is preserved on remote branch `release/rc-preflight-cleanup`.

## Recently Completed

- Sprint 201: Committed the release-required memory files (`.gitignore` un-ignore, `AGENTS.md`, `CURRENT_STATE.md`, `README.md`, `backend/Orkystra.slnx`, `IMPLEMENTATION_ROADMAP.md`, `PROJECT_STATUS.md`, `constitution/`, `prompts/`) as commit `4453898`. Verified the release preflight passes both with `-AllowDirtyWorktree` before the commit and on the clean tree after it. The push to `origin/main` was rejected because the remote main was force-updated to an unrelated FleetOps lineage; per user decision the commit was pushed to the new remote branch `release/rc-preflight-cleanup` instead of rewriting remote history. Remote CI confirmation is deferred until the canonical lineage is decided.
- Sprint 200: Fixed release reproducibility by un-ignoring the release-required memory files (`prompts/`, `constitution/`, `PROJECT_STATUS.md`, `IMPLEMENTATION_ROADMAP.md`) in `.gitignore`, keeping the genuinely-private authoring artifacts (`docs/blueprints/`, `docs/methodology/`, `docs/adr/0001-project-execution-model.md`) ignored. Added `Orkystra.Integration.Tests` to `backend/Orkystra.slnx` so the manifest's `dotnet test backend/Orkystra.slnx` command now covers all 158 backend tests (143 domain + 15 integration) instead of only the 143 domain tests. Verified release preflight passes and all three component checks are green.

## Remaining Risks

- `origin/main` is now an unrelated FleetOps lineage (`ea529f6`) with its own failing CI run; the local Orkystra sprint-200/201 lineage has no common ancestor with it. A human decision is required on which lineage is canonical before any further push or tag work.
- The local Orkystra release-memory work is only preserved on the remote branch `release/rc-preflight-cleanup`, not on `main`.
- The remote FleetOps `main` CI is currently failing on its latest push (`ea529f6`), independent of the Orkystra lineage.
- End-to-end browser validation still depends on a running local dev server in CI.
- `docs/blueprints/`, `docs/methodology/`, and `docs/adr/0001-project-execution-model.md` remain gitignored even though `PROJECT_STATUS.md` references the blueprint as a source of truth; this is a minor dangling-reference risk, not release-blocking.

## Relevant Files For The Next Sprint

- `CURRENT_STATE.md` (this restart brief)
- `docs/decisions.md` (lineage divergence decision recorded)
- Remote branch `release/rc-preflight-cleanup` (preserved Orkystra commit `4453898`)
- `docs/operations/releases/v0.1.0-rc.1-publish-checklist.md` (tag flow, blocked on lineage decision)

## Decisions That Still Matter

- Continue working in bounded sprints instead of broad autonomous rewrites.
- Treat `PROJECT_STATUS.md` and `IMPLEMENTATION_ROADMAP.md` as historical memory.
- Use `CURRENT_STATE.md` as the compact restart brief for new sessions.
- Keep release-candidate maturity work focused on cross-cutting product value rather than isolated local polish.
- Do not rewrite remote history; preserve diverged work on a side branch until a human decides the canonical lineage.

## Next Exact Action

A human decision is required: choose whether the canonical lineage is the local Orkystra release-candidate history (restore/repoint `main`, e.g. from `release/rc-preflight-cleanup`) or the remote FleetOps hosted-demo history (adopt it locally and archive the Orkystra commit). After that decision, confirm CI on the chosen lineage and only then cut the `v0.1.0-rc.1` tag from a clean tree.
