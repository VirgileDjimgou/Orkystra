from __future__ import annotations

import importlib.util
import pathlib
import tempfile
import unittest


MODULE_PATH = pathlib.Path(__file__).with_name("sprint_orchestrator.py")
SPEC = importlib.util.spec_from_file_location("sprint_orchestrator", MODULE_PATH)
assert SPEC and SPEC.loader
orchestrator = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(orchestrator)


class SprintOrchestratorTests(unittest.TestCase):
    def make_root(self) -> pathlib.Path:
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = pathlib.Path(temporary.name)
        (root / "sprints").mkdir()
        return root

    def test_selects_lowest_non_done_active_sprint(self) -> None:
        root = self.make_root()
        (root / "sprints" / "SPRINT-24-example.md").write_text("# sprint\n", encoding="utf-8")
        (root / "sprints" / "SPRINT-25-example.md").write_text("# sprint\n", encoding="utf-8")
        state = {"sprints": {"SPRINT-23": "DONE", "SPRINT-24": "NOT_STARTED", "SPRINT-25": "NOT_STARTED"}}
        self.assertEqual("SPRINT-24", orchestrator.next_eligible_sprint(state, root))

    def test_ignores_archived_files_when_validating_active_contract(self) -> None:
        root = self.make_root()
        archive = root / "sprints" / "archive"
        archive.mkdir()
        (archive / "SPRINT-24-old.md").write_text("# old\n", encoding="utf-8")
        with self.assertRaises(orchestrator.SprintGateError):
            orchestrator.active_sprint_file(root, "SPRINT-24")

    def test_detects_unchecked_acceptance_criteria(self) -> None:
        root = self.make_root()
        contract = root / "sprints" / "SPRINT-24-example.md"
        contract.write_text("## Acceptance criteria\n\n- [x] complete\n- [ ] missing\n\n## Demo proof\n", encoding="utf-8")
        self.assertEqual(["missing"], orchestrator.unchecked_acceptance_items(contract))

    def test_rejects_human_required_sprint(self) -> None:
        root = self.make_root()
        state = {"sprints": {"SPRINT-24": "HUMAN_REQUIRED"}}
        with self.assertRaises(orchestrator.SprintGateError):
            orchestrator.next_eligible_sprint(state, root)

    def test_selects_ready_then_planned_sprints(self) -> None:
        root = self.make_root()
        (root / "sprints" / "SPRINT-33-example.md").write_text("# sprint\n", encoding="utf-8")
        (root / "sprints" / "SPRINT-34-example.md").write_text("# sprint\n", encoding="utf-8")
        state = {"sprints": {"SPRINT-32": "DONE", "SPRINT-33": "READY", "SPRINT-34": "PLANNED"}}
        self.assertEqual("SPRINT-33", orchestrator.next_eligible_sprint(state, root))

        state["sprints"]["SPRINT-33"] = "DONE"
        self.assertEqual("SPRINT-34", orchestrator.next_eligible_sprint(state, root))


if __name__ == "__main__":
    unittest.main()
