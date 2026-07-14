# Orkystra Project Instructions

This repository is developed through sprint-based implementation sessions. Do not try to build the full Smart Logistics Twin platform in a single pass.

## Project Memory

Before implementing project work, read:

- `constitution/SMART_LOGISTICS_TWIN_CONSTITUTION_v2.md`
- `IMPLEMENTATION_ROADMAP.md`
- `PROJECT_STATUS.md`
- `docs/operations/post-release-candidate-checkpoint.md`
- `docs/operations/support-handoff-and-reproduction.md`
- `docs/methodology/SPRINT_OPERATING_MODEL.md`
- `docs/methodology/SPRINT_PROTOCOL.md`
- `prompts/AUTOPILOT.md`

## Command Alias

When the user writes:

```text
Smart Logistic continue
```

Treat it as a request to run the 5-sprint batch protocol from:

```text
prompts/CONTINUE_5_SPRINTS.md
```

## Maturity Checkpoint

When the user asks for the current OSS readiness posture, also read:

- `docs/operations/post-release-candidate-checkpoint.md`
- `infrastructure/scripts/verify-oss-readiness.ps1`

When the user asks to file, reproduce, or triage a bug, also read:

- `docs/operations/support-handoff-and-reproduction.md`
- `.github/ISSUE_TEMPLATE/bug_report.md`

Treat that checkpoint as the current source of truth for supported posture, unsupported areas, and the remaining estimate.

## Execution Rules

- Execute at most 5 consecutive unfinished sprints.
- Work one sprint at a time.
- Do not start the next sprint until the current sprint is implemented, verified, documented, and reflected in `PROJECT_STATUS.md` and `IMPLEMENTATION_ROADMAP.md`.
- Stop early if a build/test failure cannot be fixed, a human decision is needed, or the next sprint requires secrets, paid external services, or destructive migration work.
- Preserve the existing architecture: provider pattern, DTO boundaries, simulation-first/reality-ready design, and domain logic outside UI/infrastructure.
- Use focused tests for touched behavior.
- Prefer small vertical slices over broad refactors.

## Verification

Run the relevant checks for touched components:

- Backend: `dotnet build backend/Orkystra.slnx --configuration Release /p:UseSharedCompilation=false /nodeReuse:false`
- Backend tests: `dotnet test backend/Orkystra.slnx --configuration Release /p:UseSharedCompilation=false /nodeReuse:false`
- Frontend: `npm run build` from `frontend/web`
- Python: `python -m unittest discover python-services/tests` and `python -m compileall python-services`

Do not claim a sprint is completed unless the relevant verification has passed or a limitation is explicitly documented.
