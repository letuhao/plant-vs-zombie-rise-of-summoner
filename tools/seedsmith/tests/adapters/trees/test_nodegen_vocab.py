"""Tests for seedsmith.adapters.trees.nodegen.vocab (task H1) — the affix pick vocabulary, counted
fresh from the real corpus, never transcribed (spec-tree-language.md §3 rows 11-12, §5.1).
"""
from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from seedsmith.adapters.trees.nodegen import vocab


class RealCorpusCountsTests(unittest.TestCase):
    """Reads the REAL committed affix-family corpus — the same one `items/setgen/vocab.py`
    reads — so a drift in the corpus is caught here too, never silently re-derived."""

    def test_counts_125_families_and_their_tag_values(self) -> None:
        # The committed affix corpus grew again after the 112-family snapshot (owner commits
        # 27e908d3/3f9afb44 added thirteen offensive, seven defensive and two utility families,
        # plus new non-branch tags: metal/sturdy/arcane/mechanical, which never match a branch
        # cut). Keep this acceptance value explicit so a corpus change is reviewed here.
        vocabulary = vocab.build()
        self.assertEqual(vocabulary.count, 125)
        tag_counts = vocabulary.tag_counts()
        self.assertEqual(tag_counts["offensive"], 58)
        self.assertEqual(tag_counts["defensive"], 47)
        self.assertEqual(tag_counts["utility"], 19)

    def test_ids_are_unique_and_get_resolves_a_real_one(self) -> None:
        vocabulary = vocab.build()
        ids = vocabulary.ids()
        self.assertEqual(len(ids), len(set(ids)))
        option = vocabulary.get(ids[0])
        self.assertEqual(option.affix_id, ids[0])

    def test_get_raises_on_an_unknown_id(self) -> None:
        vocabulary = vocab.build()
        with self.assertRaises(vocab.AffixVocabularyError):
            vocabulary.get("atom.does-not-exist")

    def test_one_line_never_carries_the_display_template_magnitude_placeholder(self) -> None:
        """§5's own reasoning applies here too: a brief must never carry a number. `one_line` uses
        the affix's `name`, never its `displayTemplate` (which embeds a `{value}%`-shaped
        placeholder) — this proves it structurally rather than by eyeballing one entry."""
        vocabulary = vocab.build()
        for option in vocabulary.options:
            self.assertNotIn("{value}", option.one_line)


class EmptyCorpusRefusalTests(unittest.TestCase):
    def test_an_empty_directory_is_refused_never_widened_to_an_empty_vocabulary(self) -> None:
        empty_dir = Path(tempfile.mkdtemp())
        with self.assertRaises(vocab.AffixVocabularyError):
            vocab.build(empty_dir)


class PermittedForBranchTests(unittest.TestCase):
    """task H3: the one real, unblocked cut this vocabulary supports today — branch tag plus
    'utility' — pending the atom-tag registry the module's own docstring names by name."""

    def setUp(self) -> None:
        self.vocabulary = vocab.build()

    def test_offensive_branch_gets_offensive_and_utility_tagged_affixes(self) -> None:
        options = self.vocabulary.permitted_for_branch("offensive")
        self.assertTrue(options)
        for option in options:
            self.assertTrue("offensive" in option.tags or "utility" in option.tags)
        self.assertEqual(len(options), len([o for o in self.vocabulary.options
                                            if "offensive" in o.tags or "utility" in o.tags]))

    def test_defensive_branch_gets_defensive_and_utility_tagged_affixes(self) -> None:
        options = self.vocabulary.permitted_for_branch("defensive")
        self.assertTrue(options)
        for option in options:
            self.assertTrue("defensive" in option.tags or "utility" in option.tags)

    def test_an_illegal_branch_refuses(self) -> None:
        with self.assertRaises(ValueError):
            self.vocabulary.permitted_for_branch("sideways")

    def test_offensive_and_defensive_never_share_a_non_utility_only_affix(self) -> None:
        """An affix tagged ONLY 'offensive' must never show up in the defensive branch's own
        permitted subset, and vice versa -- utility is the only legal overlap."""
        offensive = {o.affix_id for o in self.vocabulary.permitted_for_branch("offensive")
                    if "utility" not in o.tags}
        defensive = {o.affix_id for o in self.vocabulary.permitted_for_branch("defensive")
                    if "utility" not in o.tags}
        self.assertEqual(offensive & defensive, set())


if __name__ == "__main__":
    unittest.main()
