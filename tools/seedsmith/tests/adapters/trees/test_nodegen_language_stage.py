"""Tests for seedsmith.adapters.trees.nodegen.run.run_language_stage (task H2) — the whole-tree
orchestration: idempotence (§7 gate 14) and, the sharpest acceptance bullet of this task, a forced
rerun of the language stage over UNCHANGED inputs leaving `nodes/<treeId>.json` BYTE-IDENTICAL,
hash-compared, with ZERO further model calls (proven by patching the transport with a stub that
raises the moment it would be reached).

This is the exact defect class named in tasks/passive-tree-todo.md's own H2 section: "the
commander-effect generator rewrote all 84 entries every run" — caught here at the LANGUAGE
pipeline stage specifically (distinct from H9's later catalog-from-plan byte-identical check).

    python -m pytest tools/seedsmith/tests/adapters/trees/test_nodegen_language_stage.py -v
"""
from __future__ import annotations

import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parent))

from _nodegen_fixtures import raising_call, write_plan  # noqa: E402

from seedsmith.adapters.trees.nodegen import plan_read, run  # noqa: E402
from seedsmith.adapters.trees.nodegen.vocab import AffixOption, AffixVocabulary  # noqa: E402
from seedsmith.pipeline.llm_caller import LlmCallerConfig  # noqa: E402

AFFIX_A = AffixOption(affix_id="atom.a", name="Alpha Strike", tags=("offensive",), kind_id="k1")
VOCAB = AffixVocabulary(options=(AFFIX_A,))
TEST_CONFIG = LlmCallerConfig(max_heal=0, model="test-model")


def _accepted_response(node_key: str = "test-node") -> dict:
    return {
        "affixIds": ["atom.a"], "affinity": ["core"],
        "exclusion": {"form": "none", "propertyKeys": []},
        "name": f"Test Node {node_key}", "nameKey": f"tree.node.{node_key}",
        "flavor": "A steady line.", "rationale": "", "blocked": "",
    }


def _inputs_for(_subject: run.Subject) -> run.NodeGenerationInputs:
    return run.NodeGenerationInputs(
        tree_display_name="Might", tree_reading="the way of raw force",
        motifs=("strength",), anti_motifs=(), anti_motif_tags=(),
        permitted_affixes=(AFFIX_A,), permitted_properties=("posture",),
        property_vocabulary={"posture": ("vanguard", "warden")}, affix_vocab=VOCAB)


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


class RunLanguageStageIdempotenceTests(unittest.TestCase):
    def setUp(self) -> None:
        self.seed_root = Path(tempfile.mkdtemp())
        write_plan(self.seed_root, "t1", node_count=1)
        self.plan = plan_read.load("t1", self.seed_root)
        self.ledger_path = self.seed_root / "_runs" / "ledger.json"

    def test_first_run_generates_and_writes_the_seed_document(self) -> None:
        payload = json.dumps(_accepted_response())
        with patch("seedsmith.pipeline.llm_caller.call_model", return_value=payload):
            result = run.run_language_stage(self.plan, _inputs_for, ledger_path=self.ledger_path,
                                            seed_root=self.seed_root, config=TEST_CONFIG,
                                            unresolved_max_share_permille=500)
        self.assertEqual(len(result.outcomes), 1)
        self.assertEqual(result.outcomes[0].outcome, "accepted")
        self.assertIsNotNone(result.seed_path)
        self.assertTrue(result.seed_path.exists())
        doc = json.loads(result.seed_path.read_text(encoding="utf-8"))
        self.assertEqual(len(doc["nodes"]), 1)
        self.assertEqual(doc["nodes"][0]["affixIds"], ["atom.a"])
        self.assertEqual(result.report.verdict.value, "pass")

    def test_a_forced_rerun_over_unchanged_inputs_makes_zero_calls_and_is_byte_identical(self) -> None:
        payload = json.dumps(_accepted_response())
        with patch("seedsmith.pipeline.llm_caller.call_model", return_value=payload):
            first = run.run_language_stage(self.plan, _inputs_for, ledger_path=self.ledger_path,
                                           seed_root=self.seed_root, config=TEST_CONFIG,
                                           unresolved_max_share_permille=500)
        first_hash = _sha256(first.seed_path)
        first_bytes = first.seed_path.read_bytes()

        # The forced rerun: same plan, same (now-populated) ledger. The offline transport stub
        # RAISES on any call — if idempotence has a hole and the stage tries to regenerate even one
        # already-done subject, this test fails immediately rather than silently re-writing content.
        with patch("seedsmith.pipeline.llm_caller.call_model", raising_call):
            second = run.run_language_stage(self.plan, _inputs_for, ledger_path=self.ledger_path,
                                            seed_root=self.seed_root, config=TEST_CONFIG,
                                            unresolved_max_share_permille=500)

        self.assertEqual(len(second.outcomes), 0, "a forced rerun must generate nothing new")
        self.assertEqual(second.seed_path.read_bytes(), first_bytes)
        self.assertEqual(_sha256(second.seed_path), first_hash)

    def test_running_twice_never_double_records_the_same_subject_in_the_ledger(self) -> None:
        payload = json.dumps(_accepted_response())
        with patch("seedsmith.pipeline.llm_caller.call_model", return_value=payload):
            run.run_language_stage(self.plan, _inputs_for, ledger_path=self.ledger_path,
                                   seed_root=self.seed_root, config=TEST_CONFIG)
        with patch("seedsmith.pipeline.llm_caller.call_model", raising_call):
            run.run_language_stage(self.plan, _inputs_for, ledger_path=self.ledger_path,
                                   seed_root=self.seed_root, config=TEST_CONFIG)
        ledger = run.read_ledger(self.ledger_path)
        self.assertEqual(len(ledger), 1)


class RunLanguageStageMultiNodeTests(unittest.TestCase):
    """A tree with more than one node: only the UNRESOLVED ones are ever regenerated on a rerun,
    and the seed document's own node ORDER is a pure function of `node_id` — never dict/insertion
    order — which is what makes the byte-identical property hold regardless of which subject
    happened to finish first."""

    def setUp(self) -> None:
        self.seed_root = Path(tempfile.mkdtemp())
        write_plan(self.seed_root, "t1", node_count=2)
        self.plan = plan_read.load("t1", self.seed_root)
        self.ledger_path = self.seed_root / "_runs" / "ledger.json"

    def test_two_nodes_both_accept_and_the_seed_document_is_sorted_by_node_id(self) -> None:
        # 3 calls (base + 2 vote samples) per node, in `plan.subjects` order (n0 then n1) --
        # each node's own `nameKey` must be distinct, or `build_seed_document`'s own
        # `assert_no_duplicate_name_keys` refuses the whole tree.
        responses = ([json.dumps(_accepted_response("n0"))] * 3
                    + [json.dumps(_accepted_response("n1"))] * 3)
        with patch("seedsmith.pipeline.llm_caller.call_model", side_effect=responses):
            result = run.run_language_stage(self.plan, _inputs_for, ledger_path=self.ledger_path,
                                            seed_root=self.seed_root, config=TEST_CONFIG)
        doc = json.loads(result.seed_path.read_text(encoding="utf-8"))
        ids = [n["id"] for n in doc["nodes"]]
        self.assertEqual(ids, sorted(ids))


if __name__ == "__main__":
    unittest.main()
