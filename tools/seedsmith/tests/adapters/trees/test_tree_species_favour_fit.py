"""Tests for seedsmith.adapters.trees.species.generate_favour_fit (task J8,
spec-species-tree.md §3.1 step 3). The stage that judges whether a planner-offered mechanical
favour fits a species — every answer is already inside the quota (the 166x fix), and "none of
these fit" is a legitimate, expected outcome, not a failure.
"""
from __future__ import annotations

import json
import unittest

from seedsmith.adapters.trees.species.generate_favour_fit import resolve_favour_fit
from seedsmith.adapters.trees.species.plan import FavourCell
from seedsmith.adapters.trees.species.prompts import build_favour_fit_brief, build_favour_fit_context
from seedsmith.adapters.trees.species.roster import SpeciesAnchor


def _anchor(species_id: str, reason: str = "a test fixture creature") -> SpeciesAnchor:
    return SpeciesAnchor(
        species_id=species_id, element_primary="fire", aptitude_primary="Might", posture="Force",
        traits=("flurry",), reason=reason, source_path="test.json")


def _stub_call(responses_by_call_order: "list[dict]"):
    log: "list[str]" = []

    def call(system, user, *, config=None, schema=None):
        log.append(user)
        return json.dumps(responses_by_call_order[len(log) - 1])

    return call, log


class BuildFavourFitBriefTests(unittest.TestCase):
    def test_the_brief_names_the_offered_cell_and_every_alternate_by_key(self) -> None:
        offered = FavourCell("Might", "fire", "poison")
        alternates = [FavourCell("Fortitude", "earth", "rally"), FavourCell("Vigor", "ice", "freeze")]
        context = build_favour_fit_context(_anchor("Alpha"), offered, alternates)
        brief = build_favour_fit_brief(context)
        self.assertIn(offered.key(), context["offeredKey"])
        self.assertIn("Fortitude", brief)
        self.assertIn("earth", brief)
        self.assertIn(alternates[0].key(), brief)
        self.assertIn(alternates[1].key(), brief)

    def test_the_brief_asks_the_exact_three_way_question(self) -> None:
        context = build_favour_fit_context(
            _anchor("Alpha"), FavourCell("Might", "fire", "poison"), [])
        brief = build_favour_fit_brief(context)
        self.assertIn("OFFERED", brief)
        self.assertIn("none", brief)


class ResolveFavourFitTests(unittest.TestCase):
    def _one_species(self, species_id="Alpha", offered=None, alternates=None):
        offered = offered or FavourCell("Might", "fire", "poison")
        alternates = alternates if alternates is not None else [
            FavourCell("Fortitude", "earth", "rally"), FavourCell("Vigor", "ice", "freeze")]
        return [(species_id, _anchor(species_id), offered, alternates)]

    def test_a_unanimous_offered_vote_resolves_to_the_offered_cell(self) -> None:
        offered = FavourCell("Might", "fire", "poison")
        call, log = _stub_call([{"choice": "offered"}] * 3)
        fresh, unresolved, results = resolve_favour_fit(
            self._one_species(offered=offered), provenance_base={"pipeline": "species-favour-fit"},
            call=call, workers=1)
        self.assertEqual(3, len(log))
        self.assertEqual({}, unresolved)
        self.assertEqual(offered, fresh["Alpha"]["cell"])
        self.assertEqual("high", fresh["Alpha"]["_provenance"]["voteConfidence"])

    def test_a_unanimous_alternate_vote_resolves_to_that_alternate_cell(self) -> None:
        alt = FavourCell("Fortitude", "earth", "rally")
        call, _ = _stub_call([{"choice": alt.key()}] * 3)
        fresh, unresolved, _ = resolve_favour_fit(
            self._one_species(alternates=[alt, FavourCell("Vigor", "ice", "freeze")]),
            provenance_base={"pipeline": "species-favour-fit"}, call=call, workers=1)
        self.assertEqual({}, unresolved)
        self.assertEqual(alt, fresh["Alpha"]["cell"])

    def test_a_unanimous_none_vote_is_unresolved_never_a_forced_pick(self) -> None:
        # Doc §3.1 step 3's own explicit rule: "none of these fit" is a real, expected outcome.
        call, _ = _stub_call([{"choice": "none"}] * 3)
        fresh, unresolved, _ = resolve_favour_fit(
            self._one_species(), provenance_base={"pipeline": "species-favour-fit"},
            call=call, workers=1)
        self.assertEqual({}, fresh)
        self.assertEqual("none_of_the_offered_favours_fit", unresolved["Alpha"]["reason"])

    def test_a_three_way_split_is_vote_unresolved(self) -> None:
        alt1 = FavourCell("Fortitude", "earth", "rally")
        call, _ = _stub_call([{"choice": "offered"}, {"choice": alt1.key()}, {"choice": "none"}])
        fresh, unresolved, _ = resolve_favour_fit(
            self._one_species(alternates=[alt1, FavourCell("Vigor", "ice", "freeze")]),
            provenance_base={"pipeline": "species-favour-fit"}, call=call, workers=1)
        self.assertEqual({}, fresh)
        self.assertEqual("vote_unresolved", unresolved["Alpha"]["reason"])

    def test_two_species_with_different_alternate_sets_never_cross_contaminate(self) -> None:
        # The real reason this stage cannot share one graph across species: species Beta's own
        # answer key ("beta-alt") would not even be a legal enum value in Alpha's own schema.
        alpha_alt = FavourCell("Fortitude", "earth", "rally")
        beta_alt = FavourCell("Vigor", "ice", "freeze")
        call, log = _stub_call([
            {"choice": "offered"}, {"choice": "offered"}, {"choice": "offered"},
            {"choice": beta_alt.key()}, {"choice": beta_alt.key()}, {"choice": beta_alt.key()},
        ])
        species = [
            ("Alpha", _anchor("Alpha"), FavourCell("Might", "fire", "poison"), [alpha_alt]),
            ("Beta", _anchor("Beta"), FavourCell("Bulwark", "air", "spark"), [beta_alt]),
        ]
        fresh, unresolved, results = resolve_favour_fit(
            species, provenance_base={"pipeline": "species-favour-fit"}, call=call, workers=1)
        self.assertEqual(6, len(log))
        self.assertEqual(FavourCell("Might", "fire", "poison"), fresh["Alpha"]["cell"])
        self.assertEqual(beta_alt, fresh["Beta"]["cell"])


if __name__ == "__main__":
    unittest.main()
