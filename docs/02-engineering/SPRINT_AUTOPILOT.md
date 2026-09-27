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
## Visible local status

The canonical runner records sprint selection and validation state; it does not itself run an AI coding worker in the background. To see the current state, working-tree changes and the current sprint journal in a terminal, run:

```powershell
pwsh -ExecutionPolicy Bypass -File scripts/sprint-dashboard.ps1 -Watch
```

Each active sprint must append factual milestones to `.runtime/<sprint>-progress.log`. A sprint is only genuinely executing while Codex is processing a turn or an explicitly started validation command is running; a lock alone is not evidence of background implementation.

## Interactive lifecycle

The normal lifecycle is `IDLE -> RUNNING -> VALIDATING -> DONE`. If a Codex turn ends before validation completes, it must execute `-Action Pause -Note <factual handoff>`. That transition records the sprint as `PARTIAL`, changes execution to `IDLE`, releases the lock, and makes the next `Start Next Sprint` resume the same sprint.

Use `scripts/sprint-progress.ps1 -Status <status> -Message <message>` for factual milestones. `scripts/sprint-dashboard.ps1 -Watch` displays the remaining sprint count, last activity, lock, working tree and current progress log. No state may claim continuous background work unless an actual worker process is running and identifiable.
