# Autopilot Prompt

## Recommended Universal Prompt

The recommended repository prompt is:

- `prompts/ORKYSTRA_UNIVERSAL_AGENT_PROMPT_FR.md`

If the agent can read files before acting, start by loading that prompt. It matches the current project state and is the canonical continuation entrypoint.

Use this prompt at the beginning of a new implementation session:

```text
Continue Smart Logistics Twin.

Read:
- prompts/ORKYSTRA_UNIVERSAL_AGENT_PROMPT_FR.md
- constitution/SMART_LOGISTICS_TWIN_CONSTITUTION_v2.md
- IMPLEMENTATION_ROADMAP.md
- PROJECT_STATUS.md
- docs/operations/post-release-candidate-checkpoint.md
- docs/operations/support-handoff-and-reproduction.md
- docs/methodology/SPRINT_OPERATING_MODEL.md

Determine the current sprint.
Implement only the next unfinished sprint or the next unfinished task inside the current sprint.

Rules:
- Do not build the whole platform in one pass.
- Preserve the provider pattern.
- Preserve the simulation-first and reality-ready architecture.
- Keep domain logic out of the UI and infrastructure.
- Add or update tests for touched logic.
- Update documentation when architecture, contracts, events, or behavior change.
- Update PROJECT_STATUS.md and IMPLEMENTATION_ROADMAP.md before stopping.
- If a build or test fails, analyze it, fix it, rerun it, and continue within the same sprint.
- Stop when the sprint task is complete and verified.
```

## Five-Sprint Batch Prompt

Use this prompt when you want a longer supervised run:

```text
Smart Logistic continue.

Read:
- prompts/ORKYSTRA_UNIVERSAL_AGENT_PROMPT_FR.md
- constitution/SMART_LOGISTICS_TWIN_CONSTITUTION_v2.md
- IMPLEMENTATION_ROADMAP.md
- PROJECT_STATUS.md
- docs/operations/post-release-candidate-checkpoint.md
- docs/operations/support-handoff-and-reproduction.md
- docs/methodology/SPRINT_OPERATING_MODEL.md
- docs/methodology/SPRINT_PROTOCOL.md

Determine the current sprint.
Execute up to 5 consecutive unfinished sprints, one sprint at a time.

For each sprint:
1. Identify the next unfinished sprint from IMPLEMENTATION_ROADMAP.md.
2. State the sprint goal and the smallest useful vertical slice.
3. Implement only that sprint's scope.
4. Run the relevant build and tests for touched components.
5. Fix failures before moving on.
6. Update documentation if behavior, architecture, contracts, or operations changed.
7. Update PROJECT_STATUS.md and IMPLEMENTATION_ROADMAP.md before starting the next sprint.

Stop early if:
- a build or test failure cannot be repaired in the current session
- a product or architecture decision needs human approval
- the next sprint would require secrets, paid external services, or destructive migration work
- 5 sprints have been completed

Never merge multiple sprint goals into one large refactor.
Never skip verification just to reach the 5-sprint target.
```

Short alias:

```text
Smart Logistic continue
```

The alias means: read the project memory, then run the five-sprint batch prompt above.

## Maturity Checkpoint

When the user asks for the consolidated OSS readiness pass, also read:

- `docs/operations/post-release-candidate-checkpoint.md`
- `infrastructure/scripts/verify-oss-readiness.ps1`

When the user asks to file, reproduce, or triage an issue, also read:

- `docs/operations/support-handoff-and-reproduction.md`
- `.github/ISSUE_TEMPLATE/bug_report.md`

That checkpoint is the current source of truth for supported posture, unsupported areas, and the current production-usable estimate.

## Recovery Prompt

Use this if a session becomes confused:

```text
Recover project context.

Ignore previous assumptions.
Read the universal prompt, constitution, roadmap, status, and the latest git diff.
Identify the exact current sprint and the files changed in this session.
Repair any broken build, tests, docs, or status updates.
Do not start a new sprint until the current sprint is stable.
```

## Review Prompt

Use this before accepting a sprint:

```text
Review the current sprint as a senior architect.

Check:
- domain boundaries
- provider abstraction
- event contracts
- test coverage
- documentation updates
- hidden coupling
- UI dependency leaks
- simulation determinism
- migration risks

Return only findings, risks, and required fixes.
```
