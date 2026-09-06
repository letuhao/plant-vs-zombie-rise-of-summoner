"""Tests for seedsmith.adapters.trees.nodegen.brief (task H1) — the per-node §6.2 brief text;
permutation seeded from `nodeId|field|sampleIndex`; no number ever appears in the rendered text.
"""
from __future__ import annotations

import unittest

from seedsmith.adapters.trees.nodegen import brief
from seedsmith.adapters.trees.nodegen.vocab import AffixOption


def _affixes() -> "list[AffixOption]":
    return [
        AffixOption(affix_id="atom.freezing", name="Killing Frost", tags=("offensive",),
                   kind_id="status.apply"),
        AffixOption(affix_id="atom.bulwark", name="Bulwark", tags=("defensive",),
                   kind_id="stat.modify"),
        AffixOption(affix_id="atom.venomous", name="Venomous", tags=("offensive",),
                   kind_id="status.apply"),
    ]


class TierToDepthTests(unittest.TestCase):
    def test_shallow_mid_deep_partition_all_ten_tiers(self) -> None:
        bands = {brief.tier_to_depth(t, 10) for t in range(1, 11)}
        self.assertEqual(bands, {"shallow", "mid", "deep"})

    def test_tier_one_is_shallow_and_tier_ten_is_deep(self) -> None:
        self.assertEqual(brief.tier_to_depth(1, 10), "shallow")
        self.assertEqual(brief.tier_to_depth(10, 10), "deep")

    def test_out_of_range_tier_raises(self) -> None:
        with self.assertRaises(ValueError):
            brief.tier_to_depth(0, 10)
        with self.assertRaises(ValueError):
            brief.tier_to_depth(11, 10)


class PermutationTests(unittest.TestCase):
    def test_permuted_affix_ids_is_deterministic_for_the_same_seed(self) -> None:
        a = brief.permuted_affix_ids("skill.t-off-t1-n0", 0, _affixes())
        b = brief.permuted_affix_ids("skill.t-off-t1-n0", 0, _affixes())
        self.assertEqual([o.affix_id for o in a], [o.affix_id for o in b])

    def test_a_different_sample_index_can_reorder(self) -> None:
        """Not asserted as a strict inequality (a shuffle CAN coincide by chance for tiny lists),
        but the returned set must always be identical regardless of order."""
        a = brief.permuted_affix_ids("skill.t-off-t1-n0", 0, _affixes())
        b = brief.permuted_affix_ids("skill.t-off-t1-n0", 1, _affixes())
        self.assertEqual({o.affix_id for o in a}, {o.affix_id for o in b})

    def test_permuted_property_keys_never_invents_a_key(self) -> None:
        keys = ["posture", "conversionState"]
        result = brief.permuted_property_keys("skill.t-off-t1-n0", 0, keys)
        self.assertEqual(set(result), set(keys))


class RenderBriefTests(unittest.TestCase):
    def test_render_contains_only_the_permitted_affixes(self) -> None:
        text = brief.render_brief(
            node_id="skill.t-off-t1-n0", sample_index=0, tree_display_name="Might",
            tree_reading="raw physical force", branch="offensive", tier=1, node_class="mechanism",
            motifs=["ferocity"], anti_motifs=["cowardice"],
            permitted_affixes=_affixes()[:2], permitted_properties=["posture"],
        )
        self.assertIn("atom.freezing", text)
        self.assertIn("atom.bulwark", text)
        self.assertNotIn("atom.venomous", text)

    def test_render_never_contains_a_bare_tier_number(self) -> None:
        text = brief.render_brief(
            node_id="skill.t-off-t1-n0", sample_index=0, tree_display_name="Might",
            tree_reading="raw physical force", branch="offensive", tier=7, node_class="magnitude",
            motifs=[], anti_motifs=[], permitted_affixes=_affixes(), permitted_properties=[],
        )
        self.assertNotIn("tier: 7", text.lower())
        self.assertNotIn("tier 7", text.lower())
        self.assertIn("deep", text)  # the label this stage sees instead

    def test_render_rejects_an_illegal_branch_or_node_class(self) -> None:
        with self.assertRaises(ValueError):
            brief.render_brief(
                node_id="n", sample_index=0, tree_display_name="x", tree_reading="y",
                branch="neutral", tier=1, node_class="mechanism", motifs=[], anti_motifs=[],
                permitted_affixes=[], permitted_properties=[],
            )
        with self.assertRaises(ValueError):
            brief.render_brief(
                node_id="n", sample_index=0, tree_display_name="x", tree_reading="y",
                branch="offensive", tier=1, node_class="ultimate", motifs=[], anti_motifs=[],
                permitted_affixes=[], permitted_properties=[],
            )

    def test_render_includes_siblings_when_given(self) -> None:
        siblings = [brief.SiblingSummary(node_id="skill.t-off-t1-n1", name="Chill Touch",
                                         affix_ids=("atom.freezing",))]
        text = brief.render_brief(
            node_id="skill.t-off-t1-n0", sample_index=0, tree_display_name="Might",
            tree_reading="raw physical force", branch="offensive", tier=1, node_class="mechanism",
            motifs=[], anti_motifs=[], permitted_affixes=_affixes(), permitted_properties=[],
            siblings=siblings,
        )
        self.assertIn("Chill Touch", text)


if __name__ == "__main__":
    unittest.main()
