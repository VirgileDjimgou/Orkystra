# Sprint Autopilot

The canonical orchestration implementation is `scripts/agent/sprint_orchestrator.py`; PowerShell wrappers call it. It persists execution state in `.agent/PROJECT_STATE.json` and uses `.agent/sprint.lock.json` to prevent Codex and OpenCode from implementing two sprints in the same working tree.

## Atomic command

`Start Next Sprint` means one sprint only:

1. run `scripts/sprint-runner.ps1 -Action Start`;
2. read the selected sprint contract and applicable directory instructions;
3. implement only that sprint, including required tests and documentation;
4. run validation, then `-Action Validate`;
5. update acceptance criteria, roadmap/state/handoff/report, checkpoint locally if permitted;
6. run `-Action Complete -Gate <evidence> -Checkpoint <summary>`;
7. stop. Do not start the next sprint in the same context.

The runner rejects a dirty worktree, `STOP`, unresolved `HUMAN_REQUIRED.json`, an active/stale lock, unavailable contract, invalid state, or no eligible sprint. It selects the smallest numbered non-`DONE`/non-`SUPERSEDED` sprint and ignores `sprints/archive/`.

## Batch command

`Start Next Sprints N` accepts only `1..10`. `scripts/sprint-batch-runner.ps1 -Count N` records the bounded batch then starts the first atomic sprint. Each subsequent iteration requires a fresh agent context and the same atomic runner. The single-sprint runner is authoritative; a failed gate stops the batch.

## Stop and human gates

After each failed repair, record it with `-Action Fail`. On the third failure, the orchestrator writes `.agent/HUMAN_REQUIRED.json` with sprint, gate, failure, repairs, files, required decision, and safe resume command. Resolve only with explicit human direction using `-Action ResolveGate -Note <decision>`. `-Action Stop` releases the lock and records a stop reason.

The runner never pushes, deploys, merges, weakens tests, changes acceptance criteria silently, or steals a stale lock.
