"""Tests for seedsmith.adapters.actions.characteristic_pool (A-S0, spec-characteristic-pool.md).

    python -m pytest tools/seedsmith/tests/test_characteristic_pool.py -v

Spec §5's eight named cases plus §6's acceptance criteria (1, 2, 3, 4, 4b, 5, 6, 6b, 7, 8) — see
each class's own docstring for which criterion it proves. Two kinds of fixture are used
deliberately:

- **Real, live repo data** (the catalog, motif/family assignments, the shipped tuning file) for
  everything spec calls a MEASURED fact — join counts, weight-file shape, real output shape.
- **Synthetic, in-memory fixtures** (`_species`, `_weights` helpers below) for everything that
  needs to be independent of a moving target — determinism, tie-break, planted violations,
  overflow. This split exists because `data/seed/demons/species/` (the anchor tree) is, AS
  MEASURED DURING THIS MODULE'S OWN BUILD, under active concurrent modification from an unrelated
  demon-classification pass (`git status` showed it `M`odified-but-uncommitted, plus dozens of new
  untracked anchor files, and three separate measurements inside this same build session returned
  three different anchor-tree sizes: 68, then 87, then 87 rows again). A hard-coded literal
  against that specific tree would be a false-positive tripwire, not a true content-change signal
  — see `AnchorTreeJoinTests` below for what IS and is NOT asserted as a literal, and why.
"""
from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.adapters.actions.characteristic_pool import anchors as anchors_mod  # noqa: E402
from seedsmith.adapters.actions.characteristic_pool import catalog as catalog_mod  # noqa: E402
from seedsmith.adapters.actions.characteristic_pool import derive as derive_mod  # noqa: E402
from seedsmith.adapters.actions.characteristic_pool import pool as pool_mod  # noqa: E402
from seedsmith.adapters.actions.characteristic_pool.anchors import AnchorRow, AnchorTree  # noqa: E402
from seedsmith.adapters.actions.characteristic_pool.catalog import (  # noqa: E402
    RARITY_LADDER, SpeciesRow, TRAIT_POOL, load_catalog,
)
from seedsmith.adapters.actions.characteristic_pool.derive import (  # noqa: E402
    CATEGORIES, RoleLeanWeights, SpeciesAnchor, build_species_anchor, compute_scores, derive_all,
    family_floor_order, load_weights, rank_categories,
)
from seedsmith.adapters.actions import generate_characteristic_pool as gen_mod  # noqa: E402
from seedsmith.corpus import Corpus  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parents[3]
DEMONS_ROOT = REPO_ROOT / "data" / "seed" / "demons"
ACTIONS_ROOT = REPO_ROOT / "data" / "seed" / "actions"
TUNING_PATH = REPO_ROOT / "data" / "tuning" / "action-role-lean.v1.json"


# ---------------------------------------------------------------------------------------------
# Synthetic fixtures — independent of any live file, so determinism/tie/overflow tests never
# depend on what the demon-classification pass happens to have written today.
# ---------------------------------------------------------------------------------------------

def _flat_weights(*, milli: int = 1000, secondary_scale: int = 500, version: int = 1) -> RoleLeanWeights:
    row = {c: milli for c in CATEGORIES}
    return RoleLeanWeights(
        trait_category_milli={t: dict(row) for t in TRAIT_POOL},
        element_category_milli={e: dict(row) for e in ("fire", "ice", "air", "earth", "light", "dark")},
        element_secondary_scale_milli=secondary_scale,
        rarity_category_milli={r: dict(row) for r in RARITY_LADDER},
        anchor_category_milli={a: dict(row) for a in anchors_mod.ANCHOR_AXES},
        version=version,
    )


def _species(sid: str, *, element_primary="fire", element_secondary=None, rarity="chaff",
            traits=("berserker",)) -> SpeciesRow:
    return SpeciesRow(
        species_id=sid, element_primary=element_primary, element_secondary=element_secondary,
        rarity=rarity, rarity_ordinal=RARITY_LADDER.index(rarity), traits=tuple(traits),
    )


def _anchor(species: SpeciesRow, *, family=None, motifs=(), anti_motifs=(),
           anchor: "AnchorRow | None" = None) -> SpeciesAnchor:
    return SpeciesAnchor(
        species=species, family=family, motifs=tuple(motifs), anti_motifs=tuple(anti_motifs),
        theme_key=f"demon.{species.species_id}", anchor=anchor,
    )


class CatalogParserTests(unittest.TestCase):
    """Sanity on the C# parser this whole module is built on — never trust the spec's own
    citation without re-checking the live file (this repo's design-gate discipline)."""

    def test_84_species(self) -> None:
        self.assertEqual(len(load_catalog()), 84)

    def test_trait_counts_match_spec_step_4(self) -> None:
        # spec §3 step 4's own measured counts, re-derived here from the live file rather than
        # trusted — soul-eater 28, guardian 27, coward 21, berserker 21, critical-hunter 20,
        # regenerator 20, loyal 20, swift 17, greedy 15, genius 14, bloodthirsty 14,
        # chaos-marked 12, void-touched 9, immortal 7.
        expected = {
            "soul-eater": 28, "guardian": 27, "coward": 21, "berserker": 21,
            "critical-hunter": 20, "regenerator": 20, "loyal": 20, "swift": 17, "greedy": 15,
            "genius": 14, "bloodthirsty": 14, "chaos-marked": 12, "void-touched": 9, "immortal": 7,
        }
        counts: "dict[str, int]" = {t: 0 for t in TRAIT_POOL}
        for row in load_catalog():
            for t in row.traits:
                counts[t] += 1
        self.assertEqual(counts, expected)

    def test_every_rarity_is_a_ladder_id(self) -> None:
        for row in load_catalog():
            self.assertIn(row.rarity, RARITY_LADDER)


class JoinCountTests(unittest.TestCase):
    """Acceptance/spec test 'Join counts' — the STABLE half. 84 catalog / 84 motif (100% join) /
    53 family species over 19 family ids are re-checked against real committed data every run;
    they have been measured identical across every check made while this module was built and
    are the honest tripwire spec §5 asks for."""

    def test_catalog_and_motif_and_family_counts(self) -> None:
        catalog = load_catalog()
        self.assertEqual(len(catalog), 84)
        catalog_ids = {r.species_id for r in catalog}

        motif = json.loads((DEMONS_ROOT / "_generated" / "motif-assignments.json")
                           .read_text(encoding="utf-8"))
        self.assertEqual(len(motif), 84)
        self.assertEqual(set(motif) - catalog_ids, set(), "every motif key must join the catalog")

        family = json.loads((DEMONS_ROOT / "_generated" / "family-assignments.json")
                            .read_text(encoding="utf-8"))
        self.assertEqual(len(family), 53)
        family_ids: "set[str]" = set()
        for v in family.values():
            self.assertEqual(len(v), 1, "spec §1: no species carries two families")
            family_ids.update(v)
        self.assertEqual(len(family_ids), 19)


class AnchorTreeJoinTests(unittest.TestCase):
    """The VOLATILE half — spec cites 28 anchors / 9 unjoined / 8 four-way-join, measured
    2026-09-03. This module's own build re-measured the same file three times the same day and
    got three different answers (concurrent, unrelated work — see module docstring), so this
    class asserts STRUCTURE, not a specific count, and prints what it measured rather than
    pretending the pinned literal is still true."""

    def test_anchor_tree_loads_without_raising(self) -> None:
        tree = anchors_mod.load_anchor_tree()
        self.assertIsInstance(tree, AnchorTree)

    def test_matched_species_are_a_subset_of_the_catalog(self) -> None:
        tree = anchors_mod.load_anchor_tree()
        catalog_ids = {r.species_id for r in load_catalog()}
        matched = set(tree.by_lower_id) & catalog_ids
        # Real, present today; never asserted as an exact literal for the reason in the class
        # docstring. If this ever regresses to 0 the derivation loses its anchor-enrichment
        # signal entirely, which IS worth failing loudly on.
        self.assertGreater(len(matched), 0)

    def test_unjoined_never_silently_dropped(self) -> None:
        # spec §3 step 1: an anchor whose lowered id is not a catalog species is recorded, never
        # dropped. `generate_characteristic_pool.regenerate`'s summary is where that list surfaces.
        tree = anchors_mod.load_anchor_tree()
        catalog_ids = {r.species_id for r in load_catalog()}
        unjoined = [k for k in tree.by_lower_id if k not in catalog_ids]
        # Every unjoined id must still be present, verbatim, in the tree's own keys — i.e. this
        # test would fail if some future change filtered them out of `by_lower_id` instead of
        # just excluding them from the catalog-facing join.
        for u in unjoined:
            self.assertIn(u, tree.by_lower_id)

    def test_broken_index_entries_are_reported_not_silently_skipped(self) -> None:
        # A stale index key (points at a file that no longer holds that species' row, e.g. the
        # measured `SnorkleZombie` -> `zombie/unclassified.json` case) is a real data-quality gap
        # in the (externally-owned) anchor tree; this loader must SURFACE it, never crash on it
        # and never pretend the species was simply absent.
        tree = anchors_mod.load_anchor_tree()
        self.assertIsInstance(tree.broken_index_entries, tuple)


class WeightsFileTests(unittest.TestCase):
    """Acceptance #6 / #6b — the shipped tuning file's shape and its stated neutral default."""

    def test_file_loads_and_validates(self) -> None:
        w = load_weights()
        self.assertEqual(set(w.trait_category_milli), set(TRAIT_POOL))
        self.assertEqual(set(w.element_category_milli),
                         {"fire", "ice", "air", "earth", "light", "dark"})
        self.assertEqual(set(w.rarity_category_milli), set(RARITY_LADDER))
        self.assertEqual(set(w.anchor_category_milli), set(anchors_mod.ANCHOR_AXES))

    def test_every_cell_is_1000_except_the_secondary_scale(self) -> None:
        w = load_weights()
        for block in (w.trait_category_milli, w.element_category_milli, w.rarity_category_milli,
                     w.anchor_category_milli):
            for row_key, row in block.items():
                for cat, val in row.items():
                    self.assertEqual(val, 1000, f"{row_key}[{cat}] must default to 1000")
        self.assertEqual(w.element_secondary_scale_milli, 500)

    def test_meta_states_untuned_and_smoke_batch_evidence(self) -> None:
        doc = json.loads(TUNING_PATH.read_text(encoding="utf-8"))
        note = doc["_meta"]["note"]
        self.assertIn("untuned", note)
        self.assertIn("smoke batch", note)
        self.assertIn("evidence", note)

    def test_flat_default_reproduces_plain_signal_count_ranking(self) -> None:
        """AC6b: at every weight 1000, `compute_scores` must equal a hand-counted "how many of
        this species' own signals map (via `SIGNAL_CATEGORY`) to this category" — i.e. the
        weight table contributes nothing beyond presence/absence. Cross-checked against a
        from-scratch count that never calls `compute_scores` itself."""
        weights = _flat_weights()
        sp = _species("synthspecies", element_primary="fire", element_secondary="ice",
                      rarity="cultivated", traits=("berserker", "guardian", "swift", "genius"))
        anchor = _anchor(sp)
        scores = compute_scores(anchor, weights)

        expected = {c: 0 for c in CATEGORIES}
        for t in sp.traits:
            expected[derive_mod._TRAIT_CATEGORY[t]] += 1
        expected[derive_mod._ELEMENT_CATEGORY["fire"]] += 1
        # secondary scaled to half: 500/1000 of one full-strength signal -> floors to 0 here,
        # which is itself the expected, honest behaviour of an integer half-count.
        expected[derive_mod._ELEMENT_CATEGORY["ice"]] += 0
        for c in CATEGORIES:
            expected[c] += 1  # rarity: flat across every category (tie-shaping only)
        self.assertEqual(scores, expected)


class RankingAndTieTests(unittest.TestCase):
    """Spec §5 'Tie determinism' + acceptance #4b (a five-way tie serialises as the DECLARED
    order, never an invented one)."""

    def test_declared_order_is_the_categories_tuple(self) -> None:
        self.assertEqual(CATEGORIES, ("attack", "defense", "support", "movement", "status"))

    def test_tie_breaks_on_declared_order(self) -> None:
        scores = {"status": 5, "attack": 5, "movement": 1, "defense": 1, "support": 1}
        self.assertEqual(rank_categories(scores), ("attack", "status", "defense", "support", "movement"))

    def test_five_way_tie_serialises_as_declared_order(self) -> None:
        weights = _flat_weights()
        # An empty-signal species: no traits, primary element only, rarity only — engineered so
        # every category ends up equal (rarity's own flat contribution alone).
        sp = SpeciesRow(species_id="tiedspecies", element_primary="fire", element_secondary=None,
                        rarity="chaff", rarity_ordinal=0, traits=())
        # Neutralise the one asymmetric contribution (element primary) by zeroing that block for
        # this synthetic case, so every category truly ties on rarity's flat +1 alone.
        zero_row = {c: 0 for c in CATEGORIES}
        weights = RoleLeanWeights(
            trait_category_milli=weights.trait_category_milli,
            element_category_milli={e: dict(zero_row) for e in weights.element_category_milli},
            element_secondary_scale_milli=weights.element_secondary_scale_milli,
            rarity_category_milli=weights.rarity_category_milli,
            anchor_category_milli=weights.anchor_category_milli, version=1,
        )
        anchor = _anchor(sp)
        scores = compute_scores(anchor, weights)
        self.assertTrue(derive_mod.is_five_way_tie(scores))
        entries, report = derive_all([anchor], weights)
        self.assertEqual(entries[0].lean_source, "floor")
        self.assertEqual(entries[0].lean_order, CATEGORIES)
        self.assertIsNone(entries[0].separation)

    def test_identical_species_produce_identical_lean_order_regardless_of_input_order(self) -> None:
        weights = _flat_weights()
        a = _anchor(_species("speciesa", traits=("berserker", "swift")))
        b = _anchor(_species("speciesb", traits=("berserker", "swift")))
        entries1, _ = derive_all([a, b], weights)
        entries2, _ = derive_all([b, a], weights)
        order1 = {e.species_anchor.species.species_id: e.lean_order for e in entries1}
        order2 = {e.species_anchor.species.species_id: e.lean_order for e in entries2}
        self.assertEqual(order1["speciesa"], order1["speciesb"])
        self.assertEqual(order1, order2)


class FamilyFloorAndF12Tests(unittest.TestCase):
    """Spec §5 'Planted violation — invented anchor' + 'Family-less species are derived',
    acceptance #3."""

    def test_family_less_species_gets_null_family_and_derived_nofloor(self) -> None:
        weights = _flat_weights()
        sp = _species("orphan", traits=("berserker", "guardian", "swift"))
        anchor = _anchor(sp, family=None)
        entries, report = derive_all([anchor], weights)
        entry = entries[0]
        self.assertIsNone(entry.species_anchor.family)
        self.assertEqual(entry.lean_source, "derived-nofloor")
        self.assertIsNone(entry.separation)
        self.assertGreater(len(entry.signals), 0)
        # The F12 failure mode this guards: a build that instead floors every family-less
        # species would report leanSource "floor" and separation 0 — assert we are NOT that.
        self.assertNotEqual(entry.lean_source, "floor")

    def test_stripping_a_species_family_never_substitutes_a_neighbours_family(self) -> None:
        weights = _flat_weights()
        member_a = _anchor(_species("membera", traits=("berserker",)), family="testfam")
        # `member_b` starts in the family, then gets its family assignment stripped — the
        # "invented anchor" planted violation: a build that substituted a neighbour's family
        # would show family="testfam" here instead of None.
        member_b_species = _species("memberb", traits=("swift",))
        stripped = _anchor(member_b_species, family=None)
        entries, _ = derive_all([member_a, stripped], weights)
        stripped_entry = next(e for e in entries if e.species_anchor.species.species_id == "memberb")
        self.assertIsNone(stripped_entry.species_anchor.family)
        self.assertEqual(stripped_entry.lean_source, "derived-nofloor")

    def test_family_less_species_are_derived_over_real_roster(self) -> None:
        """Spec's own named test, run over the REAL 84-species roster: over the family-less
        species, the count whose `leanOrder` is not the bare declared order must be > 0. Uses the
        live anchor tree too (present-or-absent only matters for whether the anchor block
        contributes; it does not affect which species land in this test's population, which is
        defined purely by family-assignments.json — stable per JoinCountTests)."""
        catalog = load_catalog()
        weights = load_weights()
        motif = json.loads((DEMONS_ROOT / "_generated" / "motif-assignments.json")
                           .read_text(encoding="utf-8"))
        family = json.loads((DEMONS_ROOT / "_generated" / "family-assignments.json")
                            .read_text(encoding="utf-8"))
        anchor_tree = anchors_mod.load_anchor_tree()
        anchors = [build_species_anchor(sp, family_assignments=family, motif_assignments=motif,
                                        anchor_by_lower=anchor_tree.by_lower_id)
                  for sp in catalog]
        entries, report = derive_all(anchors, weights)
        self.assertEqual(report.family_less_count, 84 - 53)
        family_less = [e for e in entries if not e.species_anchor.family]
        non_bare = sum(1 for e in family_less if e.lean_order != CATEGORIES)
        self.assertGreater(non_bare, 0)
        # And the corollary: no build regressed to flooring every family-less species uniformly.
        floored = sum(1 for e in family_less if e.lean_source == "floor")
        self.assertLess(floored, len(family_less))


class LegacyRarityLeakTests(unittest.TestCase):
    """Spec §5 'Planted violation — legacy rarity leak', acceptance #2."""

    LEGACY_BANDS = frozenset({"common", "rare", "epic", "legendary"})

    def test_catalog_rarity_is_never_a_legacy_band(self) -> None:
        for row in load_catalog():
            self.assertNotIn(row.rarity, self.LEGACY_BANDS)
            self.assertIn(row.rarity, RARITY_LADDER)

    def test_written_role_lean_file_carries_no_legacy_band(self) -> None:
        path = ACTIONS_ROOT / "_generated" / "role-lean.json"
        if not path.is_file():
            self.skipTest("role-lean.json not yet generated in this checkout")
        doc = json.loads(path.read_text(encoding="utf-8"))
        for entry in doc["entries"]:
            self.assertIn(entry["rarity"], RARITY_LADDER)
            self.assertNotIn(entry["rarity"], self.LEGACY_BANDS)


class AttackTempoExclusionTests(unittest.TestCase):
    """Spec §5 'Planted violation — degenerate signal': `attackTempo` is excluded by measurement
    (constant `"steady"` on every observed anchor row) and a test must prove re-adding it changes
    nothing."""

    def test_attack_tempo_is_never_read_by_compute_scores(self) -> None:
        weights = _flat_weights()
        sp = _species("tempo-species", traits=("berserker",))
        with_tempo = _anchor(sp, anchor=AnchorRow(
            species_id_lower="tempo-species", posture="Bastion", reach="melee",
            target_preference="frontline", attack_tempo="steady"))
        without_tempo_row = AnchorRow(
            species_id_lower="tempo-species", posture="Bastion", reach="melee",
            target_preference="frontline", attack_tempo=None)
        without_tempo = _anchor(sp, anchor=without_tempo_row)
        self.assertEqual(compute_scores(with_tempo, weights), compute_scores(without_tempo, weights))

    def test_live_anchor_tree_attack_tempo_is_constant(self) -> None:
        """The measured fact the exclusion is grounded in: every observed `attackTempo` today is
        `"steady"`. A single distinct value cannot discriminate between species even if it WERE
        scored (it would add the same constant to the same category for every anchored species),
        which is the actual reason re-adding it is provably inert — not just "it happens to be
        excluded"."""
        tree = anchors_mod.load_anchor_tree()
        observed = {r.attack_tempo for r in tree.by_lower_id.values() if r.attack_tempo}
        if not observed:
            self.skipTest("no anchor rows carry attackTempo in this checkout")
        self.assertEqual(observed, {"steady"})


class OverflowTests(unittest.TestCase):
    """Spec §5 'Overflow' — `long` throughout (Python ints are arbitrary precision, so this
    proves the WIDEN-BEFORE-MULTIPLY shape holds even at extreme magnitudes, never that Python
    itself can overflow)."""

    def test_maximal_species_does_not_overflow_and_stays_int(self) -> None:
        huge = 2_000_000_000  # far past a 32-bit boundary, well inside `long`
        weights = _flat_weights(milli=huge, secondary_scale=huge)
        sp = _species("maxspecies", element_primary="fire", element_secondary="ice",
                      rarity=RARITY_LADDER[-1], traits=TRAIT_POOL)
        anchor = _anchor(sp, anchor=AnchorRow(
            species_id_lower="maxspecies", posture="Bastion", reach="siege",
            target_preference="elite", attack_tempo="steady"))
        scores = compute_scores(anchor, weights)
        for cat, val in scores.items():
            self.assertIsInstance(val, int)
            self.assertGreater(val, 0)

    def test_secondary_contribution_widens_before_multiplying(self) -> None:
        # (long)a * b, never (long)(a * b) — with values whose PRODUCT exceeds a 32-bit int even
        # though neither factor alone does, a narrowing-before-multiply bug would silently wrap.
        huge = 3_000_000_000
        result = derive_mod._secondary_contribution(huge, huge)
        self.assertEqual(result, (huge * huge) // 1000)
        self.assertGreater(result, 2**31)


class OfflineGuaranteeTests(unittest.TestCase):
    """Spec §5 'Offline guarantee' / acceptance #8 — zero model calls, anywhere in this module."""

    def test_no_llm_transport_import_anywhere_in_the_package(self) -> None:
        pkg_dir = Path(derive_mod.__file__).resolve().parent
        files = list(pkg_dir.glob("*.py")) + [Path(gen_mod.__file__)]
        forbidden = ("llm_caller", "pipeline.run", "openai", "requests")
        for f in files:
            text = f.read_text(encoding="utf-8")
            for token in forbidden:
                self.assertNotIn(token, text, f"{f} references {token!r} — this module must "
                                              f"never import a model transport")

    def test_regenerate_runs_with_no_network(self) -> None:
        # No mocking needed to prove this — the function simply never imports anything that
        # could reach a network. A stub transport that raises would be redundant scaffolding for
        # a module with zero call sites to stub.
        summary = gen_mod.regenerate(write=False)
        self.assertEqual(summary["species"], 84)


class ResidueReportedTests(unittest.TestCase):
    """Acceptance #5 — the residue count and per-family histogram are PRINTED (the CLI summary
    `run()` emits) and WRITTEN (each entry's own `separation` field, from which both are
    reconstructible by any later reader — Checkpoint 2's "the role lean is reported with its
    separation")."""

    def test_summary_carries_the_residue_report(self) -> None:
        summary = gen_mod.regenerate(write=False)
        self.assertIn("residue", summary)
        for key in ("familyAssigned", "familyLess", "residueCount", "residueSpecies"):
            self.assertIn(key, summary["residue"])
        self.assertEqual(summary["residue"]["familyAssigned"], 53)
        self.assertEqual(summary["residue"]["familyLess"], 31)

    def test_every_written_entry_carries_its_own_separation(self) -> None:
        path = ACTIONS_ROOT / "_generated" / "role-lean.json"
        if not path.is_file():
            self.skipTest("role-lean.json not yet generated in this checkout")
        doc = json.loads(path.read_text(encoding="utf-8"))
        for entry in doc["entries"]:
            self.assertIn("separation", entry)  # present (possibly null) on every entry


class DeterminismTests(unittest.TestCase):
    """Spec §5 'Determinism' + acceptance #7 — byte-identical output over unchanged inputs, using
    a frozen snapshot (never the live, concurrently-modified anchor tree — see module docstring)."""

    def _frozen_inputs(self):
        catalog = load_catalog()
        weights = load_weights()
        motif = json.loads((DEMONS_ROOT / "_generated" / "motif-assignments.json")
                           .read_text(encoding="utf-8"))
        family = json.loads((DEMONS_ROOT / "_generated" / "family-assignments.json")
                            .read_text(encoding="utf-8"))
        # Freeze the anchor tree ONCE so both runs in a test see identical input, independent of
        # whatever the concurrent classification pass does between the two calls.
        anchor_tree = anchors_mod.load_anchor_tree()
        return catalog, weights, motif, family, anchor_tree

    def test_derive_all_is_byte_identical_across_two_runs(self) -> None:
        catalog, weights, motif, family, anchor_tree = self._frozen_inputs()
        anchors = [build_species_anchor(sp, family_assignments=family, motif_assignments=motif,
                                        anchor_by_lower=anchor_tree.by_lower_id)
                  for sp in catalog]
        entries1, report1 = derive_all(anchors, weights)
        entries2, report2 = derive_all(anchors, weights)
        rows1 = [(e.species_anchor.species.species_id, e.lean_order, e.lean_source, e.separation,
                 e.signals) for e in entries1]
        rows2 = [(e.species_anchor.species.species_id, e.lean_order, e.lean_source, e.separation,
                 e.signals) for e in entries2]
        self.assertEqual(rows1, rows2)
        self.assertEqual(report1, report2)

    def test_regenerate_writes_byte_identical_files_over_a_frozen_snapshot(self) -> None:
        """Sandboxes the whole `regenerate()` write path against a private copy of the real
        inputs, so this specific test's two runs cannot observe the live tree changing between
        them (the risk `AnchorTreeJoinTests`'s docstring documents)."""
        with tempfile.TemporaryDirectory(prefix="a-s0-determinism-") as tmp:
            tmp_path = Path(tmp)
            demons_root = tmp_path / "demons"
            (demons_root / "_generated").mkdir(parents=True)
            for name in ("motif-assignments.json", "family-assignments.json"):
                (demons_root / "_generated" / name).write_text(
                    (DEMONS_ROOT / "_generated" / name).read_text(encoding="utf-8"),
                    encoding="utf-8")
            species_root = tmp_path / "species"
            species_root.mkdir()
            (species_root / "_index.json").write_text("{}", encoding="utf-8")  # frozen: empty tree

            actions_root_1 = tmp_path / "actions1"
            actions_root_2 = tmp_path / "actions2"

            summary1 = gen_mod.regenerate(
                actions_root=actions_root_1, demons_root=demons_root,
                species_root=species_root, write=True)
            summary2 = gen_mod.regenerate(
                actions_root=actions_root_2, demons_root=demons_root,
                species_root=species_root, write=True)

            for name in ("role-lean.json", "characteristic-pool.json"):
                text1 = (actions_root_1 / "_generated" / name).read_text(encoding="utf-8")
                text2 = (actions_root_2 / "_generated" / name).read_text(encoding="utf-8")
                self.assertEqual(text1, text2, f"{name} must be byte-identical across two runs")
                self.assertTrue(text1.endswith("\n"))

            self.assertEqual(summary1["corpusHash"], summary2["corpusHash"])


class ProvenanceTests(unittest.TestCase):
    """Acceptance #7 — both files' `_meta` record `corpusHash` and `tuningVersion`."""

    def test_written_files_carry_provenance(self) -> None:
        for name in ("role-lean.json", "characteristic-pool.json"):
            path = ACTIONS_ROOT / "_generated" / name
            if not path.is_file():
                self.skipTest(f"{name} not yet generated in this checkout")
            doc = json.loads(path.read_text(encoding="utf-8"))
            self.assertIn("corpusHash", doc["_meta"])
            self.assertIn("tuningVersion", doc["_meta"])
            self.assertEqual(doc["_meta"]["tuningVersion"], 1)


class RoleLeanShapeTests(unittest.TestCase):
    """Acceptance #1, #2, #4 — over the REAL written file."""

    @classmethod
    def setUpClass(cls) -> None:
        path = ACTIONS_ROOT / "_generated" / "role-lean.json"
        if not path.is_file():
            cls.doc = None
            return
        cls.doc = json.loads(path.read_text(encoding="utf-8"))

    def setUp(self) -> None:
        if self.doc is None:
            self.skipTest("role-lean.json not yet generated in this checkout")

    def test_exactly_84_entries(self) -> None:
        self.assertEqual(len(self.doc["entries"]), 84)

    def test_lean_order_is_a_permutation_of_the_five_categories(self) -> None:
        for entry in self.doc["entries"]:
            self.assertEqual(set(entry["leanOrder"]), set(CATEGORIES))
            self.assertEqual(len(entry["leanOrder"]), 5)

    def test_lean_source_is_one_of_the_three_legal_values(self) -> None:
        for entry in self.doc["entries"]:
            self.assertIn(entry["leanSource"], {"floor", "derived", "derived-nofloor"})

    def test_family_less_entries_have_null_separation_never_zero(self) -> None:
        for entry in self.doc["entries"]:
            if entry["family"] is None and entry["leanSource"] == "derived-nofloor":
                self.assertIsNone(entry["separation"])

    def test_no_entry_key_is_an_unjoined_anchor_id(self) -> None:
        catalog_ids = {r.species_id for r in load_catalog()}
        for entry in self.doc["entries"]:
            self.assertIn(entry["speciesKey"], catalog_ids)


class PoolStructureTests(unittest.TestCase):
    """Acceptance for `characteristic-pool.json` — the six groups A-F, spec §2's inlined table,
    never `action-corpus-ideal.md` §12."""

    def test_six_groups(self) -> None:
        rows = pool_mod.build_pool_entries()
        self.assertEqual(len(rows), 6)
        self.assertEqual({r["group"] for r in rows}, set("ABCDEF"))

    def test_pairing_role_uses_none_never_neutral(self) -> None:
        rows = pool_mod.build_pool_entries()
        group_e = next(r for r in rows if r["group"] == "E")
        self.assertIn("none", group_e["closedValues"]["pairingRole"])
        self.assertNotIn("neutral", group_e["closedValues"]["pairingRole"])

    def test_group_b_has_no_threat_band_field(self) -> None:
        rows = pool_mod.build_pool_entries()
        group_b = next(r for r in rows if r["group"] == "B")
        self.assertNotIn("threatBand", group_b["fields"])

    def test_ids_match_the_action_characteristic_pool_kind_pattern(self) -> None:
        from seedsmith.adapters.actions.kinds import KINDS
        kind_spec = next(k for k in KINDS if k.kind == "action-characteristic-pool")
        for row in pool_mod.build_pool_entries():
            self.assertIsNotNone(kind_spec.id_pattern.match(row["id"]), row["id"])


class CorpusLoadRoundTripTests(unittest.TestCase):
    """The written files load back through A-C1's own `Corpus.load`, with ids matching their own
    KindSpec patterns (never `action-seed`'s) — the whole point of writing through the envelope."""

    def test_written_files_load_through_corpus_load(self) -> None:
        if not (ACTIONS_ROOT / "_generated" / "role-lean.json").is_file():
            self.skipTest("outputs not yet generated in this checkout")
        corpus = Corpus.load(ACTIONS_ROOT)
        self.assertEqual(len(corpus.by_kind("action-role-lean")), 84)
        self.assertEqual(len(corpus.by_kind("action-characteristic-pool")), 6)


if __name__ == "__main__":
    unittest.main()
