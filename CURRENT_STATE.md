# Current State

Last updated: 2026-07-14

## Canonical Continuation Command

`Continue the Orkystra project.`

Optional supervised batch command:

`Continue the Orkystra project for 5 sprints.`

## Current Sprint

Sprint 196 - Evidence preset documentation and smoke verification

Status: Planned

## Previous Sprint

Sprint 195 - Release-aware evidence preset refinement

Status: Completed

## Current Product Posture

Orkystra is currently a production-usable open-source release candidate for local and self-hosted evaluation, with the maturity work centered on supportability, evidence quality, and repeatable maintainer handoff.

## Recently Completed

- Added reusable capture presets grouped by packet class and release posture.
- Extended the support panel capture block with preset identity and reusable preset actions.
- Extended lifecycle and maintainer handoff outputs so capture presets are emitted consistently.
- Extended validation to enforce capture preset markdown and JSON contract fields.
- Re-verified frontend production build, PowerShell parser checks, and a local packet packaging smoke flow with preset fields present in lifecycle and handoff outputs.

## Remaining Risks

- Python service dependencies are declared but not fully exercised in CI on every maturity pass.
- End-to-end browser validation still depends on a running local dev server.
- The formal release tag and publication flow still need final execution.
- The current maturity work is dense in support tooling and still needs the last cleanup passes that reduce operator effort.

## Relevant Files For The Next Sprint

- `docs/operations/support-packet-review.md`
- `docs/operations/support-handoff-and-reproduction.md`
- `docs/operations/support-packet-lifecycle-summary.md`

## Decisions That Still Matter

- Continue working in bounded sprints instead of broad autonomous rewrites.
- Treat `PROJECT_STATUS.md` and `IMPLEMENTATION_ROADMAP.md` as historical memory.
- Use `CURRENT_STATE.md` as the compact restart brief for new sessions.
- Keep release-candidate maturity work focused on cross-cutting product value rather than isolated local polish.

## Next Exact Action

Document the reusable capture preset workflow in the support operations docs and run a thin smoke pass that confirms preset sections remain present in lifecycle and maintainer handoff outputs.
