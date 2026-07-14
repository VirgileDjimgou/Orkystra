# Orkystra Copilot Instructions

This repository is developed through bounded sprint sessions. Do not try to advance the whole platform in one pass.

## Read First

Before implementing project work, read:

- `AGENTS.md`
- `CURRENT_STATE.md`
- `PROJECT_STATUS.md`
- `IMPLEMENTATION_ROADMAP.md`
- `docs/decisions.md`
- `docs/operations/post-release-candidate-checkpoint.md`

Read additional files only when they are relevant to the current sprint.

## Canonical Continuation Command

When the user writes:

```text
Continue the Orkystra project.
```

you should:

1. Read the project memory files listed above.
2. Inspect `git status`.
3. Determine the next unfinished sprint or release step.
4. Implement one bounded sprint.
5. Run relevant verification for touched components.
6. Update `CURRENT_STATE.md`, `PROJECT_STATUS.md`, and `IMPLEMENTATION_ROADMAP.md`.
7. Stop after that sprint is complete.

## Optional Batch Command

When the user writes:

```text
Continue the Orkystra project for 5 sprints.
```

you may execute up to 5 consecutive unfinished sprints, but still close each sprint independently before starting the next one.

Stop early if a human decision is needed, a secret is needed, a destructive migration is required, or a failing build/test cannot be repaired cleanly.
