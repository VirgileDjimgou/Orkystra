# Orkystra Copilot Instructions

This repository is developed through sprint-based autonomous sessions. Do not try to build the full Smart Logistics Twin platform in a single pass.

## Project Memory

Before implementing project work, read:

- `constitution/SMART_LOGISTICS_TWIN_CONSTITUTION_v2.md`
- `IMPLEMENTATION_ROADMAP.md`
- `PROJECT_STATUS.md`
- `docs/methodology/VIBE_CODING_OPERATING_MODEL.md`
- `docs/methodology/SPRINT_PROTOCOL.md`
- `prompts/CODEX_AUTOPILOT.md`

## Command Alias

When the user writes:

```text
Smart Logistic continue
```

Treat it as a request to run the 5-sprint batch protocol from:

```text
prompts/SMART_LOGISTIC_CONTINUE_5_SPRINTS.md
```

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
