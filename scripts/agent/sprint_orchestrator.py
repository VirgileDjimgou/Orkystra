#!/usr/bin/env python3
"""Canonical state and locking protocol for FleetOps atomic sprint execution.

This tool deliberately prepares, records, and closes one sprint. It never writes
product code, commits, pushes, deploys, or silently starts a later sprint.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import pathlib
import subprocess
import sys
from typing import Any


ROOT = pathlib.Path(__file__).resolve().parents[2]
STATE_PATH = ROOT / ".agent" / "PROJECT_STATE.json"
LOCK_PATH = ROOT / ".agent" / "sprint.lock.json"
HUMAN_REQUIRED_PATH = ROOT / ".agent" / "HUMAN_REQUIRED.json"
STOP_PATHS = (ROOT / "STOP", ROOT / ".agent" / "STOP")
MAX_BATCH_SIZE = 10
MAX_REPAIR_ATTEMPTS = 3


class SprintGateError(RuntimeError):
    """A condition that must stop atomic sprint execution."""


def utc_now() -> dt.datetime:
    return dt.datetime.now(dt.timezone.utc).replace(microsecond=0)


def iso_now() -> str:
    return utc_now().isoformat()


def load_json(path: pathlib.Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def save_json(path: pathlib.Path, value: dict[str, Any]) -> None:
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def load_state(root: pathlib.Path = ROOT) -> dict[str, Any]:
    path = root / ".agent" / "PROJECT_STATE.json"
    state = load_json(path)
    if state.get("schemaVersion") != 2:
        raise SprintGateError("PROJECT_STATE.json must use schemaVersion 2.")
    return state


def save_state(state: dict[str, Any], root: pathlib.Path = ROOT) -> None:
    state["updatedAtUtc"] = iso_now()
    save_json(root / ".agent" / "PROJECT_STATE.json", state)


def sprint_number(sprint: str) -> int:
    try:
        return int(sprint.removeprefix("SPRINT-"))
    except ValueError as error:
        raise SprintGateError(f"Invalid sprint identifier: {sprint}") from error


def active_sprint_file(root: pathlib.Path, sprint: str) -> pathlib.Path:
    candidates = sorted((root / "sprints").glob(f"{sprint}-*.md"))
    if len(candidates) != 1:
        raise SprintGateError(f"Expected exactly one active sprint file for {sprint}; found {len(candidates)}.")
    return candidates[0]


def next_eligible_sprint(state: dict[str, Any], root: pathlib.Path = ROOT) -> str | None:
    for sprint, status in sorted(state["sprints"].items(), key=lambda item: sprint_number(item[0])):
        if status in {"DONE", "SUPERSEDED"}:
            continue
        if status in {"BLOCKED", "HUMAN_REQUIRED"}:
            raise SprintGateError(f"{sprint} is {status}; resolve its human gate before continuing.")
        if status not in {"NOT_STARTED", "IMPLEMENTED_NOT_VALIDATED", "PARTIAL"}:
            raise SprintGateError(f"{sprint} has unsupported execution status {status}.")
        active_sprint_file(root, sprint)
        return sprint
    return None


def git_is_clean(root: pathlib.Path) -> bool:
    result = subprocess.run(
        ["git", "status", "--porcelain"], cwd=root, capture_output=True, text=True, check=True
    )
    return result.stdout.strip() == ""


def read_lock(root: pathlib.Path = ROOT) -> dict[str, Any] | None:
    path = root / ".agent" / "sprint.lock.json"
    return load_json(path) if path.exists() else None


def lock_is_valid(lock: dict[str, Any]) -> bool:
    try:
        return dt.datetime.fromisoformat(lock["expiresAtUtc"]) > utc_now()
    except (KeyError, TypeError, ValueError):
        return True


def assert_start_allowed(root: pathlib.Path, state: dict[str, Any]) -> None:
    for stop_path in (root / "STOP", root / ".agent" / "STOP"):
        if stop_path.exists():
            raise SprintGateError(f"Explicit stop file exists: {stop_path.relative_to(root)}")
    if (root / ".agent" / "HUMAN_REQUIRED.json").exists():
        raise SprintGateError("HUMAN_REQUIRED.json exists; resolve the recorded gate first.")
    lock = read_lock(root)
    if lock:
        owner = lock.get("owner", "unknown")
        if lock_is_valid(lock):
            raise SprintGateError(f"Sprint lock is held by {owner} until {lock.get('expiresAtUtc')}.")
        raise SprintGateError(
            f"Sprint lock held by {owner} is stale; use resolve-gate after human review rather than stealing it."
        )
    if not git_is_clean(root):
        raise SprintGateError("Working tree is not clean; inspect, checkpoint, or resolve unrelated changes before starting.")
    execution = state["execution"]
    if execution["status"] in {"RUNNING", "VALIDATING"}:
        raise SprintGateError("Execution state is active without a usable lock; resolve the gate before starting.")


def start(root: pathlib.Path, owner: str, lease_minutes: int) -> str:
    if lease_minutes < 30 or lease_minutes > 480:
        raise SprintGateError("Lock lease must be between 30 and 480 minutes.")
    state = load_state(root)
    assert_start_allowed(root, state)
    sprint = next_eligible_sprint(state, root)
    if sprint is None:
        raise SprintGateError("No eligible sprint remains.")
    expires = utc_now() + dt.timedelta(minutes=lease_minutes)
    save_json(
        root / ".agent" / "sprint.lock.json",
        {"sprint": sprint, "owner": owner, "acquiredAtUtc": iso_now(), "expiresAtUtc": expires.isoformat()},
    )
    execution = state["execution"]
    execution.update(
        {
            "status": "RUNNING",
            "currentSprint": sprint,
            "attemptCount": 0,
            "currentBlocker": None,
            "agentTool": owner,
            "startedAtUtc": iso_now(),
            "lastActivity": {
                "status": "STARTED",
                "timestampUtc": iso_now(),
                "summary": f"{sprint} selected for visible implementation by {owner}.",
                "log": f".runtime/{sprint.lower().replace('-', '')}-progress.log",
            },
        }
    )
    state["activeSprint"] = sprint
    save_state(state, root)
    return sprint


def set_validating(root: pathlib.Path) -> None:
    state = load_state(root)
    if not read_lock(root):
        raise SprintGateError("Cannot validate without the sprint lock.")
    state["execution"]["status"] = "VALIDATING"
    save_state(state, root)


def unchecked_acceptance_items(sprint_file: pathlib.Path) -> list[str]:
    lines = sprint_file.read_text(encoding="utf-8").splitlines()
    in_section = False
    missing: list[str] = []
    for line in lines:
        if line.startswith("## "):
            in_section = line.strip().lower() in {"## acceptance criteria", "## critères d’acceptation"}
            continue
        if in_section and line.startswith("- [ ]"):
            missing.append(line[5:].strip())
    return missing


def complete(root: pathlib.Path, gate: str, checkpoint: str) -> str | None:
    state = load_state(root)
    lock = read_lock(root)
    sprint = state["execution"].get("currentSprint")
    if not lock or not sprint or lock.get("sprint") != sprint:
        raise SprintGateError("Cannot complete without a matching active sprint lock.")
    missing = unchecked_acceptance_items(active_sprint_file(root, sprint))
    if missing:
        raise SprintGateError(f"Cannot mark {sprint} DONE; unchecked acceptance criteria: {'; '.join(missing)}")
    state["sprints"][sprint] = "DONE"
    state["lastQualityGate"] = {"status": "PASSED", "timestampUtc": iso_now(), "report": ".agent/QUALITY_REPORT.md", "gate": gate}
    state["lastCheckpoint"] = {"timestampUtc": iso_now(), "summary": checkpoint}
    batch = state["execution"]["batch"]
    if batch["requestedCount"]:
        batch["completedCount"] += 1
        if batch["completedCount"] >= batch["requestedCount"]:
            state["execution"]["lastBatch"] = {**batch, "completedAtUtc": iso_now()}
            state["execution"]["batch"] = {"requestedCount": 0, "completedCount": 0}
    next_sprint = next_eligible_sprint(state, root)
    state["activeSprint"] = next_sprint or sprint
    state["execution"].update(
        {"status": "IDLE", "currentSprint": next_sprint, "attemptCount": 0, "currentBlocker": None, "completedAtUtc": iso_now()}
    )
    save_state(state, root)
    (root / ".agent" / "sprint.lock.json").unlink()
    return next_sprint


def fail(root: pathlib.Path, gate: str, observed_failure: str, affected_files: list[str]) -> None:
    state = load_state(root)
    execution = state["execution"]
    sprint = execution.get("currentSprint") or state.get("activeSprint")
    attempts = int(execution.get("attemptCount", 0)) + 1
    blocker = {"sprint": sprint, "gate": gate, "observedFailure": observed_failure, "affectedFiles": affected_files, "attempts": attempts}
    execution.update({"attemptCount": attempts, "currentBlocker": blocker, "status": "FAILED"})
    if attempts >= MAX_REPAIR_ATTEMPTS:
        execution["status"] = "HUMAN_REQUIRED"
        state["sprints"][sprint] = "HUMAN_REQUIRED"
        save_json(
            root / ".agent" / "HUMAN_REQUIRED.json",
            {
                **blocker,
                "attemptedRepairs": attempts,
                "decisionRequired": "Review the failed gate and provide an explicit safe next action.",
                "safeResumeCommand": "Start Next Sprint after resolving this gate with scripts/agent/sprint_orchestrator.py resolve-gate.",
                "createdAtUtc": iso_now(),
            },
        )
    save_state(state, root)


def stop(root: pathlib.Path, reason: str) -> None:
    state = load_state(root)
    state["execution"].update({"status": "STOPPED", "currentBlocker": {"reason": reason, "stoppedAtUtc": iso_now()}})
    save_state(state, root)
    lock_path = root / ".agent" / "sprint.lock.json"
    if lock_path.exists():
        lock_path.unlink()


def pause(root: pathlib.Path, note: str, cancel_batch: bool) -> str:
    if not note.strip():
        raise SprintGateError("A factual progress note is required when pausing a sprint.")
    state = load_state(root)
    execution = state["execution"]
    sprint = execution.get("currentSprint") or state.get("activeSprint")
    lock = read_lock(root)
    if not sprint or not lock or lock.get("sprint") != sprint:
        raise SprintGateError("Cannot pause without a matching active sprint lock.")
    if state["sprints"].get(sprint) != "DONE":
        state["sprints"][sprint] = "PARTIAL"
    if cancel_batch and execution["batch"]["requestedCount"]:
        execution["lastBatch"] = {
            **execution["batch"],
            "status": "CANCELLED",
            "cancelledAtUtc": iso_now(),
            "reason": note,
        }
        execution["batch"] = {"requestedCount": 0, "completedCount": 0}
    execution.update(
        {
            "status": "IDLE",
            "currentSprint": sprint,
            "currentBlocker": None,
            "lastActivity": {
                "status": "PAUSED",
                "timestampUtc": iso_now(),
                "summary": note,
                "log": f".runtime/{sprint.lower().replace('-', '')}-progress.log",
            },
        }
    )
    state["activeSprint"] = sprint
    save_state(state, root)
    (root / ".agent" / "sprint.lock.json").unlink()
    return sprint


def resolve_gate(root: pathlib.Path, note: str) -> None:
    if not note.strip():
        raise SprintGateError("A human resolution note is required.")
    state = load_state(root)
    human_path = root / ".agent" / "HUMAN_REQUIRED.json"
    if human_path.exists():
        human_path.unlink()
    lock_path = root / ".agent" / "sprint.lock.json"
    if lock_path.exists():
        lock_path.unlink()
    execution = state["execution"]
    sprint = execution.get("currentSprint") or state.get("activeSprint")
    if state["sprints"].get(sprint) == "HUMAN_REQUIRED":
        state["sprints"][sprint] = "PARTIAL"
    execution.update({"status": "IDLE", "attemptCount": 0, "currentBlocker": {"resolvedByHuman": note, "resolvedAtUtc": iso_now()}})
    save_state(state, root)


def batch_start(root: pathlib.Path, count: int, owner: str, lease_minutes: int) -> str:
    if count < 1 or count > MAX_BATCH_SIZE:
        raise SprintGateError(f"Batch count must be between 1 and {MAX_BATCH_SIZE}.")
    state = load_state(root)
    if state["execution"]["batch"]["requestedCount"]:
        raise SprintGateError("A batch is already recorded; finish or stop it before starting another.")
    sprint = start(root, owner, lease_minutes)
    state = load_state(root)
    state["execution"]["batch"] = {"requestedCount": count, "completedCount": 0}
    save_state(state, root)
    return sprint


def status(root: pathlib.Path) -> dict[str, Any]:
    state = load_state(root)
    eligible: str | None
    try:
        eligible = next_eligible_sprint(state, root)
    except SprintGateError as error:
        eligible = f"blocked: {error}"
    return {"execution": state["execution"], "activeSprint": state["activeSprint"], "nextEligibleSprint": eligible, "lock": read_lock(root)}


def main() -> int:
    parser = argparse.ArgumentParser(description="FleetOps atomic sprint orchestration.")
    subparsers = parser.add_subparsers(dest="command", required=True)
    start_parser = subparsers.add_parser("start")
    start_parser.add_argument("--owner", default="generic-agent")
    start_parser.add_argument("--lease-minutes", type=int, default=240)
    batch_parser = subparsers.add_parser("batch-start")
    batch_parser.add_argument("count", type=int)
    batch_parser.add_argument("--owner", default="generic-agent")
    batch_parser.add_argument("--lease-minutes", type=int, default=240)
    subparsers.add_parser("validate")
    complete_parser = subparsers.add_parser("complete")
    complete_parser.add_argument("--gate", required=True)
    complete_parser.add_argument("--checkpoint", required=True)
    fail_parser = subparsers.add_parser("fail")
    fail_parser.add_argument("--gate", required=True)
    fail_parser.add_argument("--observed-failure", required=True)
    fail_parser.add_argument("--affected-file", action="append", default=[])
    stop_parser = subparsers.add_parser("stop")
    stop_parser.add_argument("--reason", required=True)
    pause_parser = subparsers.add_parser("pause")
    pause_parser.add_argument("--note", required=True)
    pause_parser.add_argument("--cancel-batch", action="store_true")
    resolve_parser = subparsers.add_parser("resolve-gate")
    resolve_parser.add_argument("--note", required=True)
    subparsers.add_parser("status")
    args = parser.parse_args()
    try:
        if args.command == "start":
            print(f"SELECTED {start(ROOT, args.owner, args.lease_minutes)}")
        elif args.command == "batch-start":
            print(f"SELECTED {batch_start(ROOT, args.count, args.owner, args.lease_minutes)}")
        elif args.command == "validate":
            set_validating(ROOT)
            print("VALIDATING")
        elif args.command == "complete":
            next_sprint = complete(ROOT, args.gate, args.checkpoint)
            print(f"COMPLETED; next eligible: {next_sprint or 'none'}")
        elif args.command == "fail":
            fail(ROOT, args.gate, args.observed_failure, args.affected_file)
            print("FAILURE_RECORDED")
        elif args.command == "stop":
            stop(ROOT, args.reason)
            print("STOPPED")
        elif args.command == "pause":
            sprint = pause(ROOT, args.note, args.cancel_batch)
            print(f"PAUSED {sprint}; next Start Next Sprint will resume it")
        elif args.command == "resolve-gate":
            resolve_gate(ROOT, args.note)
            print("GATE_RESOLVED")
        else:
            print(json.dumps(status(ROOT), indent=2, ensure_ascii=False))
    except SprintGateError as error:
        print(f"BLOCKED: {error}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
