#!/usr/bin/env python3
"""Tests for the reachability checks.

    python tools/seed_graph/test_reachability.py

Synthetic corpora only, never the live one: these must keep failing on a broken graph long after
the real corpus is fixed, and a test that reads shipping content stops testing the day the content
changes.

Every check gets a pair — one corpus that must trip it and one that must not. The negative half is
the important half, because a reachability checker that fires on everything is indistinguishable
from one that has understood nothing, and the categorical-vs-specific grant distinction is exactly
where that goes wrong.
"""

from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from seedgraph import Acquisition, Corpus, GAP, run_all  # noqa: E402


def build(*specs):
    """Write a throwaway corpus from (kind, entries) pairs and load it back.

    Kinds are passed explicitly rather than as keyword arguments because the corpus kind is
    singular (`set`, `gem`) while its directory is plural — inferring one from the other is how
    this harness first produced seven false failures.
    """
    tmp = Path(tempfile.mkdtemp())
    for kind, entries in specs:
        directory = tmp / (kind + "s")
        directory.mkdir(parents=True, exist_ok=True)
        doc = {
            "schemaVersion": 1,
            "kind": kind,
            "_meta": {"partition": kind + "/test"},
            "entries": entries,
        }
        (directory / "test.json").write_text(json.dumps(doc), encoding="utf-8")
    corpus = Corpus.load(tmp)
    return corpus, Acquisition.build(corpus)


def codes(corpus, acquisition, severity=None):
    return [f.code for f in run_all(corpus, acquisition)
            if severity is None or f.severity == severity]


def drop_table(*entries):
    return ("drop-table", [{
        "id": "droptable.d1-001", "name": "Cache",
        "groups": [{"groupKey": "g", "entries": list(entries)}],
    }])


SET_BY_ROLE = {
    "id": "set.test-001", "name": "Testset",
    "members": [{"role": "core-guard"}, {"role": "head-guard"}],
    "thresholds": [{"pieces": 2, "atoms": []}],
}
SET_PINNED = {
    "id": "set.test-001", "name": "Testset",
    "members": [
        {"role": "core-guard", "frame": "plant", "baseType": "item.plant-stem-a-001"},
        {"role": "head-guard", "frame": "plant", "baseType": "item.plant-crown-a-001"},
    ],
    "thresholds": [{"pieces": 2, "atoms": []}],
}


class SetCompletability(unittest.TestCase):
    def test_members_by_role_alone_cannot_complete(self):
        """The finding this whole tool exists for. Nothing is a member, so no bonus is earnable."""
        corpus, acq = build(("set", [SET_BY_ROLE]))
        self.assertIn("SetUncompletable", codes(corpus, acq, GAP))

    def test_pinned_members_complete(self):
        corpus, acq = build(("set", [SET_PINNED]))
        self.assertNotIn("SetUncompletable", codes(corpus, acq))

    def test_threshold_above_member_count_is_a_gap(self):
        entry = dict(SET_PINNED, thresholds=[{"pieces": 2}, {"pieces": 4}])
        corpus, acq = build(("set", [entry]))
        self.assertIn("SetShortOfThreshold", codes(corpus, acq, GAP))


class Obtainability(unittest.TestCase):
    def test_a_gem_no_table_yields_is_a_gap(self):
        corpus, acq = build(("gem", [{"id": "gem.g1-001", "name": "Ember Shard"}]))
        self.assertIn("Unobtainable", codes(corpus, acq, GAP))

    def test_a_gem_named_by_a_drop_table_is_reachable(self):
        corpus, acq = build(
            ("gem", [{"id": "gem.g1-001", "name": "Ember Shard"}]),
            drop_table({"entryKind": "insert", "ref": "gem.g1-001", "dropBand": "seldom"}))
        self.assertNotIn("Unobtainable", codes(corpus, acq))

    def test_base_types_are_reached_categorically_not_by_name(self):
        """A drop table grants equipment by role and frame; it never names the 740 base types.

        Requiring a specific grant here would report the entire equipment corpus as unobtainable —
        the single most likely way for this tool to be confidently wrong.
        """
        corpus, acq = build(
            ("base-type", [{"id": "item.plant-stem-a-001", "name": "Stem",
                            "role": "core-guard", "frame": "plant"}]),
            drop_table({"entryKind": "equipment", "role": "core-guard", "frame": "plant",
                        "dropBand": "occasional"}))
        self.assertNotIn("Unobtainable", codes(corpus, acq))
        self.assertNotIn("SlotUncovered", codes(corpus, acq))

    def test_an_uncovered_role_frame_slot_is_a_gap(self):
        corpus, acq = build(
            ("base-type", [{"id": "item.plant-roots-a-001", "name": "Roots",
                            "role": "footing", "frame": "plant"}]))
        self.assertIn("SlotUncovered", codes(corpus, acq, GAP))

    def test_a_material_is_reached_by_its_runtime_id(self):
        """Tracking id vs runtime id — the split that has produced three separate defects here."""
        corpus, acq = build(
            ("material", [{"id": "material.001", "name": "Ash", "runtimeId": "essence.fire"}]),
            drop_table({"entryKind": "material", "ref": "essence.fire", "dropBand": "frequent"}))
        self.assertNotIn("Unobtainable", codes(corpus, acq))

    def test_a_recipe_output_counts_as_acquisition(self):
        corpus, acq = build(
            ("gem", [{"id": "gem.g1-001", "name": "Ember Shard"}]),
            ("recipe", [{"id": "recipe.001", "name": "Forge: Shard", "operation": "forge",
                         "outputKind": "container", "outputRef": "gem.g1-001", "costLines": []}]))
        self.assertNotIn("Unobtainable", codes(corpus, acq))


class Ingredients(unittest.TestCase):
    GEM = ("gem", [{"id": "gem.g1-001", "name": "Ember Shard",
                    "family": "atom.vitality", "powerBand": "low"}])

    def test_a_word_needing_an_unsupplied_family_is_a_gap(self):
        corpus, acq = build(self.GEM, ("socket-word", [{
            "id": "sockword.001", "name": "Dual Strike",
            "ingredients": [{"position": 0, "family": "atom.nonexistent",
                             "minPowerBand": "high"}]}]))
        self.assertIn("IngredientUnsatisfiable", codes(corpus, acq, GAP))

    def test_a_word_whose_families_all_exist_is_fine(self):
        corpus, acq = build(self.GEM, ("socket-word", [{
            "id": "sockword.001", "name": "Dual Strike",
            "ingredients": [{"position": 0, "family": "atom.vitality",
                             "minPowerBand": "low"}]}]))
        self.assertNotIn("IngredientUnsatisfiable", codes(corpus, acq))


class RecipeInputs(unittest.TestCase):
    @staticmethod
    def recipe(material):
        return ("recipe", [{"id": "recipe.001", "name": "Forge: Thing", "operation": "forge",
                            "outputKind": "mutation",
                            "costLines": [{"material": material, "costBand": "modest"}]}])

    def test_spending_an_unobtainable_material_is_a_gap(self):
        corpus, acq = build(self.recipe("essence.void"))
        self.assertIn("RecipeInputUnobtainable", codes(corpus, acq, GAP))

    def test_spending_a_dropped_material_is_fine(self):
        corpus, acq = build(
            self.recipe("essence.fire"),
            drop_table({"entryKind": "material", "ref": "essence.fire", "dropBand": "frequent"}))
        self.assertNotIn("RecipeInputUnobtainable", codes(corpus, acq))


class EnhancementTrack(unittest.TestCase):
    MILESTONE = ("enhancement-milestone", [{"id": "enh.001", "name": "Enhancement Vigor"}])

    def test_milestones_with_no_track_anywhere_are_unreachable(self):
        corpus, acq = build(self.MILESTONE, ("base-type", [
            {"id": "item.plant-stem-a-001", "name": "Stem",
             "role": "core-guard", "frame": "plant"}]))
        self.assertIn("FeatureUnbound", codes(corpus, acq, GAP))

    def test_one_base_type_with_a_track_binds_the_feature(self):
        corpus, acq = build(self.MILESTONE, ("base-type", [
            {"id": "item.plant-stem-a-001", "name": "Stem",
             "role": "core-guard", "frame": "plant",
             "enhanceTrack": [{"milestone": 4, "family": "atom.enhance-vigor"}]}]))
        self.assertNotIn("FeatureUnbound", codes(corpus, acq))


class EmptyCorpus(unittest.TestCase):
    def test_nothing_to_say_about_nothing(self):
        corpus, acq = build()
        self.assertEqual([], run_all(corpus, acq))


if __name__ == "__main__":
    unittest.main(verbosity=2)
