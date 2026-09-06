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


def _write_magnitude_first_plan(seed_root: Path, tree_id: str = "t1") -> None:
    """The exact shape the 2026-09-06 `might` smoke test surfaced: a tier's magnitude node listed in
    the plan BEFORE that tier's own mechanism node — `write_plan`'s shared fixture always puts a
    mechanism node first (`i == 0`), so it can never reproduce this ordering bug. Both nodes tier 1."""
    plan = {
        "schemaVersion": 1, "treeId": tree_id, "archetype": "broad-and-flat",
        "propertyVocabulary": {"posture": ["vanguard", "warden"]},
        "mechNodesByTier": [1, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        "nodes": [
            {"id": f"skill.{tree_id}-off-t1-n0", "nodeKey": "n0", "branch": "offensive", "tier": 1,
             "indexInTier": 0, "nodeClass": "magnitude", "budgetShareMilli": 500, "budgetPoints": 25},
            {"id": f"skill.{tree_id}-off-t1-n1", "nodeKey": "n1", "branch": "offensive", "tier": 1,
             "indexInTier": 1, "nodeClass": "mechanism", "budgetShareMilli": 500, "budgetPoints": 25},
        ],
    }
    path = seed_root / "passive-tree" / "plan" / f"{tree_id}.v1.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(plan, indent=2) + "\n", encoding="utf-8")


class MechanismBeforeMagnitudeSiblingTests(unittest.TestCase):
    """2026-09-06: the smoke test's own root cause, closed. `plan_run` now orders a tier's mechanism
    node(s) before its magnitude node(s) regardless of file order, and `run_language_stage` tracks
    already-accepted tier siblings itself (never trusting `inputs_for` to do it) — so a magnitude node
    that would otherwise render with brief.py's own literal "(none yet)" now sees the real mechanism
    sibling that came before it, the moment one exists."""

    def setUp(self) -> None:
        self.seed_root = Path(tempfile.mkdtemp())
        _write_magnitude_first_plan(self.seed_root, "t1")
        self.plan = plan_read.load("t1", self.seed_root)
        self.ledger_path = self.seed_root / "_runs" / "ledger.json"
        self.rendered_users: "list[str]" = []

    def _capturing_call(self, _system, user, *, config=None, temperature=0.2, schema=None):
        # The brief TEXT carries no explicit node id, so which node a call is for is read off the
        # call's own position instead: subject order is fixed by `plan_run`'s reorder (mechanism n1
        # first, 3 calls -- base + 2 votes -- then magnitude n0, 3 more calls).
        index = len(self.rendered_users)
        self.rendered_users.append(user)
        node_key = "n1" if index < 3 else "n0"
        return json.dumps(_accepted_response(node_key))

    def test_the_mechanism_node_generates_first_despite_listing_second_in_the_plan(self) -> None:
        with patch("seedsmith.pipeline.llm_caller.call_model", side_effect=self._capturing_call):
            result = run.run_language_stage(self.plan, _inputs_for, ledger_path=self.ledger_path,
                                            seed_root=self.seed_root, config=TEST_CONFIG)
        self.assertEqual(len(result.outcomes), 2)
        # plan.subjects visits the mechanism node (n1) before the magnitude node (n0), even though
        # n0 is listed FIRST in the plan's own `nodes` array.
        self.assertEqual(result.outcomes[0].subject_id, "t1:skill.t1-off-t1-n1")
        self.assertEqual(result.outcomes[1].subject_id, "t1:skill.t1-off-t1-n0")
        self.assertEqual(result.outcomes[0].outcome, "accepted")
        self.assertEqual(result.outcomes[1].outcome, "accepted")

    def test_the_magnitude_nodes_own_brief_names_the_mechanism_sibling_that_preceded_it(self) -> None:
        with patch("seedsmith.pipeline.llm_caller.call_model", side_effect=self._capturing_call):
            run.run_language_stage(self.plan, _inputs_for, ledger_path=self.ledger_path,
                                   seed_root=self.seed_root, config=TEST_CONFIG)
        # n1 (mechanism, generated first): no siblings yet -- brief.py's own literal placeholder.
        n1_base_call_user = self.rendered_users[0]
        self.assertIn("(none yet)", n1_base_call_user)
        # n0 (magnitude, generated second, same tier): n1's own accepted name + affix now appear as
        # a real sibling line, never the empty placeholder -- this is the exact fix for the smoke
        # test's "magnitude node requires an existing thing to make larger; current tree is empty."
        n0_base_call_user = self.rendered_users[3]
        self.assertIn("Test Node n1", n0_base_call_user)
        self.assertIn("atom.a", n0_base_call_user)
        self.assertNotIn("(none yet)", n0_base_call_user)

    def test_a_resumed_run_seeds_siblings_from_the_ledger_not_only_this_runs_own_acceptances(self) -> None:
        # First run: only the mechanism node (n1) generates and is accepted; the magnitude node's
        # own call is never reached (simulates a process killed between the two subjects).
        def _only_n1(_system, user, *, config=None, temperature=0.2, schema=None):
            if "skill.t1-off-t1-n0" in user:
                raise AssertionError("n0 must not be called in the first, partial run")
            return json.dumps(_accepted_response("n1"))

        with patch("seedsmith.pipeline.llm_caller.call_model", side_effect=_only_n1):
            # Only run the mechanism subject by hand-truncating the plan -- reuses plan_run directly
            # since run_language_stage itself has no partial-run knob.
            partial_plan = run.plan_run(self.plan, ledger={})
            mechanism_subject = next(s for s in partial_plan.subjects if s.node_class == "mechanism")
            outcome = run.generate_node(mechanism_subject, _inputs_for(mechanism_subject), config=TEST_CONFIG)
            self.assertEqual(outcome.outcome, "accepted")
            done = run.record_accepted({}, mechanism_subject.subject_id, outcome.record)
            run.write_ledger(done, self.ledger_path)

        # Second run, fresh process (a new `rendered_users` capture): the magnitude node's own brief
        # must see n1 as a sibling even though THIS run never generated it itself.
        rendered: "list[str]" = []

        def _capture_second_run(_system, user, *, config=None, temperature=0.2, schema=None):
            rendered.append(user)
            return json.dumps(_accepted_response("n0"))

        with patch("seedsmith.pipeline.llm_caller.call_model", side_effect=_capture_second_run):
            result = run.run_language_stage(self.plan, _inputs_for, ledger_path=self.ledger_path,
                                            seed_root=self.seed_root, config=TEST_CONFIG)
        self.assertEqual(len(result.outcomes), 1)
        self.assertEqual(result.outcomes[0].subject_id, "t1:skill.t1-off-t1-n0")
        self.assertIn("Test Node n1", rendered[0])
        self.assertNotIn("(none yet)", rendered[0])


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
