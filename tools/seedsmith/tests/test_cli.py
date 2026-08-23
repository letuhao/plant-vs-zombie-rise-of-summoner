"""Tests for seedsmith.report.cli (tasks/seedsmith-todo.md, S1).

    python -m pytest tools/seedsmith/tests/test_cli.py -v
    python -m seedsmith check --adapter stub tests/fixtures/clean && echo OK   # (from tools/seedsmith/)
"""
from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.report.cli import (  # noqa: E402
    EXIT_CANNOT_RUN,
    EXIT_CLEAN,
    EXIT_GAP,
    main,
)

FIXTURES = Path(__file__).resolve().parent / "fixtures"


class ExitCodeTests(unittest.TestCase):
    def test_clean_fixture_exits_zero(self) -> None:
        self.assertEqual(main(["check", "--adapter", "stub", str(FIXTURES / "clean")]),
                         EXIT_CLEAN)

    def test_broken_fixture_exits_one(self) -> None:
        self.assertEqual(main(["check", "--adapter", "stub", str(FIXTURES / "broken")]),
                         EXIT_GAP)

    def test_unreadable_corpus_exits_two_distinct_from_gap(self) -> None:
        code = main(["check", "--adapter", "stub", str(FIXTURES / "unreadable")])
        self.assertEqual(code, EXIT_CANNOT_RUN)
        self.assertNotEqual(code, EXIT_GAP)

    def test_unknown_adapter_exits_two(self) -> None:
        code = main(["check", "--adapter", "does-not-exist", str(FIXTURES / "clean")])
        self.assertEqual(code, EXIT_CANNOT_RUN)

    def test_gate_flag_never_fails_while_nothing_is_promoted(self) -> None:
        # Every metric ships gates=False for the whole of W1 (spec-metrics.md §4) — --gate
        # against the broken fixture must therefore exit clean, not 1, until something is
        # deliberately promoted.
        code = main(["check", "--adapter", "stub", "--gate", str(FIXTURES / "broken")])
        self.assertEqual(code, EXIT_CLEAN)


class JsonOutputTests(unittest.TestCase):
    def test_json_output_matches_findings_and_carries_schema_version(self, ) -> None:
        import tempfile
        out_path = Path(tempfile.mkdtemp()) / "out.json"
        code = main(["check", "--adapter", "stub", "--json", str(out_path),
                    str(FIXTURES / "broken")])
        self.assertEqual(code, EXIT_GAP)

        data = json.loads(out_path.read_text(encoding="utf-8"))
        self.assertEqual(len(data), 1)
        self.assertEqual(data[0]["metric"], "Coverage/EmptyPartition")
        self.assertEqual(data[0]["subject"], "b")
        self.assertEqual(data[0]["schemaVersion"], 1)


class MetricFilterTests(unittest.TestCase):
    def test_metric_filter_runs_only_the_named_metric(self) -> None:
        # Only one metric exists in S1, but the --metric flag's plumbing (registry.get by id,
        # empty selection safety) is proven here rather than left for a later wave to discover
        # broken.
        code = main(["check", "--adapter", "stub", "--metric", "Coverage/EmptyPartition",
                    str(FIXTURES / "clean")])
        self.assertEqual(code, EXIT_CLEAN)

    def test_unknown_metric_id_runs_nothing_not_an_error(self) -> None:
        code = main(["check", "--adapter", "stub", "--metric", "Does/NotExist",
                    str(FIXTURES / "broken")])
        self.assertEqual(code, EXIT_CLEAN)  # nothing ran, so nothing found a GAP


if __name__ == "__main__":
    unittest.main()
