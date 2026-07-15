#!/usr/bin/env python3
from __future__ import annotations

import argparse
import datetime as dt
import json
import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
STATE = ROOT / ".agent" / "PROJECT_STATE.json"
CHECKPOINTS = ROOT / ".agent" / "checkpoints"


def run(command: list[str]) -> None:
    subprocess.run(command, cwd=ROOT, check=True)


def main() -> int:
    parser = argparse.ArgumentParser(description="Create a FleetOps development checkpoint.")
    parser.add_argument("--summary", required=True)
    parser.add_argument("--skip-quality-gate", action="store_true")
    parser.add_argument("--no-commit", action="store_true")
    args = parser.parse_args()

    if not args.skip_quality_gate:
        gate = ROOT / "scripts" / ("quality-gate.ps1" if sys.platform == "win32" else "quality-gate.sh")
        run(["powershell", "-ExecutionPolicy", "Bypass", "-File", str(gate)] if sys.platform == "win32" else [str(gate)])

    now = dt.datetime.now(dt.timezone.utc).replace(microsecond=0)
    stamp = now.strftime("%Y%m%dT%H%M%SZ")
    state = json.loads(STATE.read_text(encoding="utf-8"))
    state["lastCheckpoint"] = {"timestampUtc": now.isoformat(), "summary": args.summary}
    state["lastQualityGate"] = {
        "status": "SKIPPED" if args.skip_quality_gate else "PASSED",
        "timestampUtc": now.isoformat(),
        "report": ".agent/QUALITY_REPORT.md",
    }
    STATE.write_text(json.dumps(state, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    CHECKPOINTS.mkdir(parents=True, exist_ok=True)
    checkpoint = CHECKPOINTS / f"{stamp}.md"
    checkpoint.write_text(
        f"# Checkpoint {stamp}\n\n- Sprint: {state['activeSprint']}\n- Résumé: {args.summary}\n- Quality gate: {state['lastQualityGate']['status']}\n",
        encoding="utf-8",
    )

    if not args.no_commit:
        run(["git", "add", "-A"])
        run(["git", "commit", "-m", f"chore(checkpoint): {args.summary}"])

    print(checkpoint.relative_to(ROOT))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
