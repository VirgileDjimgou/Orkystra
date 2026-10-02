#!/usr/bin/env python3
"""Verify that ROADMAP, machine state, sprint contracts, and Git HEAD agree.

This is the canonical consistency check referenced by scripts/quality-gate.ps1,
scripts/quality-gate.sh, and the release-validation workflow. It is read-only:
it never edits state, writes product code, commits, or pushes.
"""
from __future__ import annotations

import argparse
import json
import pathlib
import re
import subprocess
import sys
from typing import Any


ROOT = pathlib.Path(__file__).resolve().parents[2]
MAX_BATCH_SIZE = 10
SELECTABLE_STATUSES = {
    "NOT_STARTED",
    "IMPLEMENTED_NOT_VALIDATED",
    "PARTIAL",
    "READY",
    "PLANNED",
}
STATUS_HEADING = re.compile(r"^##\s+(?:Status|Statut)\s*$", re.MULTILINE)
STATUS_TOKEN = re.compile(r"`([A-Z][A-Z_]+)`")
ROADMAP_ROW = re.compile(r"^\|\s*(\d+)\s*\|[^|]*\|\s*([^|]+)\|", re.MULTILINE)


class ConsistencyError(RuntimeError):
    """Raised when repository truth documents contradict each other."""


def read_text(path: pathlib.Path) -> str:
    return path.read_text(encoding="utf-8")


def load_state(root: pathlib.Path) -> dict[str, Any]:
    path = root / ".agent" / "PROJECT_STATE.json"
    if not path.exists():
        raise ConsistencyError(f"Missing machine state: {path.relative_to(root)}")
    state = json.loads(read_text(path))
    if state.get("schemaVersion") != 2:
        raise ConsistencyError("PROJECT_STATE.json must use schemaVersion 2.")
    for key in ("sprints", "execution", "activeSprint"):
        if key not in state:
            raise ConsistencyError(f"PROJECT_STATE.json is missing '{key}'.")
    return state


def sprint_number(sprint: str) -> int:
    try:
        return int(sprint.removeprefix("SPRINT-"))
    except ValueError as error:
        raise ConsistencyError(f"Invalid sprint identifier: {sprint}") from error


def next_eligible_sprint(sprints: dict[str, str]) -> str | None:
    for sprint, status in sorted(sprints.items(), key=lambda item: sprint_number(item[0])):
        if status in {"DONE", "SUPERSEDED"}:
            continue
        if status in {"BLOCKED", "HUMAN_REQUIRED"}:
            raise ConsistencyError(f"{sprint} is {status}; its human gate is still open.")
        if status not in SELECTABLE_STATUSES:
            raise ConsistencyError(f"{sprint} has unsupported execution status {status}.")
        return sprint
    return None


def contract_paths(root: pathlib.Path) -> dict[str, pathlib.Path]:
    contracts: dict[str, pathlib.Path] = {}
    for path in sorted((root / "sprints").glob("SPRINT-*.md")):
        match = re.match(r"SPRINT-(\d+)-", path.name)
        if not match:
            continue
        key = f"SPRINT-{int(match.group(1)):02d}"
        if key in contracts:
            raise ConsistencyError(f"Duplicate contract file for {key}.")
        contracts[key] = path
    return contracts


def contract_status(path: pathlib.Path) -> str | None:
    text = read_text(path)
    heading = STATUS_HEADING.search(text)
    if not heading:
        return None
    for line in text[heading.end():].splitlines():
        stripped = line.strip()
        if not stripped:
            continue
        if stripped.startswith("#"):
            return None
        token = STATUS_TOKEN.search(stripped)
        if token:
            return token.group(1)
        return None
    return None


def roadmap_statuses(root: pathlib.Path) -> dict[str, str]:
    path = root / "ROADMAP.md"
    if not path.exists():
        raise ConsistencyError("Missing ROADMAP.md")
    statuses: dict[str, str] = {}
    for number, cell in ROADMAP_ROW.findall(read_text(path)):
        sprint = f"SPRINT-{int(number):02d}"
        token = re.search(r"[A-Z][A-Z_]+", cell.replace("`", ""))
        if not token:
            raise ConsistencyError(f"ROADMAP row for {sprint} has no status token.")
        statuses[sprint] = token.group(0)
    return statuses


def run_git(root: pathlib.Path, *args: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["git", *args], cwd=root, capture_output=True, text=True, check=False
    )


def verify(root: pathlib.Path) -> list[str]:
    state = load_state(root)
    sprints = state["sprints"]
    execution = state["execution"]
    checks: list[str] = []

    contracts = contract_paths(root)
    for sprint in sorted(sprints, key=sprint_number):
        status = sprints[sprint]
        if sprint not in contracts:
            raise ConsistencyError(f"{sprint} has no contract file in sprints/.")
        observed = contract_status(contracts[sprint])
        if observed is not None and observed != status:
            raise ConsistencyError(
                f"{sprint} contract status '{observed}' disagrees with machine state '{status}'."
            )
        if observed is None and status != "DONE":
            raise ConsistencyError(
                f"{sprint} contract has no Status section but machine state is '{status}'."
            )
    checks.append(f"contracts: {len(contracts)} files agree with machine state")

    for sprint, roadmap_status in roadmap_statuses(root).items():
        if sprint not in sprints:
            raise ConsistencyError(f"ROADMAP references unknown {sprint}.")
        if sprints[sprint] != roadmap_status:
            raise ConsistencyError(
                f"ROADMAP status '{roadmap_status}' for {sprint} disagrees with machine state '{sprints[sprint]}'."
            )
    checks.append("roadmap: sprint tables agree with machine state")

    active = state["activeSprint"]
    current = execution.get("currentSprint")
    if active != current:
        raise ConsistencyError(
            f"activeSprint '{active}' does not match execution.currentSprint '{current}'."
        )
    if active not in sprints:
        raise ConsistencyError(f"activeSprint '{active}' is not listed in sprints.")
    status = execution.get("status")
    lock_path = root / ".agent" / "sprint.lock.json"
    if status == "IDLE":
        if lock_path.exists():
            raise ConsistencyError(
                "execution.status is IDLE but .agent/sprint.lock.json exists."
            )
        eligible = next_eligible_sprint(sprints)
        if eligible and active != eligible:
            raise ConsistencyError(
                f"execution is IDLE but activeSprint '{active}' is not the next eligible sprint '{eligible}'."
            )
    elif status in {"RUNNING", "VALIDATING"}:
        if not lock_path.exists():
            raise ConsistencyError(
                f"execution.status is {status} but no sprint lock exists."
            )
        lock = json.loads(read_text(lock_path))
        if lock.get("sprint") != current:
            raise ConsistencyError(
                f"sprint lock '{lock.get('sprint')}' does not match execution.currentSprint '{current}'."
            )
    checks.append(f"execution: status {status} coherent with active sprint {active}")

    human_path = root / ".agent" / "HUMAN_REQUIRED.json"
    human_sprints = sorted(s for s, value in sprints.items() if value == "HUMAN_REQUIRED")
    if human_path.exists() != bool(human_sprints):
        raise ConsistencyError(
            "HUMAN_REQUIRED.json and HUMAN_REQUIRED sprint statuses disagree."
        )
    checks.append(
        f"human gates: {', '.join(human_sprints) if human_sprints else 'none recorded'}"
    )

    for stop in (root / "STOP", root / ".agent" / "STOP"):
        if stop.exists():
            raise ConsistencyError(f"Explicit stop file exists: {stop.relative_to(root)}")
    checks.append("stop gates: clear")

    batch = execution.get("batch", {})
    requested = int(batch.get("requestedCount", 0))
    completed = int(batch.get("completedCount", 0))
    if not 0 <= completed <= requested <= MAX_BATCH_SIZE:
        raise ConsistencyError(
            f"Batch counters are invalid: {completed}/{requested} (max {MAX_BATCH_SIZE})."
        )
    checks.append(f"batch: {completed}/{requested}")

    last_commit = execution.get("lastCommit")
    if not last_commit:
        raise ConsistencyError("execution.lastCommit is missing.")
    exists = run_git(root, "cat-file", "-e", f"{last_commit}^{{commit}}")
    if exists.returncode != 0:
        raise ConsistencyError(
            f"execution.lastCommit '{last_commit}' is not present in this clone; "
            "check out with full history (fetch-depth: 0)."
        )
    ancestor = run_git(root, "merge-base", "--is-ancestor", last_commit, "HEAD")
    if ancestor.returncode != 0:
        raise ConsistencyError(
            f"execution.lastCommit '{last_commit}' is not an ancestor of HEAD."
        )
    checks.append(f"git: lastCommit {last_commit} is an ancestor of HEAD")

    current_file = root / ".agent" / "CURRENT_SPRINT.md"
    if not current_file.exists():
        raise ConsistencyError("Missing .agent/CURRENT_SPRINT.md.")
    if active not in read_text(current_file):
        raise ConsistencyError(
            f".agent/CURRENT_SPRINT.md does not mention active sprint {active}."
        )
    checks.append("current sprint brief mentions the active sprint")

    return checks


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Verify FleetOps repository truth consistency."
    )
    parser.add_argument("--root", type=pathlib.Path, default=ROOT)
    args = parser.parse_args()
    try:
        checks = verify(args.root)
    except ConsistencyError as error:
        print(f"FAIL: {error}", file=sys.stderr)
        return 1
    for check in checks:
        print(f"OK: {check}")
    print("State consistency verified.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
