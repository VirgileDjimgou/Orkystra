# Orkystra Project Workflow

## Objective

Advance Orkystra as a maintainable smart logistics platform without losing continuity between work sessions.

## Required Project Memory

Always read these files before implementing project work:

- `AGENTS.md`
- `CURRENT_STATE.md`
- `PROJECT_STATUS.md`
- `IMPLEMENTATION_ROADMAP.md`
- `docs/decisions.md`
- `docs/operations/post-release-candidate-checkpoint.md`

Read additional architecture or operations files only when the current sprint touches them.

## Canonical Continuation Command

When the user writes:

`Continue the Orkystra project.`

interpret it as:

1. Read the required project memory files.
2. Check `git status`.
3. Determine the next unfinished sprint or the next unfinished release step.
4. Inspect only the relevant code and documentation.
5. Implement one bounded sprint.
6. Run the relevant verification for the touched components.
7. Update `CURRENT_STATE.md`, `PROJECT_STATUS.md`, and `IMPLEMENTATION_ROADMAP.md`.
8. Stop after the sprint is complete and verified.

## Optional Batch Command

When the user writes:

`Continue the Orkystra project for 5 sprints.`

repeat the same loop for up to 5 consecutive unfinished sprints, but stop early if:

- a human decision is needed
- a secret or paid service is needed
- a destructive migration is needed
- a build or test failure cannot be repaired cleanly

## Mandatory Rules

- Preserve the existing architecture and provider boundaries.
- Do not rewrite unrelated subsystems.
- Work from the next unfinished sprint, not from memory alone.
- Prefer the smallest useful vertical slice.
- Keep domain logic out of UI and infrastructure.
- Run the relevant tests and builds after meaningful changes.
- Update documentation when behavior, contracts, operations, or architecture change.
- Never count a sprint that only polishes one local UI file.

## Definition Of Done

A sprint is complete only when:

- touched components build
- touched tests pass, or a limitation is documented explicitly
- docs are updated where needed
- `CURRENT_STATE.md` is current
- `PROJECT_STATUS.md` is current
- `IMPLEMENTATION_ROADMAP.md` is current

## Current Focus

The repository is in the post-release-candidate maturity phase.
The next planned sprint is Sprint 195: release-aware evidence preset refinement.
