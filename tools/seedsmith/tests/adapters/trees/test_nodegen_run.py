"""Tests for seedsmith.adapters.trees.nodegen.run (task H1) — `plan_run` over one tree's committed
plan -> `RunPlan{subjects, held, already_done}`.
"""
from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from _nodegen_fixtures import write_plan  # noqa: E402

from seedsmith.adapters.trees.nodegen import plan_read, run


class PlanRunTests(unittest.TestCase):
    def test_one_subject_per_node(self) -> None:
        seed_root = Path(tempfile.mkdtemp())
        write_plan(seed_root, "t1", node_count=4)
        plan = plan_read.load("t1", seed_root)
        result = run.plan_run(plan, ledger={})
        self.assertEqual(len(result.subjects), 4)
        self.assertEqual(result.held, [])
        self.assertEqual(result.already_done, [])
        self.assertTrue(result.complete)

    def test_a_subject_already_in_the_ledger_is_skipped(self) -> None:
        seed_root = Path(tempfile.mkdtemp())
        write_plan(seed_root, "t1", node_count=4)
        plan = plan_read.load("t1", seed_root)
        first_node = plan.nodes[0]
        ledger = {f"t1:{first_node.node_id}": {"done": True}}
        result = run.plan_run(plan, ledger=ledger)
        self.assertEqual(len(result.subjects), 3)
        self.assertEqual(result.already_done, [f"t1:{first_node.node_id}"])

    def test_subject_carries_no_brief_or_schema_yet(self) -> None:
        """H1's own honest gap: resolving a brief/schema needs a quota cell, which is H3's."""
        seed_root = Path(tempfile.mkdtemp())
        write_plan(seed_root, "t1", node_count=1)
        plan = plan_read.load("t1", seed_root)
        result = run.plan_run(plan, ledger={})
        self.assertIsNone(result.subjects[0].brief)
        self.assertIsNone(result.subjects[0].schema)


class LedgerRoundTripTests(unittest.TestCase):
    def test_write_then_read_round_trips(self) -> None:
        ledger_path = Path(tempfile.mkdtemp()) / "ledger.json"
        done = {"t1:skill.t1-off-t1-n0": {"acceptedUtc": "2026-09-06T00:00:00Z"}}
        run.write_ledger(done, ledger_path)
        self.assertEqual(run.read_ledger(ledger_path), done)

    def test_reading_a_missing_ledger_returns_empty(self) -> None:
        missing = Path(tempfile.mkdtemp()) / "does-not-exist.json"
        self.assertEqual(run.read_ledger(missing), {})


class RunPlanSummaryTests(unittest.TestCase):
    def test_summary_reports_held_reasons(self) -> None:
        plan = run.RunPlan(subjects=[], held=[("t1:n0", "UnsatisfiableCell"),
                                              ("t1:n1", "UnsatisfiableCell")], already_done=[])
        summary = plan.summary()
        self.assertEqual(summary["held"], 2)
        self.assertEqual(summary["heldByReason"]["UnsatisfiableCell"], 2)
        self.assertFalse(plan.complete)


if __name__ == "__main__":
    unittest.main()
