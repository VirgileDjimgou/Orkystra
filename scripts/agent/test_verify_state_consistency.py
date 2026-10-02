from __future__ import annotations

import importlib.util
import json
import pathlib
import subprocess
import tempfile
import unittest


MODULE_PATH = pathlib.Path(__file__).with_name("verify_state_consistency.py")
SPEC = importlib.util.spec_from_file_location("verify_state_consistency", MODULE_PATH)
assert SPEC and SPEC.loader
consistency = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(consistency)


class VerifyStateConsistencyTests(unittest.TestCase):
    def build_root(
        self,
        machine_sprints: dict[str, str],
        contract_statuses: dict[str, str | None],
        roadmap_rows: list[tuple[int, str]],
        current_text: str,
        execution_overrides: dict[str, object] | None = None,
    ) -> pathlib.Path:
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = pathlib.Path(temporary.name)
        (root / ".agent").mkdir()
        (root / "sprints").mkdir()
        subprocess.run(["git", "init", "-q"], cwd=root, check=True, capture_output=True)
        subprocess.run(
            [
                "git",
                "-c",
                "user.email=ci@example.com",
                "-c",
                "user.name=CI",
                "commit",
                "--allow-empty",
                "-m",
                "init",
            ],
            cwd=root,
            check=True,
            capture_output=True,
        )
        head = subprocess.run(
            ["git", "rev-parse", "--short", "HEAD"],
            cwd=root,
            capture_output=True,
            text=True,
            check=True,
        ).stdout.strip()

        for sprint, status in contract_statuses.items():
            body = f"# {sprint}\n\n"
            if status is not None:
                body += f"## Status\n\n`{status}`\n"
            (root / "sprints" / f"{sprint}-example.md").write_text(body, encoding="utf-8")

        roadmap_lines = [
            "# Roadmap",
            "",
            "| Sprint | Outcome | Status | Source of truth |",
            "|---|---|---|---|",
        ]
        for number, status in roadmap_rows:
            roadmap_lines.append(
                f"| {number} | Example | {status} | `sprints/SPRINT-{number}-example.md` |"
            )
        (root / "ROADMAP.md").write_text("\n".join(roadmap_lines) + "\n", encoding="utf-8")

        eligible = next(
            (
                sprint
                for sprint, status in sorted(machine_sprints.items())
                if status not in {"DONE", "SUPERSEDED"}
            ),
            sorted(machine_sprints)[-1],
        )
        execution: dict[str, object] = {
            "status": "IDLE",
            "currentSprint": eligible,
            "batch": {"requestedCount": 0, "completedCount": 0},
            "lastCommit": head,
        }
        if execution_overrides:
            execution.update(execution_overrides)
        state = {
            "schemaVersion": 2,
            "activeSprint": eligible,
            "sprints": machine_sprints,
            "execution": execution,
        }
        (root / ".agent" / "PROJECT_STATE.json").write_text(
            json.dumps(state, indent=2) + "\n", encoding="utf-8"
        )
        (root / ".agent" / "CURRENT_SPRINT.md").write_text(current_text, encoding="utf-8")
        return root

    def test_consistent_repository_passes(self) -> None:
        root = self.build_root(
            machine_sprints={"SPRINT-24": "DONE", "SPRINT-25": "PLANNED"},
            contract_statuses={"SPRINT-24": None, "SPRINT-25": "PLANNED"},
            roadmap_rows=[(24, "DONE — 2026-01-01"), (25, "PLANNED")],
            current_text="# Active\n\nSPRINT-25 is planned.\n",
        )
        checks = consistency.verify(root)
        self.assertTrue(any("contracts" in check for check in checks))

    def test_contract_status_mismatch_is_rejected(self) -> None:
        root = self.build_root(
            machine_sprints={"SPRINT-25": "PLANNED"},
            contract_statuses={"SPRINT-25": "DONE"},
            roadmap_rows=[(25, "PLANNED")],
            current_text="SPRINT-25\n",
        )
        with self.assertRaises(consistency.ConsistencyError):
            consistency.verify(root)

    def test_roadmap_status_mismatch_is_rejected(self) -> None:
        root = self.build_root(
            machine_sprints={"SPRINT-25": "PLANNED"},
            contract_statuses={"SPRINT-25": "PLANNED"},
            roadmap_rows=[(25, "READY")],
            current_text="SPRINT-25\n",
        )
        with self.assertRaises(consistency.ConsistencyError):
            consistency.verify(root)

    def test_executable_contract_without_status_is_rejected(self) -> None:
        root = self.build_root(
            machine_sprints={"SPRINT-25": "READY"},
            contract_statuses={"SPRINT-25": None},
            roadmap_rows=[(25, "READY")],
            current_text="SPRINT-25\n",
        )
        with self.assertRaises(consistency.ConsistencyError):
            consistency.verify(root)

    def test_idle_execution_with_lock_is_rejected(self) -> None:
        root = self.build_root(
            machine_sprints={"SPRINT-25": "PLANNED"},
            contract_statuses={"SPRINT-25": "PLANNED"},
            roadmap_rows=[(25, "PLANNED")],
            current_text="SPRINT-25\n",
        )
        (root / ".agent" / "sprint.lock.json").write_text("{}", encoding="utf-8")
        with self.assertRaises(consistency.ConsistencyError):
            consistency.verify(root)

    def test_invalid_batch_counters_are_rejected(self) -> None:
        root = self.build_root(
            machine_sprints={"SPRINT-25": "PLANNED"},
            contract_statuses={"SPRINT-25": "PLANNED"},
            roadmap_rows=[(25, "PLANNED")],
            current_text="SPRINT-25\n",
            execution_overrides={"batch": {"requestedCount": 2, "completedCount": 3}},
        )
        with self.assertRaises(consistency.ConsistencyError):
            consistency.verify(root)

    def test_stale_last_commit_is_rejected(self) -> None:
        root = self.build_root(
            machine_sprints={"SPRINT-25": "PLANNED"},
            contract_statuses={"SPRINT-25": "PLANNED"},
            roadmap_rows=[(25, "PLANNED")],
            current_text="SPRINT-25\n",
            execution_overrides={"lastCommit": "0000000"},
        )
        with self.assertRaises(consistency.ConsistencyError):
            consistency.verify(root)


if __name__ == "__main__":
    unittest.main()
