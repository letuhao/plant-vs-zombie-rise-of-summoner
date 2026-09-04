"""Tests for seedsmith.adapters.actions.distribution_planner (A-S1, spec-distribution-planner.md).

    python -m pytest tools/seedsmith/tests/test_distribution_planner.py -v

Spec §5's named cases plus §6's acceptance criteria (1, 2, 3, 4, 4b, 5, 6, 6b, 6c, 7, 7b x2, 8, 9)
-- see each class's own docstring for which criterion it proves. Same fixture split every prior
module in this session established: real, live repo data for everything spec calls a MEASURED
fact; synthetic, in-memory fixtures for determinism, planted violations, and overflow.

**The pairings.json rewrite named in spec §3 step 6 is explicitly OUT OF SCOPE for this module's
build (see generate_distribution_planner.py's own module docstring for the full scoping
decision).** Every test below that needs a REACHABLE payoff family therefore reads from a
SYNTHETIC pairing fixture built from two real ids in the 98-family namespace
(`atom.freezing`/`atom.venomous`, both `g-affliction.json`) -- never from the real, unmodified
`data/seed/actions/pairings.json`, whose two shipped keys exist in none of the three namespaces
measured by the spec (§2) and so can never be reachable. This mirrors the spec's own testing-
strategy row, adapted: it says the planted-payoff test reads "from the rewritten pairings.json at
test time" -- that file does not exist in this checkout, so this suite reads from its own
in-memory fixture instead, and says so here rather than silently reinterpreting the spec.
"""
from __future__ import annotations

import ast
import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.adapters.actions.characteristic_pool.derive import CATEGORIES  # noqa: E402
from seedsmith.adapters.actions.type_weights.tuning import AREA_SHAPES, TARGET_MODES  # noqa: E402
from seedsmith.adapters.actions.vocab import (  # noqa: E402
    PAIRING_ROLES, RELATIONS, load_family_ids, load_pairing_keys,
)
from seedsmith.adapters.actions.distribution_planner import derive as dp  # noqa: E402
from seedsmith.adapters.actions.distribution_planner import fingerprint as fp  # noqa: E402
from seedsmith.adapters.actions.distribution_planner.tuning import (  # noqa: E402
    DEDUP_TUNING_PATH, DEFAULT_AVOID_NEIGHBOUR_K, FINGERPRINT_COMPONENT_COUNT, RUN_TUNING_PATH,
    load_dedup_k, load_run_tuning,
)
from seedsmith.adapters.actions import generate_distribution_planner as gen_mod  # noqa: E402
from seedsmith.adapters.actions.kinds import KINDS  # noqa: E402
from seedsmith.adapters.actions.load import load_committed  # noqa: E402
from seedsmith.corpus import Corpus  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parents[3]
ACTIONS_ROOT = REPO_ROOT / "data" / "seed" / "actions"
OUTPUT_PATH = ACTIONS_ROOT / "_briefs" / "round-1.json"

FAMILY_IDS = load_family_ids()                                  # the 98, read fresh (live tree)
FIXTURE_ATOM_ID = "atom.fx-passive-atk-flat"                     # data/seed/atoms/fx-core.json (17)


# ---------------------------------------------------------------------------------------------
# Synthetic fixtures
# ---------------------------------------------------------------------------------------------

def _weights(*, category_milli=None, target_mode_milli=None, area_shape_milli=None) -> dp.WeightsRow:
    return dp.WeightsRow(
        category_milli=dict(category_milli or {c: 200 for c in CATEGORIES}),
        target_mode_milli=dict(target_mode_milli or {m: (1000 // len(TARGET_MODES)
                                                          + (1 if i < 1000 % len(TARGET_MODES) else 0))
                                                    for i, m in enumerate(TARGET_MODES)}),
        area_shape_milli=dict(area_shape_milli or {s: 250 for s in AREA_SHAPES}),
    )


# Two REAL ids in the 98-family namespace, used to build a synthetic, reachable pairing table --
# never the real (unreachable) pairings.json. Both from g-affliction.json.
FAKE_PAYOFF = "atom.freezing"
FAKE_ENABLER = "atom.venomous"
assert FAKE_PAYOFF in FAMILY_IDS and FAKE_ENABLER in FAMILY_IDS


class RunTuningLoadTests(unittest.TestCase):
    """Acceptance #6c: the shipped run-tuning file carries the EXACT stated defaults, and its
    `_meta` states the counts are untuned smoke-batch placeholders."""

    def test_shipped_defaults(self) -> None:
        t = load_run_tuning()
        self.assertEqual(t.mode, "smoke")
        self.assertEqual(t.general_count, 15)
        self.assertEqual(t.per_family_count, 2)
        self.assertEqual(t.per_species_count, 2)
        self.assertEqual(t.multiplicative_pairs, (("atom.keen-edge", "atom.cruelty"),))
        self.assertEqual(t.family_motif_max, 6)
        self.assertEqual(t.version, 2)

    def test_meta_states_untuned(self) -> None:
        doc = json.loads(RUN_TUNING_PATH.read_text(encoding="utf-8"))
        self.assertEqual(doc["_meta"]["default"], "expanded-real-smoke-batch")
        self.assertEqual(doc["_meta"]["smokeSubjects"], "four-way-join-8")
        self.assertIn("placeholder", doc["_meta"]["note"])
        self.assertIn("smoke batch", doc["_meta"]["note"])

    def test_stale_citation_avoidNeighbourK_is_shipped_but_unused(self) -> None:
        """**A found spec self-contradiction, documented rather than silently resolved either
        way** -- matching every prior module this session (each found at least one stale
        citation). Spec §3 step 1's own JSONC literal for `action-corpus-run.v1.json` includes
        `"avoidNeighbourK": 3`, and the task instructions require shipping that EXACT block --
        so the field is present in the shipped file. But spec §3 step 8's own later "DECIDED"
        correction states `k` is read from `action-dedup.v1.json` instead, default 8, and never
        from this file. This module follows step 8 (the later, explicit correction) for the
        actual algorithm and ships step 1's literal block verbatim -- so `avoidNeighbourK` in the
        shipped file is real JSON but genuinely UNREAD by this module's own code."""
        doc = json.loads(RUN_TUNING_PATH.read_text(encoding="utf-8"))
        self.assertEqual(doc["avoidNeighbourK"], 3)
        pkg_dir = Path(dp.__file__).resolve().parent
        for f in list(pkg_dir.glob("*.py")) + [Path(gen_mod.__file__)]:
            self.assertNotIn("avoidNeighbourK", f.read_text(encoding="utf-8"),
                             f"{f} must never read the stale avoidNeighbourK field")


class RunTuningPlantedViolationTests(unittest.TestCase):
    """Spec §5's four magnitude-smuggling shapes, applied to the RUN TUNING loader itself."""

    def _write(self, tmp: Path, **overrides) -> Path:
        doc = json.loads(RUN_TUNING_PATH.read_text(encoding="utf-8"))
        doc.update(overrides)
        path = tmp / "action-corpus-run.v1.json"
        path.write_text(json.dumps(doc), encoding="utf-8")
        return path

    def test_float_refused(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            path = self._write(Path(tmp), generalCount=5.5)
            with self.assertRaises(ValueError) as ctx:
                load_run_tuning(path)
            self.assertIn("generalCount", str(ctx.exception))

    def test_numeric_string_refused(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            path = self._write(Path(tmp), perSpeciesCount="1")
            with self.assertRaises(ValueError) as ctx:
                load_run_tuning(path)
            self.assertIn("perSpeciesCount", str(ctx.exception))

    def test_bool_masquerading_as_int_refused(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            path = self._write(Path(tmp), perFamilyCount=True)
            with self.assertRaises(ValueError) as ctx:
                load_run_tuning(path)
            self.assertIn("perFamilyCount", str(ctx.exception))

    def test_bad_mode_refused(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            path = self._write(Path(tmp), mode="turbo")
            with self.assertRaises(ValueError):
                load_run_tuning(path)


class DedupKTests(unittest.TestCase):
    """Acceptance #7b: `k` is read from `action-dedup.v1.json` (never from
    `action-corpus-run.v1.json`), and defaults to the fingerprint's own component count plus one
    wherever that file is absent.

    **UPDATED 2026-09-04 (A-S3, spec-dedup-select.md): `action-dedup.v1.json` now exists for
    real** -- A-S3 is the file's actual owner and shipped it with `k: 8`, the same value this
    loader's own default already produced. The tripwire this class used to carry
    ("`action-dedup.v1.json` exists now -- re-check the A-S1 default-k call") has fired, exactly as
    it was written to: it is replaced below with a test of the NEW state (`source == "file"`)
    rather than left asserting an absence that is no longer true. `test_reads_k_from_file_when_present`
    is kept unchanged -- it never depended on the shipped file being absent, only on the loader
    correctly reading a file when one exists, which is still exactly what it tests."""

    def test_dedup_file_now_exists_and_matches_the_default(self) -> None:
        self.assertTrue(DEDUP_TUNING_PATH.is_file(),
                        "action-dedup.v1.json is missing again -- A-S3's own file was removed; "
                        "re-check whether the default-k fallback path needs to come back into play")
        doc = json.loads(DEDUP_TUNING_PATH.read_text(encoding="utf-8"))
        self.assertEqual(doc["k"], DEFAULT_AVOID_NEIGHBOUR_K,
                         "the shipped file's k no longer matches this loader's own derived "
                         "default -- A-S3 has genuinely re-tuned k; that is fine, but this loader's "
                         "own default should be re-derived to match or explicitly diverge on purpose")

    def test_default_k_equals_component_count_plus_one(self) -> None:
        self.assertEqual(FINGERPRINT_COMPONENT_COUNT, 7)
        self.assertEqual(DEFAULT_AVOID_NEIGHBOUR_K, 8)

    def test_k_is_now_sourced_from_the_real_file_not_the_fallback_default(self) -> None:
        k, source = load_dedup_k()
        self.assertEqual(k, 8)
        self.assertEqual(source, "file",
                         "k is still coming from the fallback default -- action-dedup.v1.json "
                         "should be readable now that A-S3 has shipped it")

    def test_reads_k_from_file_when_present(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "action-dedup.v1.json"
            path.write_text(json.dumps({"k": 12}), encoding="utf-8")
            k, source = load_dedup_k(path)
            self.assertEqual(k, 12)
            self.assertEqual(source, "file")

    def test_default_still_used_when_a_DIFFERENT_missing_path_is_given(self) -> None:
        """The fallback path (§ tuning.py's own documented default) is still real code, still
        reachable, and still correct -- it is simply no longer what the SHIPPED path exercises."""
        with tempfile.TemporaryDirectory() as tmp:
            missing = Path(tmp) / "does-not-exist.json"
            k, source = load_dedup_k(missing)
            self.assertEqual(k, DEFAULT_AVOID_NEIGHBOUR_K)
            self.assertEqual(source, "default")


class FamilyMotifDerivationTests(unittest.TestCase):
    """Spec §3 step 2b, acceptance #7b (the first one, family motifs)."""

    def test_intersection_wins_when_nonempty(self) -> None:
        rows = [(("a", "b", "c"), ("x",)), (("a", "b"), ("y",))]
        motifs, anti, basis = dp.derive_family_motifs(rows, family_motif_max=6)
        self.assertEqual(motifs, ("a", "b"))
        self.assertEqual(anti, ("x", "y"))
        self.assertEqual(basis, "intersection")

    def test_majority_fallback_when_intersection_empty(self) -> None:
        # 3 members; "a" held by 2 of 3 and "c" held by 2 of 3 (both >= ceil(3/2)=2); "b" held by
        # only 1 -> majority = {"a", "c"}, sorted byte-wise.
        rows = [(("a", "b"), ()), (("a", "c"), ()), (("c",), ())]
        motifs, _, basis = dp.derive_family_motifs(rows, family_motif_max=6)
        self.assertEqual(basis, "majority")
        self.assertEqual(motifs, ("a", "c"))

    def test_frequency_fallback_capped_by_family_motif_max(self) -> None:
        # 4 members, no motif shared by >=2 (no majority), everything unique once -> frequency,
        # capped at family_motif_max, ties broken byte-wise.
        rows = [(("d",), ()), (("c",), ()), (("b",), ()), (("a",), ())]
        motifs, _, basis = dp.derive_family_motifs(rows, family_motif_max=2)
        self.assertEqual(basis, "frequency")
        self.assertEqual(motifs, ("a", "b"))          # byte-wise sorted, capped to 2

    def test_empty_members_is_total_not_a_crash(self) -> None:
        motifs, anti, basis = dp.derive_family_motifs([], family_motif_max=6)
        self.assertEqual(motifs, ())
        self.assertEqual(anti, ())
        self.assertEqual(basis, "intersection")

    def test_all_nineteen_families_intersect_nonempty_against_real_data(self) -> None:
        fam_path = REPO_ROOT / "data" / "seed" / "demons" / "_generated" / "family-assignments.json"
        lean_path = ACTIONS_ROOT / "_generated" / "role-lean.json"
        if not fam_path.is_file() or not lean_path.is_file():
            self.skipTest("A-S0 outputs not yet generated in this checkout")
        family_assignments = json.loads(fam_path.read_text(encoding="utf-8"))
        species_anchor = dp.parse_species_anchor(json.loads(lean_path.read_text(encoding="utf-8")))
        members = gen_mod._family_members(family_assignments)
        self.assertEqual(len(members), 19)

        cherry_seen = False
        for family_id, species_ids in members.items():
            rows = [(species_anchor[s].motifs, species_anchor[s].anti_motifs)
                   for s in species_ids if s in species_anchor]
            motifs, anti, basis = dp.derive_family_motifs(rows, family_motif_max=6)
            self.assertEqual(basis, "intersection", f"family {family_id!r} needed a fallback")
            self.assertGreater(len(motifs), 0)
            self.assertEqual(len(anti), 5, f"family {family_id!r} anti-motif union != 5")
            if family_id == "cherry":
                cherry_seen = True
                self.assertEqual(motifs, ("僵尸", "樱桃"))
                self.assertEqual(len(species_ids), 7)
        self.assertTrue(cherry_seen, "cherry family not found in the real corpus")

    def test_family_size_histogram_matches_spec_measurement(self) -> None:
        fam_path = REPO_ROOT / "data" / "seed" / "demons" / "_generated" / "family-assignments.json"
        if not fam_path.is_file():
            self.skipTest("family-assignments.json not present in this checkout")
        family_assignments = json.loads(fam_path.read_text(encoding="utf-8"))
        members = gen_mod._family_members(family_assignments)
        sizes = {}
        for v in members.values():
            sizes[len(v)] = sizes.get(len(v), 0) + 1
        self.assertEqual(sizes, {7: 1, 5: 2, 4: 1, 3: 3, 2: 11, 1: 1})
        self.assertEqual(sum(len(v) for v in members.values()), 53)
        self.assertEqual(len(members), 19)


class LargestRemainderAndExpandTests(unittest.TestCase):
    """Spec §3 steps 3/4a, acceptance #3/#4b."""

    def test_distributes_to_largest_fractions_with_declared_order_tiebreak(self) -> None:
        weights = {"attack": 334, "defense": 333, "support": 333, "movement": 0, "status": 0}
        counts = dp.largest_remainder_count(weights, CATEGORIES, 3)
        self.assertEqual(sum(counts.values()), 3)
        self.assertEqual(counts, {"attack": 1, "defense": 1, "support": 1, "movement": 0, "status": 0})

    def test_zero_total_yields_all_zero(self) -> None:
        weights = {c: 200 for c in CATEGORIES}
        counts = dp.largest_remainder_count(weights, CATEGORIES, 0)
        self.assertEqual(set(counts.values()), {0})

    def test_expand_counts_is_declared_order_grouped(self) -> None:
        counts = {"attack": 2, "defense": 0, "support": 1, "movement": 0, "status": 1}
        seq = dp.expand_counts(counts, CATEGORIES)
        self.assertEqual(seq, ["attack", "attack", "support", "status"])

    def test_independent_of_dict_insertion_order(self) -> None:
        a = {"attack": 7, "defense": 3, "support": 5, "movement": 2, "status": 1}
        b = {"status": 1, "movement": 2, "support": 5, "defense": 3, "attack": 7}
        self.assertEqual(dp.largest_remainder_count(a, CATEGORIES, 18),
                         dp.largest_remainder_count(b, CATEGORIES, 18))


class OverflowTests(unittest.TestCase):
    """Spec §5 'Overflow' -- `long` throughout, widened before multiplying, forced overflow throws."""

    def test_widen_before_multiply_overflow_throws(self) -> None:
        with self.assertRaises(OverflowError):
            dp._widen_mul(10_000_000_000_000_000_000, 1000)

    def test_large_but_legal_vector_does_not_overflow(self) -> None:
        weights = {c: 200 for c in CATEGORIES}
        counts = dp.largest_remainder_count(weights, CATEGORIES, 1_000_000_000)
        self.assertEqual(sum(counts.values()), 1_000_000_000)
        for v in counts.values():
            self.assertIsInstance(v, int)


class RungWindowAndStructureAxesTests(unittest.TestCase):
    """Spec §3 steps 4/5, acceptance #4/#7. Literals asserted directly: general 2, family 5,
    signature 6, and the `Rung = rungBand[1]` collapse rule."""

    RUNGS_PATH = REPO_ROOT / "data" / "tuning" / "action-rungs.v1.json"

    def setUp(self) -> None:
        self.rung_table = dp.load_rung_table(self.RUNGS_PATH)

    def test_rung_windows(self) -> None:
        self.assertEqual(dp.RUN_WINDOW, {"general": (1, 4), "family": (1, 7), "species": (1, 10)})

    def test_assignable_axis_counts_are_2_5_6(self) -> None:
        general = dp.structure_axes_for("general", self.rung_table)
        family = dp.structure_axes_for("family", self.rung_table)
        species = dp.structure_axes_for("species", self.rung_table)
        self.assertEqual(len(general), 2)
        self.assertEqual(len(family), 5)
        self.assertEqual(len(species), 6)
        self.assertEqual(set(general), {"scopeSplit", "riderStatus"})
        self.assertEqual(set(family), {"scopeSplit", "riderStatus", "condition", "sequence",
                                       "consumption"})
        self.assertEqual(set(species), {"scopeSplit", "riderStatus", "condition", "sequence",
                                        "consumption", "restriction"})

    def test_reaction_never_appears_species_ceiling_row_has_it_raw(self) -> None:
        # The RAW rung-10 row DOES carry 'reaction' (it is unspendable, not undetectable/absent);
        # structure_axes_for must still subtract it before it ever reaches a brief.
        self.assertIn("reaction", self.rung_table[10])
        self.assertNotIn("reaction", dp.structure_axes_for("species", self.rung_table))

    def test_reaction_named_is_refused_not_flagged(self) -> None:
        with self.assertRaises(ValueError) as ctx:
            dp.validate_structure_axes(("riderStatus", "reaction"))
        self.assertIn("reaction", str(ctx.exception))

    def test_rung_collapse_rule_uses_ceiling(self) -> None:
        # rungBand[1] (the ceiling) is what structure_axes_for actually reads -- proven by
        # comparing against the window's own second element for all three scopes.
        for scope, (floor, ceiling) in dp.RUN_WINDOW.items():
            axes_from_ceiling = dp.structure_axes_for(scope, self.rung_table)
            axes_from_raw_ceiling_row = tuple(a for a in self.rung_table[ceiling] if a != "reaction")
            self.assertEqual(axes_from_ceiling, axes_from_raw_ceiling_row)

    def test_validate_rung_band_accepts_the_real_window_and_refuses_a_raised_floor(self) -> None:
        dp.validate_rung_band("species", [1, 10])
        with self.assertRaises(ValueError) as ctx:
            dp.validate_rung_band("species", [5, 10])
        self.assertIn("spec-rung-semantics.md", str(ctx.exception))


class CategoryRelationTests(unittest.TestCase):
    """The relation map this module authors (a genuine editorial call, flagged in the module
    docstring) -- proven to be closed vocabulary, never a magnitude."""

    def test_every_category_maps_to_real_vocabulary(self) -> None:
        self.assertEqual(set(dp.CATEGORY_RELATION), set(CATEGORIES))
        for relation in dp.CATEGORY_RELATION.values():
            self.assertIn(relation, RELATIONS)


class PoolTests(unittest.TestCase):
    """Spec §3 step 7, acceptance #6/#6b."""

    def test_allowed_is_all_98_and_forbidden_is_the_pair_union(self) -> None:
        allowed, forbidden = dp.build_pool(FAMILY_IDS, (("atom.keen-edge", "atom.cruelty"),))
        self.assertEqual(len(allowed), 98)
        self.assertEqual(set(allowed), FAMILY_IDS)
        self.assertEqual(forbidden, ("atom.cruelty", "atom.keen-edge"))

    def test_same_eligible_set_every_tier_planted_widening_refused(self) -> None:
        allowed, _ = dp.build_pool(FAMILY_IDS, ())
        narrowed = tuple(sorted(FAMILY_IDS))[:10]
        with self.assertRaises(ValueError) as ctx:
            dp.validate_no_family_widening({"general": allowed, "family": allowed,
                                            "species": narrowed})
        self.assertIn("powerBudget", str(ctx.exception))

    def test_atom_family_namespace_accepts_real_and_refuses_fixture(self) -> None:
        dp.validate_atom_family_namespace(["atom.keen-edge", "atom.cruelty"], FAMILY_IDS)
        with self.assertRaises(ValueError) as ctx:
            dp.validate_atom_family_namespace([FIXTURE_ATOM_ID], FAMILY_IDS)
        self.assertIn(FIXTURE_ATOM_ID, str(ctx.exception))

    def test_namespace_count_is_exactly_98(self) -> None:
        self.assertEqual(len(FAMILY_IDS), 98)

    def test_multiplicative_conflict_refused_for_flat_pair(self) -> None:
        with self.assertRaises(ValueError) as ctx:
            dp.validate_no_multiplicative_conflict(
                ("atom.keen-edge", "atom.cruelty", "atom.precision"), (),
                (("atom.keen-edge", "atom.cruelty"),))
        msg = str(ctx.exception)
        self.assertIn("atom.keen-edge", msg)
        self.assertIn("atom.cruelty", msg)

    def test_multiplicative_conflict_refused_for_replace_twins(self) -> None:
        """The Replace twins `atom.prec-verdict`/`atom.prec-reckoning` (g-precision.json) --
        proven generic: the pairs are read from a `multiplicativePairs`-shaped argument, never
        hard-coded to the Flat pair's own ids."""
        self.assertIn("atom.prec-verdict", FAMILY_IDS)
        self.assertIn("atom.prec-reckoning", FAMILY_IDS)
        with self.assertRaises(ValueError) as ctx:
            dp.validate_no_multiplicative_conflict(
                ("atom.prec-verdict", "atom.prec-reckoning"), (),
                (("atom.prec-verdict", "atom.prec-reckoning"),))
        msg = str(ctx.exception)
        self.assertIn("atom.prec-verdict", msg)
        self.assertIn("atom.prec-reckoning", msg)

    def test_no_conflict_when_one_half_is_forbidden(self) -> None:
        dp.validate_no_multiplicative_conflict(
            ("atom.keen-edge", "atom.cruelty"), ("atom.cruelty",),
            (("atom.keen-edge", "atom.cruelty"),))    # must not raise


class PairingRoleTests(unittest.TestCase):
    """Spec §3 step 6, acceptance #5. Uses the SYNTHETIC pairing fixture (module docstring) --
    never the real, unreachable pairings.json."""

    FAKE_TABLE = {FAKE_PAYOFF: (FAKE_ENABLER,)}

    def test_reachable_payoff_pairs_with_the_next_ordinal_as_enabler(self) -> None:
        assignments = dp.assign_pairing_roles(4, FAMILY_IDS, self.FAKE_TABLE)
        self.assertEqual(assignments[0].role, "payoff")
        self.assertEqual(assignments[0].paired_payoff_family, FAKE_PAYOFF)
        self.assertEqual(assignments[1].role, "enabler")
        self.assertEqual(assignments[1].paired_payoff_family, FAKE_PAYOFF)
        self.assertEqual(assignments[1].forced_enabler, FAKE_ENABLER)
        self.assertEqual(assignments[2].role, "none")
        self.assertEqual(assignments[3].role, "none")

    def test_unreachable_universe_yields_all_none(self) -> None:
        assignments = dp.assign_pairing_roles(3, frozenset({"atom.precision"}), self.FAKE_TABLE)
        self.assertTrue(all(a.role == "none" for a in assignments))

    def test_real_pairings_json_is_unreachable_today(self) -> None:
        real_keys = load_pairing_keys()
        self.assertTrue(real_keys.isdisjoint(FAMILY_IDS),
                        "pairings.json now overlaps the 98-family namespace -- the rewrite named "
                        "in spec §3 step 6 may have landed; re-check the scoping decision")

    def test_against_the_real_corpus_every_brief_gets_role_none(self) -> None:
        if not OUTPUT_PATH.is_file():
            self.skipTest("round-1.json not yet generated in this checkout")
        doc = json.loads(OUTPUT_PATH.read_text(encoding="utf-8"))
        roles = {e["pairing"]["role"] for e in doc["entries"]}
        self.assertEqual(roles, {"none"})

    def test_planted_violation_unpaired_payoff_refused(self) -> None:
        group = [
            {"briefId": "brief.species.x.001",
            "pairing": {"role": "payoff", "pairedPayoffFamily": FAKE_PAYOFF}},
            {"briefId": "brief.species.x.002", "pairing": {"role": "none", "pairedPayoffFamily": None}},
        ]
        with self.assertRaises(ValueError) as ctx:
            dp.validate_pairing_coverage(group)
        self.assertIn("brief.species.x.001", str(ctx.exception))

    def test_paired_group_passes(self) -> None:
        group = [
            {"briefId": "brief.species.x.001",
            "pairing": {"role": "payoff", "pairedPayoffFamily": FAKE_PAYOFF}},
            {"briefId": "brief.species.x.002",
            "pairing": {"role": "enabler", "pairedPayoffFamily": FAKE_PAYOFF}},
        ]
        dp.validate_pairing_coverage(group)   # must not raise

    def test_status_id_in_paired_payoff_family_refused(self) -> None:
        with self.assertRaises(ValueError) as ctx:
            dp.validate_pairing_vocabulary("freeze", frozenset({FAKE_PAYOFF}))
        self.assertIn("STATUS", str(ctx.exception))

    def test_unknown_pairing_key_refused(self) -> None:
        with self.assertRaises(ValueError):
            dp.validate_pairing_vocabulary("atom.precision", frozenset({FAKE_PAYOFF}))

    def test_none_is_always_legal(self) -> None:
        dp.validate_pairing_vocabulary(None, frozenset())   # must not raise


class SchemaAuditTests(unittest.TestCase):
    """Spec §5 'Planted violation -- a magnitude in a brief', acceptance #2: all four smuggling
    shapes refused, `slot.rungBand` exempted as the one legal int pair."""

    def _valid_brief(self) -> dict:
        return {
            "briefId": "brief.general.general.001", "scope": "general", "scopeKey": None,
            "anchor": {"family": None, "element": None, "rarity": None, "themeKey": None,
                      "motifs": [], "antiMotifs": []},
            "slot": {"category": "attack", "targetMode": "self", "areaShape": None,
                    "relation": "enemy", "kind": None, "rungBand": [1, 4],
                    "structureAxes": ["scopeSplit", "riderStatus"], "structureEnforced": True},
            "pool": {"allowedAtomFamilies": ["atom.precision"], "forbiddenAtomFamilies": []},
            "pairing": {"role": "none", "pairedPayoffFamily": None},
            "avoidNeighbours": [],
            "_provenance": {"corpusHash": "x", "promptVersion": 1, "round": 1, "tuningVersion": 1},
        }

    def test_valid_brief_passes(self) -> None:
        dp.audit_no_magnitude_smuggling(self._valid_brief())   # must not raise

    def test_rung_band_int_pair_is_legal(self) -> None:
        b = self._valid_brief()
        b["slot"]["rungBand"] = [1, 10]
        dp.audit_no_magnitude_smuggling(b)                     # must not raise

    def test_bare_number_refused(self) -> None:
        b = self._valid_brief()
        b["slot"]["chance"] = 250
        with self.assertRaises(ValueError) as ctx:
            dp.audit_no_magnitude_smuggling(b)
        self.assertIn("chance", str(ctx.exception))

    def test_duration_ms_refused(self) -> None:
        b = self._valid_brief()
        b["slot"]["durationMs"] = 3000
        with self.assertRaises(ValueError):
            dp.audit_no_magnitude_smuggling(b)

    def test_power_milli_refused(self) -> None:
        b = self._valid_brief()
        b["pool"]["powerMilli"] = 500
        with self.assertRaises(ValueError):
            dp.audit_no_magnitude_smuggling(b)

    def test_numeric_string_refused(self) -> None:
        b = self._valid_brief()
        b["slot"]["category"] = "250"
        with self.assertRaises(ValueError) as ctx:
            dp.audit_no_magnitude_smuggling(b)
        self.assertIn("250", str(ctx.exception))

    def test_enum_of_numeric_strings_refused(self) -> None:
        b = self._valid_brief()
        b["slot"]["structureAxes"] = ["100", "200"]
        with self.assertRaises(ValueError):
            dp.audit_no_magnitude_smuggling(b)

    def test_provenance_ints_are_exempt(self) -> None:
        dp.audit_no_magnitude_smuggling(self._valid_brief())   # _provenance carries real ints


class FingerprintTests(unittest.TestCase):
    """Spec §3 step 8, acceptance #7b (the second one)."""

    def _fp(self, **overrides) -> fp.FingerprintComponents:
        base = dict(atom_families=("atom.precision", "atom.keen-edge"), category="attack",
                   target_mode="area", area_shape="row", relation="enemy",
                   structure_axes=("condition", "riderStatus"), pairing_role="enabler")
        base.update(overrides)
        return fp.FingerprintComponents(**base)

    def test_render_joins_list_components_with_plus_and_components_with_pipe(self) -> None:
        s = fp.render_fingerprint_string(self._fp())
        self.assertEqual(s, "atom.keen-edge+atom.precision|attack|area|row|enemy|"
                            "condition+riderStatus|enabler")

    def test_distance_zero_for_identical_fingerprints(self) -> None:
        a = fp.render_fingerprint(self._fp())
        b = fp.render_fingerprint(self._fp())
        self.assertEqual(fp.field_distance(a, b), 0)

    def test_planted_distance_one(self) -> None:
        a = fp.render_fingerprint(self._fp())
        b = fp.render_fingerprint(self._fp(category="defense"))
        self.assertEqual(fp.field_distance(a, b), 1)

    def test_planted_distance_three(self) -> None:
        a = fp.render_fingerprint(self._fp())
        b = fp.render_fingerprint(self._fp(category="defense", target_mode="self",
                                           relation="ally"))
        self.assertEqual(fp.field_distance(a, b), 3)

    def test_list_component_compares_as_rendered_string_not_as_a_set(self) -> None:
        a = fp.render_fingerprint(self._fp(atom_families=("atom.a", "atom.b")))
        b = fp.render_fingerprint(self._fp(atom_families=("atom.a", "atom.c")))
        # a one-member difference inside atomFamilies is distance 1 on THAT field, not a set
        # difference of 2 -- overall distance across the 7 components is still 1.
        self.assertEqual(fp.field_distance(a, b), 1)

    def test_k_nearest_orders_by_distance_then_action_id(self) -> None:
        target = fp.render_fingerprint(self._fp())
        near = fp.render_fingerprint(self._fp(category="defense"))          # distance 1
        far = fp.render_fingerprint(self._fp(category="defense", target_mode="self",
                                              relation="ally"))              # distance 3
        candidates = [("action.species.x.002", far), ("action.species.x.001", near),
                     ("action.species.x.003", near)]
        result = dp.k_nearest(target, candidates, 3) if hasattr(dp, "k_nearest") else \
            fp.k_nearest(target, candidates, 3)
        self.assertEqual(result[0], ("action.species.x.001", 1))
        self.assertEqual(result[1], ("action.species.x.003", 1))
        self.assertEqual(result[2][0], "action.species.x.002")

    def test_k_nearest_is_shuffle_invariant(self) -> None:
        target = fp.render_fingerprint(self._fp())
        candidates = [(f"action.species.x.{i:03d}", fp.render_fingerprint(self._fp(category=c)))
                     for i, c in enumerate(["attack", "defense", "support", "movement", "status"])]
        import random
        shuffled = list(candidates)
        random.Random(7).shuffle(shuffled)
        self.assertEqual(fp.k_nearest(target, candidates, 3), fp.k_nearest(target, shuffled, 3))

    def test_empty_candidates_returns_empty_never_raises(self) -> None:
        target = fp.render_fingerprint(self._fp())
        self.assertEqual(fp.k_nearest(target, [], 8), [])

    def test_round_1_has_no_accepted_corpus_avoid_neighbours_empty(self) -> None:
        if not OUTPUT_PATH.is_file():
            self.skipTest("round-1.json not yet generated in this checkout")
        doc = json.loads(OUTPUT_PATH.read_text(encoding="utf-8"))
        for e in doc["entries"]:
            self.assertEqual(e["avoidNeighbours"], [],
                             f"{e['briefId']}: round 1 must read no accepted corpus")


class CasingRoundTripTests(unittest.TestCase):
    """Spec §5 'Casing', acceptance #4b -- every emitted enum is real wire-string vocabulary,
    never a PascalCase enum member name."""

    def setUp(self) -> None:
        if not OUTPUT_PATH.is_file():
            self.skipTest("round-1.json not yet generated in this checkout")
        self.doc = json.loads(OUTPUT_PATH.read_text(encoding="utf-8"))

    def test_category_target_mode_area_shape_relation_are_real_vocabulary(self) -> None:
        for e in self.doc["entries"]:
            slot = e["slot"]
            self.assertIn(slot["category"], CATEGORIES)
            self.assertIn(slot["targetMode"], TARGET_MODES)
            if slot["areaShape"] is not None:
                self.assertIn(slot["areaShape"], AREA_SHAPES)
            self.assertIn(slot["relation"], RELATIONS)
            self.assertIn(e["pairing"]["role"], PAIRING_ROLES)

    def test_no_pascal_case_leakage(self) -> None:
        pascal = {"Attack", "Defense", "Support", "Movement", "Status", "Self", "Single", "Multi",
                 "RolledTarget", "All", "Area", "Row", "Column", "Square", "Rectangle", "Enemy",
                 "Ally", "Any"}
        for e in self.doc["entries"]:
            slot = e["slot"]
            for value in (slot["category"], slot["targetMode"], slot["areaShape"], slot["relation"]):
                if value is not None:
                    self.assertNotIn(value, pascal)

    def test_brief_id_matches_kind_spec_pattern(self) -> None:
        kind_spec = next(k for k in KINDS if k.kind == "action-brief")
        for e in self.doc["entries"]:
            self.assertIsNotNone(kind_spec.id_pattern.match(e["briefId"]), e["briefId"])
            self.assertEqual(e["id"], e["briefId"])


class CorpusLoadRoundTripTests(unittest.TestCase):
    """The written file loads back through A-C1's own `Corpus.load` -- proves `id` (not only the
    spec's own `briefId`) is present, since that is the field `Corpus.load`/`discover_edges`
    actually key on (`kinds.py`'s `action-brief` KindSpec: `required={"id"}`)."""

    def test_written_file_loads_through_corpus_load_and_discovers_edges(self) -> None:
        """`load_committed`, not a raw `Corpus.load` — see `test_type_weights.py`'s own sibling
        fix (2026-09-04): real `_rounds/` content (a real smoke batch) now legitimately reuses
        `_briefs/round-1.json`'s own ids by design (A-S2's assembled P3 briefs), which only the
        purpose-built `_rounds/`-excluding loader tolerates."""
        if not OUTPUT_PATH.is_file():
            self.skipTest("round-1.json not yet generated in this checkout")
        result = load_committed(ACTIONS_ROOT)
        rows = result.corpus.by_kind("action-brief")
        self.assertEqual(len(rows), 221)
        kind_spec = next(k for k in KINDS if k.kind == "action-brief")
        edges = result.corpus.discover_edges(kind_spec.id_pattern, skip_fields=frozenset({"name"}))
        self.assertEqual(len(edges), 221)


class FullRunRefusalTests(unittest.TestCase):
    """Spec §5 'Full-run refusal', acceptance #8."""

    def test_shipped_default_is_smoke(self) -> None:
        self.assertEqual(load_run_tuning().mode, "smoke")

    def test_full_without_flag_refused(self) -> None:
        with self.assertRaises(ValueError) as ctx:
            dp.refuse_full_run_if_ungated("full", False, False)
        self.assertIn("A-S5", str(ctx.exception))

    def test_full_with_flag_but_no_gate_still_refused(self) -> None:
        with self.assertRaises(ValueError) as ctx:
            dp.refuse_full_run_if_ungated("full", True, False)
        self.assertIn("A-S5", str(ctx.exception))

    def test_smoke_never_refused(self) -> None:
        dp.refuse_full_run_if_ungated("smoke", False, False)     # must not raise

    def test_full_with_flag_and_gate_passes(self) -> None:
        dp.refuse_full_run_if_ungated("full", True, True)        # must not raise (hypothetical)

    def test_gate_evidence_absent_in_this_checkout(self) -> None:
        self.assertFalse(gen_mod.SMOKE_GATE_EVIDENCE_PATH.is_file(),
                         "A-S5's coverage report now exists -- a full run may be reachable; "
                         "re-check this refusal's operational meaning")


class DryRunAndOfflineTests(unittest.TestCase):
    """Spec §5 '--dry-run', acceptance #8 -- zero writes, zero model calls."""

    def test_dry_run_computes_but_writes_nothing(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            tmp_path = Path(tmp)
            summary = gen_mod.regenerate(actions_root=tmp_path / "actions",
                                        demons_root=REPO_ROOT / "data" / "seed" / "demons",
                                        write=False)
            self.assertFalse((tmp_path / "actions" / "_briefs" / "round-1.json").exists())
            self.assertFalse(summary["written"])

    def test_no_model_transport_import_anywhere_in_the_package(self) -> None:
        pkg_dir = Path(dp.__file__).resolve().parent
        files = list(pkg_dir.glob("*.py")) + [Path(gen_mod.__file__)]
        forbidden = ("llm_caller", "pipeline.run", "openai", "requests")
        for f in files:
            text = f.read_text(encoding="utf-8")
            for token in forbidden:
                self.assertNotIn(token, text, f"{f} references {token!r}")


class RosterSizeTests(unittest.TestCase):
    """Spec §5 'Roster', acceptance #1 -- 84 species, 19 families, 53 family-assigned species."""

    @classmethod
    def setUpClass(cls) -> None:
        if not OUTPUT_PATH.is_file():
            cls.doc = None
            return
        cls.doc = json.loads(OUTPUT_PATH.read_text(encoding="utf-8"))

    def setUp(self) -> None:
        if self.doc is None:
            self.skipTest("round-1.json not yet generated in this checkout")

    def test_species_and_family_subject_counts(self) -> None:
        species_briefs = [e for e in self.doc["entries"] if e["scope"] == "species"]
        family_briefs = [e for e in self.doc["entries"] if e["scope"] == "family"]
        general_briefs = [e for e in self.doc["entries"] if e["scope"] == "general"]
        self.assertEqual(len({e["scopeKey"] for e in species_briefs}), 84)
        self.assertEqual(len({e["scopeKey"] for e in family_briefs}), 19)
        self.assertEqual(len(general_briefs), 15)
        self.assertNotEqual(len({e["scopeKey"] for e in species_briefs}), 904)

    def test_family_assigned_species_count_is_53(self) -> None:
        fam_path = REPO_ROOT / "data" / "seed" / "demons" / "_generated" / "family-assignments.json"
        family_assignments = json.loads(fam_path.read_text(encoding="utf-8"))
        members = gen_mod._family_members(family_assignments)
        self.assertEqual(sum(len(v) for v in members.values()), 53)


class QuotaExactnessTests(unittest.TestCase):
    """Acceptance #3 -- per-subject category counts equal the largest-remainder allocation of
    A-T1's real weights, spot-checked directly against the shipped type-weights.json."""

    def setUp(self) -> None:
        tw_path = ACTIONS_ROOT / "type-weights.json"
        if not tw_path.is_file() or not OUTPUT_PATH.is_file():
            self.skipTest("type-weights.json / round-1.json not yet generated in this checkout")
        self.tw_by_key = {(e["scope"], e["scopeKey"]): e
                         for e in json.loads(tw_path.read_text(encoding="utf-8"))["entries"]}
        self.round_doc = json.loads(OUTPUT_PATH.read_text(encoding="utf-8"))

    def test_species_category_counts_match_largest_remainder_exactly(self) -> None:
        for scope_key in ("cherrybomb", "peashooter"):
            row = self.tw_by_key.get(("species", scope_key))
            if row is None:
                continue
            briefs = [e for e in self.round_doc["entries"]
                     if e["scope"] == "species" and e["scopeKey"] == scope_key]
            self.assertEqual(len(briefs), 2)          # perSpeciesCount == 2 at the shipped default
            expected = dp.largest_remainder_count(row["categoryMilli"], CATEGORIES, 2)
            actual = {c: 0 for c in CATEGORIES}
            for b in briefs:
                actual[b["slot"]["category"]] += 1
            self.assertEqual(actual, expected)


class DeterminismTests(unittest.TestCase):
    """Spec §5 'Determinism', acceptance #9."""

    def test_plan_round_pure_function_is_repeatable(self) -> None:
        species_anchor = {"a": dp.SpeciesAnchorRow("a", "fam", "fire", "chaff", "demon.a",
                                                    ("m1", "m2"), ())}
        weights_by_key = {("species", "a"): _weights(), ("family", "fam"): _weights()}
        kwargs = dict(
            species_ids=["a"], family_members={"fam": ["a"]}, species_anchor=species_anchor,
            weights_by_key=weights_by_key, rung_table=dp.load_rung_table(
                REPO_ROOT / "data" / "tuning" / "action-rungs.v1.json"),
            family_ids=FAMILY_IDS, pairing_table={}, general_count=2, per_species_count=1,
            per_family_count=1, multiplicative_pairs=(("atom.keen-edge", "atom.cruelty"),),
            family_motif_max=6, corpus_hash="fixed", tuning_version=1,
        )
        r1 = dp.plan_round(**kwargs)
        r2 = dp.plan_round(**kwargs)
        self.assertEqual(r1, r2)

    def test_regenerate_is_byte_identical_across_two_real_runs(self) -> None:
        if not OUTPUT_PATH.is_file():
            self.skipTest("round-1.json not yet generated in this checkout")
        text1 = OUTPUT_PATH.read_text(encoding="utf-8")
        gen_mod.regenerate(write=True)
        text2 = OUTPUT_PATH.read_text(encoding="utf-8")
        self.assertEqual(text1, text2)
        self.assertTrue(text2.endswith("\n"))

    def test_provenance_records_corpus_hash_tuning_version_round(self) -> None:
        if not OUTPUT_PATH.is_file():
            self.skipTest("round-1.json not yet generated in this checkout")
        doc = json.loads(OUTPUT_PATH.read_text(encoding="utf-8"))
        self.assertIn("corpusHash", doc["_meta"])
        self.assertEqual(doc["_meta"]["round"], 1)
        self.assertEqual(doc["_meta"]["tuningVersion"], 2)
        for e in doc["entries"]:
            self.assertEqual(e["_provenance"]["corpusHash"], doc["_meta"]["corpusHash"])
            self.assertEqual(e["_provenance"]["round"], 1)


class ConstraintFourGateTests(unittest.TestCase):
    """Acceptance #6 -- allowedAtomFamilies identical across tiers against the REAL generated
    round, and constraint 4's three gates checked against the live tree.

    UPDATED 2026-09-04 (A-G1, spec-tier-access-gate.md): this class used to assert all three gates
    were absent, with a deliberate tripwire ("re-check whether constraint 4's gates are still
    absent") planted for whichever module landed the first of them. A-G1 landed two -- a per-rung
    `powerBudgetMilli` row (`data/tuning/action-rungs.v2.json`) and a rung-keyed budget check with a
    real C# production caller (`RpgStore.BuildActionCatalog`) -- so the tripwire fired, correctly,
    and is replaced below with tests that check the NEW state rather than the old absence."""

    def test_allowed_identical_across_tiers_in_the_real_round(self) -> None:
        if not OUTPUT_PATH.is_file():
            self.skipTest("round-1.json not yet generated in this checkout")
        doc = json.loads(OUTPUT_PATH.read_text(encoding="utf-8"))
        by_scope: "dict[str, set]" = {}
        for e in doc["entries"]:
            by_scope.setdefault(e["scope"], set()).add(tuple(sorted(e["pool"]["allowedAtomFamilies"])))
        for scope, sets in by_scope.items():
            self.assertEqual(len(sets), 1, f"{scope} briefs disagree on allowedAtomFamilies")
        distinct_across_scopes = {next(iter(s)) for s in by_scope.values()}
        self.assertEqual(len(distinct_across_scopes), 1,
                         "allowedAtomFamilies differs across scopes -- constraint 4 violated")

    def test_gate_1_power_budget_row_now_exists(self) -> None:
        # A-G1 gate 1: data/tuning/action-rungs.v2.json carries powerBudgetMilli on every row.
        doc = json.loads((REPO_ROOT / "data" / "tuning" / "action-rungs.v2.json").read_text(encoding="utf-8"))
        self.assertTrue(all("powerBudgetMilli" in row for row in doc["rows"]))

    def test_gate_3_power_budget_has_a_real_production_caller_now(self) -> None:
        # A-G1 gate 3: ContentValidation.Budget's rung-keyed overload is wired into
        # RpgStore.BuildActionCatalog (the WebMatchService battle-resolve path), not just its own
        # tests. A rejection reason naming the check is the marker.
        text = (REPO_ROOT / "src" / "FusionRpg.Data" / "Sqlite" / "RpgStore.ActionCatalog.cs").read_text(encoding="utf-8")
        self.assertIn("PowerBudgetExceeded", text)
        self.assertIn("ContentValidation.Budget", text)

    def test_gate_2_multiplicative_pricing_is_still_open(self) -> None:
        # A-G1 gate 2 (D2, multiplicative / family-aware non-additive pricing) is explicitly NOT
        # this module's to close -- confirm definitions.md still records it open rather than assume.
        text = (REPO_ROOT / "docs" / "architecture" / "effect-atom" / "definitions.md").read_text(encoding="utf-8")
        self.assertIn("multiplicative pricing is", text)
        self.assertIn("**open**, not solved", text)

    def test_family_widening_still_refused_with_two_of_three_gates_open(self) -> None:
        # The load-bearing assertion: landing gates 1 and 3 must not, by itself, let C1's
        # family-access widening through. Two of three is not three.
        allowed, _ = dp.build_pool(FAMILY_IDS, ())
        narrowed = tuple(sorted(FAMILY_IDS))[:10]
        with self.assertRaises(ValueError) as ctx:
            dp.validate_no_family_widening({"general": allowed, "family": allowed,
                                            "species": narrowed})
        self.assertIn("stays refused", str(ctx.exception))


class AtomFamilyRoundTests(unittest.TestCase):
    """Acceptance #6b -- every atom-family id anywhere in the real round is one of the 98."""

    def test_every_id_in_the_round_is_in_the_98(self) -> None:
        if not OUTPUT_PATH.is_file():
            self.skipTest("round-1.json not yet generated in this checkout")
        doc = json.loads(OUTPUT_PATH.read_text(encoding="utf-8"))
        for e in doc["entries"]:
            ids = e["pool"]["allowedAtomFamilies"] + e["pool"]["forbiddenAtomFamilies"]
            for fam_id in ids:
                self.assertIn(fam_id, FAMILY_IDS, f"{e['briefId']}: {fam_id!r} outside the 98")


class RestrictionAndFamilyAnchorKeyTests(unittest.TestCase):
    """Acceptance #7/#7b -- restriction-flagged count, and family-scope anchor keys present."""

    def setUp(self) -> None:
        if not OUTPUT_PATH.is_file():
            self.skipTest("round-1.json not yet generated in this checkout")
        self.doc = json.loads(OUTPUT_PATH.read_text(encoding="utf-8"))

    def test_restriction_unenforced_count_equals_species_scope_count(self) -> None:
        species_briefs = [e for e in self.doc["entries"] if e["scope"] == "species"]
        unenforced = [e for e in self.doc["entries"] if not e["slot"]["structureEnforced"]]
        self.assertEqual(len(unenforced), len(species_briefs))
        for e in unenforced:
            self.assertIn("restriction", e["slot"]["structureAxes"])

    def test_family_briefs_carry_the_three_keys_even_when_empty(self) -> None:
        for e in self.doc["entries"]:
            if e["scope"] != "family":
                continue
            self.assertIn("familyMotifs", e["anchor"])
            self.assertIn("familyAntiMotifs", e["anchor"])
            self.assertIn("familyMotifBasis", e["anchor"])


class MagicNumberAuditTests(unittest.TestCase):
    """Acceptance #6c's own tail clause and this session's own AST-based-audit precedent (Python
    is not covered by `scripts/audit-magic-numbers.py` -- confirmed directly: its `--summary`
    output lists no seedsmith/python domain, only C#-side domains). Every bare int/float literal
    in this module's own executable code must be structural (an index, a small fixed count, the
    1000 per-mille scale, a `Path.parents[N]` depth index, or the two `long` range bounds) —
    every count the ALGORITHM uses (generalCount, perFamilyCount, perSpeciesCount,
    multiplicativePairs, familyMotifMax) lives in `data/tuning/action-corpus-run.v1.json`, never
    as a literal here."""

    # 0/1/2 -- loop/tuple indices, ordinal +1, ceil(n/2). 4/7/10 -- the rung-window CEILINGS,
    # structurally tied to the shipped 10-row rung table's own shape (spec §3 step 4's literal
    # decision, not a balance-surface dial: moving a tier boundary means re-deriving
    # structureAxes from a different rung row, not tuning a number in isolation). 5/6 --
    # `Path(__file__).resolve().parents[N]` directory-depth indices. 1000 -- the per-mille scale.
    # The two `long` bounds close the CLAUDE.md overflow-guard allowlist, same as every prior
    # module.
    _ALLOWED = frozenset({0, 1, 2, 4, 5, 6, 7, 10, 1000,
                          9_223_372_036_854_775_807, 9_223_372_036_854_775_808})

    def test_zero_unallowlisted_numeric_literals(self) -> None:
        pkg_dir = Path(dp.__file__).resolve().parent
        files = list(pkg_dir.glob("*.py")) + [Path(gen_mod.__file__)]
        offenders: "list[str]" = []
        for f in files:
            tree = ast.parse(f.read_text(encoding="utf-8"), filename=str(f))
            for node in ast.walk(tree):
                if isinstance(node, ast.Constant) and isinstance(node.value, (int, float)) \
                        and not isinstance(node.value, bool):
                    if node.value not in self._ALLOWED:
                        offenders.append(f"{f.name}:{node.lineno} -> {node.value!r}")
        self.assertEqual(offenders, [], f"bare numeric literal(s) found: {offenders}")

    def test_audit_script_confirms_it_does_not_cover_python_paths(self) -> None:
        import subprocess
        result = subprocess.run(
            ["python", str(REPO_ROOT / "scripts" / "audit-magic-numbers.py"), "--summary"],
            capture_output=True, text=True, cwd=str(REPO_ROOT))
        self.assertNotIn("seedsmith", result.stdout.lower())


if __name__ == "__main__":
    unittest.main()
