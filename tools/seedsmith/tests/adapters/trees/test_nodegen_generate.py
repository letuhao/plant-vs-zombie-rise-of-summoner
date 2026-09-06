"""Tests for seedsmith.adapters.trees.nodegen.run's task-H2 additions: the real gate runner over
one node — `build_response_gate`/`brief_conformance_defects` (§7 gate 9), `call_one_node_sample`/
`generate_node` (§7 gates 6, 7, 11, 12, 13), `offline_transport_stub`/`UnexpectedTransportCall`
(§7 gate 24), `audit_node_schema_descriptions` (§7 gate 2).

    python -m pytest tools/seedsmith/tests/adapters/trees/test_nodegen_generate.py -v

Every test that must make zero model calls patches `seedsmith.pipeline.llm_caller.call_model`
with `_nodegen_fixtures.raising_call` — the same offline-transport pattern already used four times
over in this repo (`test_classify_pipelines.py`, `test_family_propose.py`,
`test_general_propose.py`, `test_signature_propose.py`). Every test that DOES exercise a call
patches it with a small scripted stub instead, so no test ever reaches a real network endpoint.
"""
from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parent))

from _nodegen_fixtures import raising_call  # noqa: E402

from seedsmith.adapters.trees.nodegen import run  # noqa: E402
from seedsmith.adapters.trees.nodegen.vocab import AffixOption, AffixVocabulary  # noqa: E402
from seedsmith.pipeline.llm_caller import LlmCallerConfig  # noqa: E402


AFFIX_A = AffixOption(affix_id="atom.a", name="Alpha Strike", tags=("offensive",), kind_id="k1")
AFFIX_B = AffixOption(affix_id="atom.b", name="Bulwark", tags=("defensive",), kind_id="k2")
AFFIX_C = AffixOption(affix_id="atom.c", name="Cursed Bloom", tags=("utility", "anti"), kind_id="k3")
AFFIX_D = AffixOption(affix_id="atom.d", name="Driftguard", tags=("defensive",), kind_id="k4")
VOCAB = AffixVocabulary(options=(AFFIX_A, AFFIX_B, AFFIX_C, AFFIX_D))

TEST_CONFIG = LlmCallerConfig(max_heal=1)  # 2 attempts total per call — keeps scripts short


def _inputs(**overrides) -> run.NodeGenerationInputs:
    base = dict(
        tree_display_name="Might", tree_reading="the way of raw force",
        motifs=("strength",), anti_motifs=("weakness",), anti_motif_tags=("anti",),
        permitted_affixes=(AFFIX_A, AFFIX_B, AFFIX_C, AFFIX_D), permitted_properties=("posture",),
        property_vocabulary={"posture": ("vanguard", "warden")}, affix_vocab=VOCAB,
    )
    base.update(overrides)
    return run.NodeGenerationInputs(**base)


def _response(affix_ids=("atom.a",), name="Test Node", exclusion=None) -> dict:
    return {
        "affixIds": list(affix_ids), "affinity": ["core"] * len(affix_ids),
        "exclusion": exclusion or {"form": "none", "propertyKeys": []},
        "name": name, "nameKey": "tree.node.test-node", "flavor": "A steady line.",
        "rationale": "", "blocked": "",
    }


def _subject(node_id: str = "skill.might-off-t1-n0") -> run.Subject:
    return run.Subject(subject_id=f"t1:{node_id}", tree_id="t1", node_id=node_id, node_key="n0",
                      branch="offensive", tier=1, node_class="mechanism")


class OfflineTransportStubTests(unittest.TestCase):
    def test_the_stub_raises_on_any_call(self) -> None:
        with self.assertRaises(run.UnexpectedTransportCall):
            run.offline_transport_stub("system", "user", {})

    def test_the_stub_raises_even_with_no_schema(self) -> None:
        with self.assertRaises(run.UnexpectedTransportCall):
            run.offline_transport_stub("system", "user")


class RunPreflightDelegationTests(unittest.TestCase):
    """§7 gate 6 delegates VERBATIM to `actions.validate_heal.preflight.run_preflight` — no
    tree-specific probe schema of its own (constrained decoding is a property of the SERVER, not
    of this module's response shape)."""

    def test_skip_true_never_touches_the_transport(self) -> None:
        result = run.run_preflight(skip=True, transport=run.offline_transport_stub)
        self.assertEqual(result.status, "skipped")
        self.assertFalse(result.blocks_run)

    def test_a_raising_transport_with_skip_false_is_reported_failed_not_propagated(self) -> None:
        """`actions.validate_heal.preflight.run_preflight`'s own contract: any exception from the
        transport becomes a `status="failed"` result, never an uncaught raise — this module reuses
        that behaviour rather than reimplementing its own."""
        result = run.run_preflight(skip=False, transport=run.offline_transport_stub)
        self.assertEqual(result.status, "failed")
        self.assertTrue(result.blocks_run)


class AuditNodeSchemaDescriptionsTests(unittest.TestCase):
    def test_the_real_shipped_schema_passes_with_no_call(self) -> None:
        with patch("seedsmith.pipeline.llm_caller.call_model", raising_call):
            defects = run.audit_node_schema_descriptions()
        self.assertEqual(defects, [])


class BriefConformanceDefectsTests(unittest.TestCase):
    def test_an_affix_tagged_with_an_anti_motif_is_a_defect(self) -> None:
        defects = run.brief_conformance_defects(
            _response(affix_ids=["atom.c"]), permitted_affix_ids=["atom.a", "atom.b", "atom.c"],
            anti_motif_tags=["anti"], affix_vocab=VOCAB)
        self.assertIn("affixIds", defects)

    def test_a_clean_affix_pick_has_no_defect(self) -> None:
        defects = run.brief_conformance_defects(
            _response(affix_ids=["atom.a"]), permitted_affix_ids=["atom.a", "atom.b", "atom.c"],
            anti_motif_tags=["anti"], affix_vocab=VOCAB)
        self.assertEqual(defects, {})

    def test_an_affix_outside_the_permitted_set_is_never_double_reported_here(self) -> None:
        """Already a gate-7 (`run_g1`) enum violation — this function's own job is narrower."""
        defects = run.brief_conformance_defects(
            _response(affix_ids=["atom.zzz"]), permitted_affix_ids=["atom.a", "atom.b"],
            anti_motif_tags=["anti"], affix_vocab=VOCAB)
        self.assertEqual(defects, {})


class BuildResponseGateTests(unittest.TestCase):
    def setUp(self) -> None:
        from seedsmith.adapters.trees.nodegen.schema import schema_for_call
        self.schema = schema_for_call(["atom.a", "atom.b", "atom.c"], ["posture"])
        self.gate = run.build_response_gate(
            schema=self.schema, permitted_affix_ids=["atom.a", "atom.b", "atom.c"],
            permitted_property_keys=["posture"], anti_motif_tags=["anti"], affix_vocab=VOCAB,
            property_vocabulary={"posture": ("vanguard", "warden")},
            tree_display_name="Might", motifs=["strength"])

    def test_a_clean_response_passes(self) -> None:
        self.assertEqual(self.gate(_response(affix_ids=["atom.a"])), [])

    def test_a_contract_violation_is_reported(self) -> None:
        bad = _response(affix_ids=["atom.a"])
        del bad["nameKey"]
        self.assertTrue(self.gate(bad))

    def test_an_anti_motif_pick_is_reported(self) -> None:
        self.assertTrue(self.gate(_response(affix_ids=["atom.c"])))

    def test_a_name_that_echoes_the_tree_display_name_is_reported(self) -> None:
        self.assertTrue(self.gate(_response(name="Might")))

    def test_an_illegal_exclusion_property_key_is_reported(self) -> None:
        bad = _response(exclusion={"form": "reroute", "propertyKeys": ["not-a-real-property"]})
        self.assertTrue(self.gate(bad))


class CallOneNodeSampleOfflineGuaranteeTests(unittest.TestCase):
    def test_never_reaches_the_model_when_nothing_calls_it(self) -> None:
        """Assembling a brief/schema/gate makes zero calls by construction — proven by patching
        the real transport with a stub that raises the moment it would be reached."""
        from seedsmith.adapters.trees.nodegen.brief import render_brief
        from seedsmith.adapters.trees.nodegen.schema import schema_for_call

        with patch("seedsmith.pipeline.llm_caller.call_model", raising_call):
            schema = schema_for_call(["atom.a"], ["posture"])
            gate = run.build_response_gate(
                schema=schema, permitted_affix_ids=["atom.a"], permitted_property_keys=["posture"],
                anti_motif_tags=[], affix_vocab=VOCAB, property_vocabulary={"posture": ("vanguard",)},
                tree_display_name="Might", motifs=["strength"])
            self.assertEqual(gate(_response(affix_ids=["atom.a"])), [])
            text = render_brief(
                node_id="skill.might-off-t1-n0", sample_index=0, tree_display_name="Might",
                tree_reading="reading", branch="offensive", tier=1, node_class="mechanism",
                motifs=["strength"], anti_motifs=[], permitted_affixes=[AFFIX_A],
                permitted_properties=["posture"])
            self.assertIn("Might", text)


class GenerateNodeAcceptedTests(unittest.TestCase):
    def test_a_unanimous_vote_is_accepted(self) -> None:
        payload = json.dumps(_response(affix_ids=["atom.a"]))
        with patch("seedsmith.pipeline.llm_caller.call_model", return_value=payload):
            outcome = run.generate_node(_subject(), _inputs(), config=TEST_CONFIG)
        self.assertEqual(outcome.outcome, "accepted")
        self.assertIsNotNone(outcome.record)
        self.assertEqual(outcome.record.affix_ids, ("atom.a",))

    def test_a_declared_block_on_the_base_call_is_reported_not_escalated(self) -> None:
        blocked = {**_response(), "blocked": "no legal effect fits this brief"}
        with patch("seedsmith.pipeline.llm_caller.call_model", return_value=json.dumps(blocked)):
            outcome = run.generate_node(_subject(), _inputs(), config=TEST_CONFIG)
        self.assertEqual(outcome.outcome, "blocked")

    def test_a_response_that_never_satisfies_the_gate_escalates(self) -> None:
        always_bad = json.dumps({"affixIds": ["atom.zzz"]})  # outside the permitted set, forever
        with patch("seedsmith.pipeline.llm_caller.call_model", return_value=always_bad):
            outcome = run.generate_node(_subject(), _inputs(), config=TEST_CONFIG)
        self.assertEqual(outcome.outcome, "escalated")

    def test_a_1_1_1_vote_is_unresolved(self) -> None:
        """Three distinct single-member picks (none anti-motif-tagged, so every individual call
        passes its own per-call gate) -- no member reaches the 2-of-3 majority threshold."""
        responses = [
            json.dumps(_response(affix_ids=["atom.a"])),
            json.dumps(_response(affix_ids=["atom.b"])),
            json.dumps(_response(affix_ids=["atom.d"])),
        ]
        with patch("seedsmith.pipeline.llm_caller.call_model", side_effect=responses):
            outcome = run.generate_node(_subject(), _inputs(), config=TEST_CONFIG)
        self.assertEqual(outcome.outcome, "unresolved")

    def test_persist_time_re_gate_catches_a_voted_composite_the_base_call_alone_would_pass(self) -> None:
        """§7 gate 13's own reason to exist. Every INDIVIDUAL sample response is internally clean
        (its own `affixIds` and `affinity` agree in length, and neither uses an anti-motif affix) —
        so no per-call gate check ever rejects any of the three calls. But `resolve_set_vote` is a
        per-MEMBER majority (§7 gate 11): `atom.a` is unanimous (3/3), `atom.b` reaches the 2-of-3
        threshold from samples 1-2 alone, so the vote resolves to the TWO-member set `{a, b}` even
        though the base call (sample 0, whose OTHER fields the final record otherwise uses) only
        ever answered with the ONE-member set `{a}` — its own `affinity` array has length 1. The
        persisted composite (`affixIds=[a,b]`, `affinity=[core]`) is a length mismatch §6.3
        requires never happen, and it is invisible to every per-call check, because no single call
        ever produced it."""
        responses = [
            json.dumps(_response(affix_ids=["atom.a"])),               # base call, sample 0
            json.dumps(_response(affix_ids=["atom.a", "atom.b"])),      # vote sample 1
            json.dumps(_response(affix_ids=["atom.a", "atom.b"])),      # vote sample 2
        ]
        with patch("seedsmith.pipeline.llm_caller.call_model", side_effect=responses):
            outcome = run.generate_node(_subject(), _inputs(), config=TEST_CONFIG)
        self.assertEqual(outcome.outcome, "escalated")
        self.assertIn("persist time", outcome.detail)


class RecordAcceptedIdempotenceTests(unittest.TestCase):
    def test_a_second_record_for_the_same_subject_raises(self) -> None:
        record = run.build_node_record("skill.t1-off-t1-n0", "n0", "offensive", 1, "mechanism",
                                       _response())
        done = run.record_accepted({}, "t1:skill.t1-off-t1-n0", record)
        with self.assertRaises(ValueError):
            run.record_accepted(done, "t1:skill.t1-off-t1-n0", record)

    def test_recording_never_mutates_the_callers_dict(self) -> None:
        record = run.build_node_record("skill.t1-off-t1-n0", "n0", "offensive", 1, "mechanism",
                                       _response())
        original: "dict" = {}
        run.record_accepted(original, "t1:skill.t1-off-t1-n0", record)
        self.assertEqual(original, {})


if __name__ == "__main__":
    unittest.main()
