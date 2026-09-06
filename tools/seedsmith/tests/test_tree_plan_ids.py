"""Tests for seedsmith.adapters.trees.plan.ids (ruling R3, task B1).

    python -m pytest tools/seedsmith/tests/test_tree_plan_ids.py -v
"""
from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.adapters.trees.plan.ids import (  # noqa: E402
    IdMintError,
    mint_node_keys,
    node_id,
    refuse_if_key_reused,
)


class NodeIdGrammarTests(unittest.TestCase):
    def test_the_shape_is_exactly_skill_tree_branch_t_tier_key(self) -> None:
        self.assertEqual(node_id("might", "offensive", 3, "n0"), "skill.might-off-t3-n0")
        self.assertEqual(node_id("might", "defensive", 10, "n19"), "skill.might-def-t10-n19")

    def test_no_slash_or_dot_inside_the_body(self) -> None:
        result = node_id("might", "offensive", 1, "n0")
        body = result[len("skill."):]
        self.assertNotIn("/", body)
        self.assertNotIn(".", body)

    def test_unknown_branch_is_refused(self) -> None:
        with self.assertRaises(IdMintError):
            node_id("might", "sideways", 1, "n0")

    def test_bad_tree_slug_is_refused(self) -> None:
        with self.assertRaises(IdMintError):
            node_id("Might", "offensive", 1, "n0")  # uppercase
        with self.assertRaises(IdMintError):
            node_id("might tree", "offensive", 1, "n0")  # space

    def test_tier_below_one_is_refused(self) -> None:
        with self.assertRaises(IdMintError):
            node_id("might", "offensive", 0, "n0")


class MintKeysTests(unittest.TestCase):
    def test_first_mint_produces_fresh_ordinal_keys(self) -> None:
        keys = mint_node_keys("might", "offensive", 1, 2, {}, {})
        self.assertEqual(keys, ["n0", "n1"])

    def test_a_committed_key_is_read_back_verbatim_not_re_minted(self) -> None:
        # R3: nodeKey is minted ONCE and read back — a hand-authored key from a previous run must
        # survive a re-emit unchanged, even if it doesn't look like this module's own naming scheme.
        existing = {("offensive", 1, 0): "some-hand-picked-name"}
        keys = mint_node_keys("might", "offensive", 1, 2, existing, {})
        self.assertEqual(keys[0], "some-hand-picked-name")
        self.assertEqual(keys[1], "n0")  # the new slot gets a fresh key, unaffected by the old one's name

    def test_reading_back_does_not_disturb_the_fresh_counter(self) -> None:
        # If slot 0 is already named oddly, slot 1's fresh key must not collide with a NUMBER that
        # slot 0's name happens to look like — the counter is independent of read-back content.
        existing = {("offensive", 1, 0): "n99"}
        keys = mint_node_keys("might", "offensive", 1, 2, existing, {})
        self.assertEqual(keys, ["n99", "n0"])

    def test_two_different_slots_in_the_same_run_never_reuse_a_fresh_ordinal(self) -> None:
        next_ordinal: "dict[tuple[str, str, int], int]" = {}
        first = mint_node_keys("might", "offensive", 1, 2, {}, next_ordinal)
        second = mint_node_keys("might", "offensive", 1, 1, {}, next_ordinal)  # same slot key space
        self.assertEqual(set(first) & set(second), set())

    def test_different_tiers_have_independent_counters(self) -> None:
        next_ordinal: "dict[tuple[str, str, int], int]" = {}
        tier1 = mint_node_keys("might", "offensive", 1, 2, {}, next_ordinal)
        tier2 = mint_node_keys("might", "offensive", 2, 2, {}, next_ordinal)
        # Independent counters means both start at n0 — different tiers, no shared key space.
        self.assertEqual(tier1, ["n0", "n1"])
        self.assertEqual(tier2, ["n0", "n1"])


class ReuseRefusalTests(unittest.TestCase):
    def test_no_reuse_passes(self) -> None:
        refuse_if_key_reused(["n0", "n1", "n2"])  # must not raise

    def test_a_repeated_key_in_one_slot_is_refused(self) -> None:
        with self.assertRaises(IdMintError):
            refuse_if_key_reused(["n0", "n0"])


if __name__ == "__main__":
    unittest.main()
