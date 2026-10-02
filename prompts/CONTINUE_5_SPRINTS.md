# Smart Logistic Continue - 5 Sprint Batch

Use this prompt when you want a longer supervised continuation run.

```text
Smart Logistic continue.

Read:
- prompts/PROJECT_PROMPT.md
- constitution/SMART_LOGISTICS_TWIN_CONSTITUTION_v2.md
- IMPLEMENTATION_ROADMAP.md
- PROJECT_STATUS.md
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

## Expected Output

At the end of the run, report:

- completed sprint numbers
- files changed
- verification commands and outcomes
- the next sprint to run later
- any blocker or human decision needed
