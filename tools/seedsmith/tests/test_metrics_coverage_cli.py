"""Tests for `seedsmith metrics --coverage` (tasks/seedsmith-todo.md, S7).

    python -m pytest tools/seedsmith/tests/test_metrics_coverage_cli.py -v
"""
from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.metrics.appendix_a import AppendixARow, coverage_report  # noqa: E402
from seedsmith.report.cli import EXIT_CLEAN, EXIT_GAP, main  # noqa: E402


class _FakeMetric:
    def __init__(self, id_, covers):
        self.id = id_
        self.covers = covers


class CoverageReportTests(unittest.TestCase):
    def test_every_w1_scope_row_currently_registered_is_claimed(self) -> None:
        # This is the acceptance criterion itself: run it against the REAL registry, not a fake
        # one, so a future metric that forgets to declare `covers` is caught immediately.
        code = main(["metrics", "--coverage"])
        self.assertEqual(code, EXIT_CLEAN)

    def test_an_in_scope_row_with_no_covering_metric_is_unclaimed_not_hidden(self) -> None:
        rows = (AppendixARow(99, "a hypothetical missing check", "TestFamily", True),)
        import seedsmith.metrics.appendix_a as appendix_a_module
        original = appendix_a_module.ROWS
        appendix_a_module.ROWS = rows
        try:
            report = coverage_report([])
            self.assertEqual(len(report["unclaimed"]), 1)
            self.assertEqual(report["unclaimed"][0].number, 99)
        finally:
            appendix_a_module.ROWS = original

    def test_an_out_of_scope_row_with_no_covering_metric_is_a_known_gap_not_unclaimed(self) -> None:
        rows = (AppendixARow(100, "planner work, W2", "Feasibility", False),)
        import seedsmith.metrics.appendix_a as appendix_a_module
        original = appendix_a_module.ROWS
        appendix_a_module.ROWS = rows
        try:
            report = coverage_report([])
            self.assertEqual(report["unclaimed"], [])
            self.assertEqual(len(report["known_gap"]), 1)
        finally:
            appendix_a_module.ROWS = original

    def test_a_claimed_row_lists_the_covering_metric_id(self) -> None:
        rows = (AppendixARow(101, "d", "F", True),)
        import seedsmith.metrics.appendix_a as appendix_a_module
        original = appendix_a_module.ROWS
        appendix_a_module.ROWS = rows
        try:
            metric = _FakeMetric("Family/Thing", ("appendix-a:101",))
            report = coverage_report([metric])
            self.assertEqual(len(report["claimed"]), 1)
            row, ids = report["claimed"][0]
            self.assertEqual(row.number, 101)
            self.assertEqual(ids, ["Family/Thing"])
        finally:
            appendix_a_module.ROWS = original


if __name__ == "__main__":
    unittest.main()
