"""Tests for seedsmith.adapters.trees.plan.{vocabulary,emit,tuning} (task B1) — the full one-tree
plan, end to end, against the real committed roster mirrors.

    python -m pytest tools/seedsmith/tests/test_tree_plan_emit.py -v
"""
from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.adapters.trees.plan import emit as plan_emit  # noqa: E402
from seedsmith.adapters.trees.plan import tuning as plan_tuning  # noqa: E402
from seedsmith.adapters.trees.plan import vocabulary  # noqa: E402
from seedsmith.adapters.trees.plan.ids import IdMintError  # noqa: E402


def real_seed_root() -> Path:
    dir_ = Path(__file__).resolve()
    while dir_ != dir_.parent and not (dir_ / "AGENTS.md").exists():
        dir_ = dir_.parent
    return dir_ / "data" / "seed"


class RosterAndVocabularyTests(unittest.TestCase):
    """Reads the REAL committed mirrors (A1-A3's own output) — no fixture substitutes them, since
    B1's whole point is to prove it reads live counts, never typed ones."""

    def test_roster_counts_match_the_spec(self) -> None:
        roster = vocabulary.load_roster(real_seed_root())
        self.assertEqual(len(roster.aptitudes), 12)
        self.assertEqual(len(roster.elements), 6)
        # D51 (2026-09-06): 21 -> 24, the live StatusCategoryRegistry's three nerve.* statuses
        # (afflicted, shaken, unsettled) accepted into this program's own corpus, re-baked via
        # `PassiveTreeRosterGen --status-emit`.
        self.assertEqual(len(roster.statuses), 24)

    def test_property_vocabulary_counts_match_the_spec_table(self) -> None:
        vocab = vocabulary.load_property_vocabulary(10, real_seed_root())
        self.assertEqual(vocab.counts["nodeClass"], 2)
        self.assertEqual(vocab.counts["branch"], 2)
        self.assertEqual(vocab.counts["tier"], 10)
        self.assertEqual(vocab.counts["posture"], 3)
        self.assertEqual(vocab.counts["aptitude"], 12)
        self.assertEqual(vocab.counts["element"], 7)  # 6 + omni
        self.assertEqual(vocab.counts["status"], 24)  # D51: 21 -> 24, three nerve.* statuses accepted
        self.assertEqual(vocab.counts["atomAttachPoint"], 7)
        self.assertEqual(vocab.counts["atomKind"], 16)
        self.assertEqual(vocab.counts["atomTrigger"], 13)
        self.assertEqual(vocab.counts["atomTriggerAuthorable"], 11)
        self.assertEqual(vocab.counts["channelFamily"], 54)  # D52: 53 -> 54, live derived-stats growth
        self.assertEqual(vocab.counts["conversionState"], 2)
        self.assertEqual(vocab.counts["exclusionForm"], 3)

    def test_a_missing_mirror_is_EXIT_CANNOT_RUN_naming_the_file_never_an_empty_axis(self) -> None:
        empty_root = Path(tempfile.mkdtemp())
        with self.assertRaises(vocabulary.VocabularyError) as ex:
            vocabulary.load_roster(empty_root)
        self.assertIn("roster.json", str(ex.exception))


class TuningTests(unittest.TestCase):
    def test_the_live_tuning_file_loads_every_key_tree_plan_owns(self) -> None:
        t = plan_tuning.load()
        self.assertEqual(t["tierLadder"]["reqScalePoints"], 5)
        self.assertEqual(t["archetype"]["rewardSpreadMaxRatioMilli"], 6000)
        self.assertEqual(t["potency"]["maxNodeShareMilli"], 182)


class BuildPlanTests(unittest.TestCase):
    def setUp(self) -> None:
        self.tuning = plan_tuning.load()
        self.spec = plan_emit.might_tree_spec()

    def test_forty_nodes_twenty_per_branch_rootless(self) -> None:
        plan = plan_emit.build_plan(self.spec, self.tuning)
        self.assertEqual(len(plan["nodes"]), 40)
        offensive = [n for n in plan["nodes"] if n["branch"] == "offensive"]
        defensive = [n for n in plan["nodes"] if n["branch"] == "defensive"]
        self.assertEqual(len(offensive), 20)
        self.assertEqual(len(defensive), 20)
        # Rootless: every node has a real (branch, tier) — none is a shared parentless root.
        for n in plan["nodes"]:
            self.assertIn(n["branch"], ("offensive", "defensive"))
            self.assertGreaterEqual(n["tier"], 1)

    def test_node_ids_match_the_grammar(self) -> None:
        plan = plan_emit.build_plan(self.spec, self.tuning)
        for n in plan["nodes"]:
            self.assertTrue(n["id"].startswith("skill.might-"))
            self.assertNotIn("/", n["id"][len("skill."):])

    def test_tier_budget_sums_to_1000_with_zero_residual_at_ten_tiers(self) -> None:
        plan = plan_emit.build_plan(self.spec, self.tuning)
        self.assertEqual(sum(plan["budget"]["tierShareMilli"]), 1000)

    def test_w_over_req_is_b_over_5_at_all_ten_tiers(self) -> None:
        plan = plan_emit.build_plan(self.spec, self.tuning)
        # req values ARE W/req's denominator at b=1 (ladder.py's own convention) — cross-check
        # against the spec table directly here too, at the plan level.
        self.assertEqual(plan["ladder"]["reqByTier"], [5, 15, 30, 50, 75, 105, 140, 180, 225, 275])

    def test_property_vocabulary_is_emitted_with_thirteen_axes(self) -> None:
        plan = plan_emit.build_plan(self.spec, self.tuning)
        expected_axes = {"nodeClass", "branch", "tier", "posture", "aptitude", "element", "status",
                         "atomAttachPoint", "atomKind", "atomTrigger", "channelFamily",
                         "conversionState", "exclusionForm"}
        self.assertEqual(set(plan["propertyVocabulary"].keys()), expected_axes)

    def test_no_hardcoded_roster_count_reaches_the_plan_wrongly(self) -> None:
        # If A3's mirror grows (e.g. a 22nd status), the plan must reflect it with zero code change
        # — proven here by asserting the plan's own count equals the LIVE mirror's count, not a
        # literal this test itself types as an assumption.
        plan = plan_emit.build_plan(self.spec, self.tuning)
        live_roster = vocabulary.load_roster()
        self.assertEqual(plan["propertyVocabularyCounts"]["status"], len(live_roster.statuses))

    def test_r_g0_gate_currency_is_aptitude_points(self) -> None:
        plan = plan_emit.build_plan(self.spec, self.tuning)
        self.assertEqual(plan["ladder"]["gateCurrency"], "aptitudePoints")

    def test_archetype_assigned_to_might_is_broad_and_flat(self) -> None:
        # Might is aptitude ordinal 0 -> archetypes[0 % 3] = broad-and-flat.
        plan = plan_emit.build_plan(self.spec, self.tuning)
        self.assertEqual(plan["archetype"], "broad-and-flat")


class EmitCheckRoundTripTests(unittest.TestCase):
    """Uses a scratch seed root so this suite never touches the real committed
    data/seed/passive-tree/plan/might.v1.json."""

    def setUp(self) -> None:
        self.scratch = Path(tempfile.mkdtemp())
        self.tuning = plan_tuning.load()
        self.spec = plan_emit.might_tree_spec()

    def _emit_to_scratch(self) -> Path:
        path = self.scratch / "might.v1.json"
        existing = plan_emit._read_existing_plan(path)
        plan = plan_emit.build_plan(self.spec, self.tuning, existing_plan=existing)
        path.write_text(json.dumps(plan, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        return path

    def test_re_emit_is_byte_identical(self) -> None:
        path = self._emit_to_scratch()
        first = path.read_text(encoding="utf-8")
        second_text = self._emit_to_scratch().read_text(encoding="utf-8")
        self.assertEqual(first, second_text)

    def test_a_hand_corrupted_budget_column_fails_check(self) -> None:
        path = self._emit_to_scratch()
        doc = json.loads(path.read_text(encoding="utf-8"))
        doc["nodes"][0]["budgetShareMilli"] = 999999
        path.write_text(json.dumps(doc, indent=2, sort_keys=True), encoding="utf-8")

        existing = plan_emit._read_existing_plan(path)
        regenerated = plan_emit.build_plan(self.spec, self.tuning, existing_plan=existing)
        diffs: "list[str]" = []
        plan_emit._diff_json(doc, regenerated, "$", diffs)
        self.assertTrue(any("budgetShareMilli" in d for d in diffs))

    def test_re_emit_reads_back_existing_keys_rather_than_re_minting(self) -> None:
        # No hyphens: the id grammar uses "-" as the field separator, so a nodeKey containing one
        # would make skill.<tree>-<branch>-t<tier>-<nodeKey> unparseable back into its parts —
        # ids.py's own regex correctly refuses it (see NodeIdGrammarTests).
        path = self._emit_to_scratch()
        doc = json.loads(path.read_text(encoding="utf-8"))
        original_first_key = doc["nodes"][0]["nodeKey"]
        doc["nodes"][0]["nodeKey"] = "handpicked"
        path.write_text(json.dumps(doc, indent=2, sort_keys=True), encoding="utf-8")

        existing = plan_emit._read_existing_plan(path)
        regenerated = plan_emit.build_plan(self.spec, self.tuning, existing_plan=existing)
        self.assertEqual(regenerated["nodes"][0]["nodeKey"], "handpicked")
        self.assertNotEqual(regenerated["nodes"][0]["nodeKey"], original_first_key)

    def test_the_real_committed_plan_agrees_with_a_fresh_check(self) -> None:
        real_path = real_seed_root() / "passive-tree" / "plan" / "might.v1.json"
        if not real_path.exists():
            self.skipTest("might.v1.json not yet committed in this checkout")
        diffs = plan_emit.check(self.spec, self.tuning)
        self.assertEqual(diffs, [])


if __name__ == "__main__":
    unittest.main()
