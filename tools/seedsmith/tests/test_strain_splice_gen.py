"""Tests for item module 21, `strain-splice-gen` (docs/architecture/item/spec-strain-splice-gen.md).

    python -m pytest tools/seedsmith/tests/test_strain_splice_gen.py -q

⭐ **Almost every assertion here runs against the REAL shipped corpus**, not a synthetic fixture: the
40-gem / 34-family insert vocabulary, the 740-entry base-type corpus, the 25 legacy socket-words, the
36-row build-theme registry, the twelve-aptitude roster and both live tuning files. That is
deliberate, and it is what caught this module's largest finding — the spec's central claim that "no
Strain and no Splice is buildable on any shipped chassis" is **stale**: module 6 re-issued the
`socketMax` table on 2026-09-04 and `armament-primary` / `core-guard` now reach 4.

Where a count is a moving target the test asserts the RELATIONSHIP rather than the number; where the
number IS the finding (the 25 legacy entries, the 102 cells) it is asserted exactly.
"""
from __future__ import annotations

import json
import subprocess
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.adapters.items.combogen import (  # noqa: E402
    brief as brief_mod,
    catalogue as catalogue_mod,
    emit,
    grid,
    migrate as migrate_mod,
    run as run_mod,
    schema as schema_mod,
    supply as supply_mod,
    tuning as tuning_mod,
)
from seedsmith.corpus import Corpus  # noqa: E402
from seedsmith.metrics import Ctx, Severity  # noqa: E402
from seedsmith.metrics.linkage import COMBINATION_KINDS, CombinationIngredients  # noqa: E402
from seedsmith.pipeline.model import BLOCKED_FIELD, Pipeline, audit_schema  # noqa: E402
from seedsmith.planner.schedule import DEFAULT_MODEL_TIERS  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parents[3]
ITEMS_ROOT = REPO_ROOT / "data" / "seed" / "items"
COMBOGEN_DIR = (Path(__file__).resolve().parents[1] / "seedsmith" / "adapters" / "items"
                / "combogen")

TUNING = tuning_mod.load()
SUPPLY = supply_mod.build()
HOST_ROLES = TUNING.host_roles()
GRANTED = run_mod.granted_family_vocabulary(SUPPLY)


def _entry(entry_id: str, kind: str, data: dict):
    """One synthetic corpus row — the only synthetic fixtures in this file, and they exist to prove
    a metric covers a kind id no shipped file carries YET."""
    from seedsmith.corpus import Entry
    return Entry(id=entry_id, kind=kind, partition="test", path=f"{kind}/{entry_id}.json",
                 data={"id": entry_id, **data})


def _base_type_entries() -> "list[dict]":
    out: "list[dict]" = []
    for path in sorted((ITEMS_ROOT / "base-types").rglob("*.json")):
        doc = json.loads(path.read_text(encoding="utf-8"))
        if doc.get("kind") != "base-type":
            continue
        out.extend(doc.get("entries", []))
    return out


# ── the grid ────────────────────────────────────────────────────────────────────────────────────

class GridTests(unittest.TestCase):

    def test_the_grid_yields_exactly_36_strains_and_66_splices(self):
        aptitudes = grid.load_aptitudes()
        archetypes = grid.archetypes()
        self.assertEqual(12, len(aptitudes))
        self.assertEqual(3, len(archetypes))
        self.assertEqual(36, len(grid.strain_cells()))
        self.assertEqual(66, len(grid.splice_cells()))
        self.assertEqual(102, len(grid.all_cells()))
        # The re-derivation, so a thirteenth aptitude grows the grid instead of going red.
        n = len(aptitudes)
        self.assertEqual(n * len(archetypes), len(grid.strain_cells()))
        self.assertEqual(n * (n - 1) // 2, len(grid.splice_cells()))

    def test_the_two_axes_are_read_never_transcribed(self):
        # `assert_grid_agrees` re-measures the registry against the roster on every call.
        self.assertEqual((12, 3), grid.assert_grid_agrees())

    def test_a_grid_that_moved_raises_instead_of_being_absorbed(self):
        rows = grid.load_aptitudes()
        themes = grid.load_build_themes()
        # An aptitude the registry does not cover — a thirteenth landing with no Strain cells.
        extra = rows + (grid.AptitudeRow("Umbrage", 12, "force", "made up", "For the test."),)
        with self.assertRaises(grid.GridDrift):
            grid.assert_grid_agrees(extra, themes)
        # …and an incomplete product: drop one cell and the grid is no longer 12 x 3.
        with self.assertRaises(grid.GridDrift):
            grid.assert_grid_agrees(rows, themes[:-1])

    def test_a_splice_pair_is_unordered_by_id_construction(self):
        by_id = {a.id: a for a in grid.load_aptitudes()}
        might, agility = by_id["Might"], by_id["Agility"]
        self.assertEqual(
            "combo.splice-might-agility",
            emit.splice_id(might.token, might.ordinal, agility.token, agility.ordinal))
        self.assertEqual(
            "combo.splice-might-agility",
            emit.splice_id(agility.token, agility.ordinal, might.token, might.ordinal))
        ids = [emit.combo_id(c) for c in grid.splice_cells()]
        self.assertEqual(len(ids), len(set(ids)))
        with self.assertRaises(emit.IdRefused):
            emit.splice_id("might", 0, "might", 0)

    def test_every_minted_id_is_a_legal_container_id(self):
        ids = [emit.combo_id(c) for c in grid.all_cells()]
        self.assertEqual(102, len(set(ids)))
        for minted in ids:
            self.assertEqual(1, minted.count("."))
            self.assertRegex(minted, r"^combo\.(strain|splice)-[a-z0-9]+(-[a-z0-9]+)*$")

    def test_no_id_name_or_prompt_contains_the_word_runeword(self):
        # ⛔ D20, over the emitted ids AND every source file in the package AND a real brief.
        for minted in (emit.combo_id(c) for c in grid.all_cells()):
            self.assertEqual([], grid.scan_for_banned_word(minted))
        # ⚠ Exactly three files may say it, and each because it ENFORCES the ban: `__init__.py`
        # states the rule at the top of the package, `grid.py` holds the constant and the scanner,
        # `brief.py` applies the scanner to its own output. Anywhere else — a name, a comment, a
        # docstring — is the drift D20 banned the word to prevent, and the set comparison catches a
        # fourth file the day it appears.
        saying_it = {path.name for path in sorted(COMBOGEN_DIR.glob("*.py"))
                     if grid.scan_for_banned_word(path.read_text(encoding="utf-8"))}
        self.assertEqual({"__init__.py", "grid.py", "brief.py"}, saying_it)
        cell = grid.strain_cells()[0]
        text = brief_mod.build_brief(cell, TUNING, SUPPLY,
                                     granted_families=GRANTED, host_roles=HOST_ROLES)
        self.assertEqual([], grid.scan_for_banned_word(text))

    def test_a_strain_cell_reuses_module_13s_theme_key_rather_than_minting_a_new_one(self):
        keys = {c.theme_key for c in grid.strain_cells()}
        self.assertEqual(36, len(keys))
        self.assertTrue(all(k.startswith("build.") for k in keys))
        # A Splice is a PAIR of build themes, not a 37th one — it deliberately carries no key.
        self.assertEqual({None}, {c.theme_key for c in grid.splice_cells()})


# ── the tuning files ────────────────────────────────────────────────────────────────────────────

class TuningTests(unittest.TestCase):

    def test_the_shipped_tuning_carries_D20s_four_ingredient_plan(self):
        self.assertEqual(4, TUNING.ingredient_count)
        self.assertEqual(TUNING.ingredient_count, len(TUNING.min_tier_plan))
        self.assertEqual(sorted(TUNING.min_tier_plan), list(TUNING.min_tier_plan))
        self.assertEqual(45, TUNING.catalogue_size_bar)
        self.assertEqual(1, TUNING.attuned_tier_bonus)

    def test_the_parser_refuses_rather_than_defaults(self):
        raw = json.loads(tuning_mod.STRAIN_SPLICE_PATH.read_text(encoding="utf-8"))
        for section in ("recipe", "learnability", "distinctness"):
            stripped = {k: v for k, v in raw.items() if k != section}
            path = self._tmp(stripped)
            with self.assertRaises(tuning_mod.ComboTuningError):
                tuning_mod.load(path)

    def test_a_key_module_16_owns_is_refused_as_a_fork(self):
        # ⛔ Two sources of truth for the ingredient count is how a generated combination stops
        # matching the evaluator that has to fire it.
        raw = json.loads(tuning_mod.STRAIN_SPLICE_PATH.read_text(encoding="utf-8"))
        raw["recipe"]["ingredientCount"] = 4
        with self.assertRaises(tuning_mod.ComboTuningError) as ctx:
            tuning_mod.load(self._tmp(raw))
        self.assertIn("sockets.v1.json", str(ctx.exception))

    def test_a_min_tier_plan_that_disagrees_with_the_ingredient_count_raises(self):
        raw = json.loads(tuning_mod.STRAIN_SPLICE_PATH.read_text(encoding="utf-8"))
        raw["recipe"]["minTierPlan"] = [1, 1, 2]
        with self.assertRaises(tuning_mod.ComboTuningError) as ctx:
            tuning_mod.load(self._tmp(raw))
        self.assertIn("ingredient count", str(ctx.exception))

    def test_a_min_tier_outside_the_shipped_ladder_raises(self):
        raw = json.loads(tuning_mod.STRAIN_SPLICE_PATH.read_text(encoding="utf-8"))
        raw["recipe"]["minTierPlan"] = [1, 1, 2, TUNING.insert_tier_count + 1]
        with self.assertRaises(tuning_mod.ComboTuningError) as ctx:
            tuning_mod.load(self._tmp(raw))
        self.assertIn("insert ladder", str(ctx.exception))

    def test_the_tuning_file_carries_no_content_ceiling(self):
        # A cap on how many combinations may exist would be a hard progression ceiling on content
        # breadth (AGENTS.md). D17's dead tail, protected the way module 13 protects its own.
        text = tuning_mod.STRAIN_SPLICE_PATH.read_text(encoding="utf-8")
        for forbidden in ("maxCombinations", "maxStrains", "maxSplices", "gridCap"):
            self.assertNotIn(forbidden, text)
        self.assertIn("REPORTED, NEVER ENFORCED", text)

    def test_the_python_and_csharp_host_role_derivations_agree(self):
        # `SocketGeometry.RolesThatCanHostAStrain` is the C# half; this is the Python mirror, and
        # both read the same shipped file. Two derivations that agree beat one plus a literal.
        self.assertEqual(("armament-primary", "core-guard"), HOST_ROLES)
        sockets = json.loads(tuning_mod.SOCKETS_PATH.read_text(encoding="utf-8"))
        expected = tuple(sorted(r for r, c in sockets["socketCeiling"].items()
                                if c >= sockets["strainSplice"]["ingredientCount"]))
        self.assertEqual(expected, HOST_ROLES)

    def _tmp(self, doc: dict) -> Path:
        import os
        import tempfile
        fd, name = tempfile.mkstemp(suffix=".json")
        os.close(fd)                       # Windows refuses a second open on a live handle
        path = Path(name)
        path.write_text(json.dumps(doc), encoding="utf-8")
        self.addCleanup(path.unlink, missing_ok=True)
        return path


# ── the module-6 dependency, flipped ────────────────────────────────────────────────────────────

class ChassisTests(unittest.TestCase):

    def test_a_shipped_base_type_can_now_host_a_four_ingredient_combination(self):
        """⭐ The spec's `no_shipped_base_type_can_host_a_four_ingredient_combination_today`
        fixture, in its FLIPPED form.

        The spec measured 2026-09-03 and found a maximum `socketMax` of 2 across 740 entries, with
        `jewel-minor-a` carrying 24 rows where the field was ABSENT. Neither is true of the corpus
        committed 2026-09-04 (`dcabac3 update seeds`): the maximum is 4, no row omits the field, and
        the two roles that reach 4 are exactly the two ssot-sockets §4.1 assigns 4.
        """
        entries = _base_type_entries()
        self.assertGreaterEqual(len(entries), 740)
        by_role: "dict[str, int]" = {}
        for entry in entries:
            role = entry["role"]
            by_role[role] = max(by_role.get(role, 0), int(entry.get("socketMax", 0)))
        self.assertEqual(TUNING.ingredient_count, max(by_role.values()))
        can_host = tuple(sorted(r for r, m in by_role.items() if m >= TUNING.ingredient_count))
        self.assertEqual(("armament-primary", "core-guard"), can_host)
        # …and the corpus agrees with the tuning about who they are.
        self.assertEqual(HOST_ROLES, can_host)

    def test_the_real_per_actor_splice_ceiling_is_two_and_the_backstop_is_non_binding(self):
        self.assertEqual(2, TUNING.geometric_combo_ceiling())
        self.assertGreater(TUNING.max_combos_per_actor, TUNING.geometric_combo_ceiling())


# ── the schema, and P1 ──────────────────────────────────────────────────────────────────────────

class SchemaTests(unittest.TestCase):

    def schema(self) -> dict:
        return schema_mod.combination_schema(
            TUNING, supplied_families=SUPPLY.families, host_roles=HOST_ROLES,
            granted_families=GRANTED)

    def test_the_schema_is_audit_schema_clean(self):
        self.assertEqual([], audit_schema(self.schema()))

    def test_a_bare_integer_magnitude_field_fails_pipeline_construction(self):
        # Mechanical P1, proven rather than asserted: `Pipeline.__post_init__` runs the audit at
        # CONSTRUCTION, so a numeric field never reaches a call.
        bad = self.schema()
        bad["properties"]["grantedTier"] = {"type": "integer"}
        with self.assertRaises(ValueError) as ctx:
            Pipeline(metric="strain-splice-gen", scope="test", schema=bad,
                     gate=lambda _: [], on_persist=lambda _k, _v: None)
        self.assertIn("magnitudes come from", str(ctx.exception))

    def test_blocked_is_a_legal_answer_and_writes_nothing(self):
        written: "list[tuple[str, dict]]" = []
        pipeline = Pipeline(metric="strain-splice-gen", scope="test", schema=self.schema(),
                            gate=lambda _: [], on_persist=lambda k, v: written.append((k, v)))
        self.assertIn(BLOCKED_FIELD, pipeline.schema["properties"])
        self.assertEqual([], written)

    def test_every_combination_takes_exactly_four_ingredients(self):
        node = self.schema()["properties"]["ingredients"]
        self.assertEqual(TUNING.ingredient_count, node["minItems"])
        self.assertEqual(TUNING.ingredient_count, node["maxItems"])

    def test_the_schema_offers_no_tier_no_cost_and_no_min_tier(self):
        names = schema_mod.schema_field_names(self.schema())
        for banned in ("tier", "cost", "chance", "duration", "minTier", "baseTier", "position"):
            self.assertNotIn(banned, names)

    def test_the_host_role_enum_is_closed_to_roles_that_can_actually_hold_four(self):
        self.assertEqual(list(HOST_ROLES), self.schema()["properties"]["hostRole"]["enum"])

    def test_a_schema_with_no_supplied_family_or_no_host_role_is_refused(self):
        with self.assertRaises(ValueError):
            schema_mod.combination_schema(TUNING, supplied_families=(), host_roles=HOST_ROLES,
                                          granted_families=GRANTED)
        with self.assertRaises(ValueError):
            schema_mod.combination_schema(TUNING, supplied_families=SUPPLY.families,
                                          host_roles=(), granted_families=GRANTED)


# ── the gem-supply precheck ─────────────────────────────────────────────────────────────────────

class SupplyTests(unittest.TestCase):

    def test_the_live_gem_corpus_supplies_the_ingredient_vocabulary(self):
        self.assertEqual(40, SUPPLY.gem_count)
        self.assertEqual(34, SUPPLY.family_count)
        self.assertEqual(SUPPLY.family_count, len(set(SUPPLY.families)))

    def test_every_ingredient_family_is_supplied_by_a_live_gem(self):
        # The schema's enum IS the supplied set, so a well-formed answer cannot name anything else.
        node = schema_mod.combination_schema(
            TUNING, supplied_families=SUPPLY.families, host_roles=HOST_ROLES,
            granted_families=GRANTED)["properties"]["ingredients"]["items"]
        self.assertEqual([], SUPPLY.refuse(node["enum"]))

    def test_the_precheck_refuses_an_unsupplied_family_before_any_call(self):
        with self.assertRaises(supply_mod.SupplyRefused) as ctx:
            supply_mod.precheck(["atom.might", "atom.nonesuch"], SUPPLY)
        self.assertIn("atom.nonesuch", str(ctx.exception))
        self.assertNotIn("atom.might", str(ctx.exception))

    def test_the_gating_metric_agrees_with_the_precheck_on_the_live_corpus(self):
        corpus = Corpus.load(ITEMS_ROOT)
        findings = CombinationIngredients().run(Ctx(corpus=corpus, adapter=None))
        self.assertEqual([], findings)
        # …and the legacy corpus's own ingredient families are all supplied, which is why.
        legacy = migrate_mod.load_legacy()
        wanted = {f for e in legacy for f in e.ingredient_families}
        self.assertEqual([], SUPPLY.refuse(sorted(wanted)))


# ── the kind rename, and the gate that must follow it ───────────────────────────────────────────

class KindMigrationTests(unittest.TestCase):

    def test_IngredientUnsatisfiable_gates_after_the_kind_rename(self):
        """⭐ The migration's real risk, closed permanently rather than at cutover.

        The 2026-09-04 ruling warns that the metric "must follow the kind, or a `gates = True` check
        quietly stops gating". A metric keyed on ONE spelling does exactly that — it goes on passing,
        over zero rows, and nothing says so. So the gate reads BOTH ids.
        """
        self.assertEqual(("socket-word", "combination"), COMBINATION_KINDS)
        self.assertTrue(CombinationIngredients.gates)
        self.assertEqual("Registration/IngredientUnsatisfiable", CombinationIngredients.id)

        # A `combination`-kinded row with an unsupplied family is caught exactly as a socket-word is.
        for kind in COMBINATION_KINDS:
            corpus = Corpus()
            corpus.add(_entry("gem.a", "gem", {"family": "atom.might", "powerBand": "high"}))
            corpus.add(_entry("x.001", kind, {
                "name": "Test", "ingredients": [{"family": "atom.nonesuch"}]}))
            findings = CombinationIngredients().run(Ctx(corpus=corpus, adapter=None))
            self.assertEqual(1, len(findings), f"kind {kind!r} is not covered by the gate")
            self.assertEqual(Severity.GAP, findings[0].severity)
            self.assertEqual(kind, findings[0].evidence["kind"])

    def test_the_KINDS_assertion_still_holds(self):
        from seedsmith.adapters.items.kinds import KINDS
        self.assertEqual(15, len(KINDS))
        self.assertEqual(15, len({k.kind for k in KINDS}))
        # ⏸ The rename itself is bundled with the regeneration run — see `migrate.py`'s own docstring
        # and this module's todo entry. What is asserted here is that the port still has 15 kinds,
        # so the rename stays "renamed, not removed" whenever it lands.
        self.assertIn("socket-word", {k.kind for k in KINDS})

    def test_a_combination_gets_the_stronger_model_like_a_socket_word_did(self):
        self.assertEqual(DEFAULT_MODEL_TIERS.for_kind("socket-word"),
                         DEFAULT_MODEL_TIERS.for_kind("combination"))
        self.assertEqual(DEFAULT_MODEL_TIERS.strong, DEFAULT_MODEL_TIERS.for_kind("combination"))

    def test_not_one_legacy_socket_word_is_a_legal_combination_today(self):
        """⛔ The evidence behind "regenerate, do not retain" — measured, not asserted."""
        report = migrate_mod.legality_report(TUNING, host_roles=HOST_ROLES)
        self.assertEqual(25, report.total)
        self.assertEqual(0, len(report.legal))
        self.assertEqual(25, len(report.illegal))
        # The four reasons, each present somewhere in the corpus.
        joined = " ".join(r for reasons in report.problems.values() for r in reasons)
        self.assertIn("ingredients, not D20's 4", joined)
        self.assertIn("`position`", joined)
        self.assertIn("gem.word-", joined)
        self.assertIn("minSockets", joined)

    def test_the_ward_array_hosted_legacy_entry_is_named_as_unhostable(self):
        # It is outside the twelve-role hybrid core AND its ceiling cannot reach four, so it can
        # never be worn by a hybrid nor hold the recipe. Named rather than silently regenerated.
        report = migrate_mod.legality_report(TUNING, host_roles=HOST_ROLES)
        hosted = [e for e in report.entries if e.host_role == "ward-array"]
        self.assertEqual(1, len(hosted))
        self.assertTrue(any("ward-array" in r for r in report.problems[hosted[0].id]))

    def test_every_migration_site_still_exists(self):
        self.assertEqual([], migrate_mod.missing_sites())
        self.assertGreaterEqual(len(migrate_mod.MIGRATION_SITES), 7)


# ── emit ────────────────────────────────────────────────────────────────────────────────────────

class EmitTests(unittest.TestCase):

    def test_the_same_ingredients_in_any_arrangement_fold_to_the_same_rows(self):
        # D41 at the emit layer, not only at the matcher.
        picks = ["atom.bulwark", "atom.might", "atom.bulwark", "atom.vitality"]
        rows = emit.ingredient_rows(picks, TUNING)
        for arrangement in ([picks[i] for i in order] for order in
                            ((3, 2, 1, 0), (1, 0, 3, 2), (2, 3, 0, 1))):
            self.assertEqual(rows, emit.ingredient_rows(arrangement, TUNING))
        self.assertEqual(TUNING.ingredient_count, sum(r.quantity for r in rows))

    def test_an_ingredient_row_carries_no_position(self):
        row = emit.ingredient_rows(["atom.might"] * 4, TUNING)[0].to_dict()
        self.assertNotIn("position", row)
        self.assertEqual({"family", "minTier", "quantity"}, set(row))

    def test_a_wrong_ingredient_count_is_refused_at_the_emit_boundary(self):
        with self.assertRaises(emit.IdRefused):
            emit.ingredient_rows(["atom.might"] * 3, TUNING)

    def test_min_sockets_is_derived_never_authored(self):
        self.assertEqual(TUNING.ingredient_count, emit.min_sockets(TUNING))

    def test_matching_affinity_grants_an_enhanced_tier_and_never_gates(self):
        cell = grid.strain_cells()[0]
        plain = emit.granted_tier(cell, TUNING, all_attuned=False)
        attuned = emit.granted_tier(cell, TUNING, all_attuned=True)
        self.assertEqual(TUNING.attuned_tier_bonus, attuned - plain)
        self.assertGreaterEqual(plain, 1)   # failure is impossible; a mismatch still produces one

    def test_resonance_affinity_stays_a_plus_one_and_is_not_re_specified_here(self):
        # ⚠ The two layers treat affinity differently ON PURPOSE — soft `+1` to Pure's effective
        # count, an enhanced tier for a Strain/Splice — and this module re-specifies neither.
        sockets = json.loads(tuning_mod.SOCKETS_PATH.read_text(encoding="utf-8"))
        self.assertEqual(1, sockets["resonance"]["attunedEffectiveCountBonus"])
        self.assertEqual(1, sockets["resonance"]["attunedTierBonus"])
        # …and nothing in this package READS Pure's own bonus. Asserted as the read shape
        # (`"attunedEffectiveCountBonus")`), because `tuning.py` names the key deliberately — in the
        # list of keys it REFUSES, which is the opposite of reading it.
        for path in sorted(COMBOGEN_DIR.glob("*.py")):
            self.assertNotIn('"attunedEffectiveCountBonus")', path.read_text(encoding="utf-8"))
        self.assertIn("attunedEffectiveCountBonus", tuning_mod.SOCKETS_OWNED_KEYS)


# ── the brief ───────────────────────────────────────────────────────────────────────────────────

class BriefTests(unittest.TestCase):

    def brief(self, cell) -> str:
        return brief_mod.build_brief(cell, TUNING, SUPPLY,
                                     granted_families=GRANTED, host_roles=HOST_ROLES)

    def test_the_brief_carries_the_roster_words_verbatim(self):
        cell = grid.strain_cells()[0]
        apt = cell.aptitudes[0]
        text = self.brief(cell)
        self.assertIn(apt.meaning, text)
        self.assertIn(apt.reading, text)

    def test_the_brief_states_no_number_the_schema_already_fixes(self):
        for cell in (grid.strain_cells()[0], grid.splice_cells()[0]):
            self.assertEqual([], brief_mod.spells_the_count(self.brief(cell),
                                                            TUNING.ingredient_count))

    def test_the_brief_names_no_element(self):
        # ⚠ No aptitude → element mapping is introduced; the gap stays visible.
        for cell in (grid.strain_cells()[0], grid.splice_cells()[0]):
            text = self.brief(cell).lower()
            for element in ("fire", "ice", "earth", "air", "light", "dark", "omni"):
                self.assertNotIn(f" {element} ", text)

    def test_a_brief_that_spelled_the_count_would_be_refused(self):
        # The guard is real, not decorative — proven by feeding it a text that breaks it.
        with self.assertRaises(brief_mod.BriefRefused):
            brief_mod.build_brief(
                grid.strain_cells()[0], TUNING, SUPPLY, granted_families=GRANTED,
                host_roles=HOST_ROLES + ("four-ingredient-role",))

    def test_a_combination_asks_for_a_mechanism_not_a_flat_add(self):
        text = self.brief(grid.strain_cells()[0])
        self.assertIn("MECHANISM", text)
        self.assertIn("volume discount with a name", text)


# ── the run plan ────────────────────────────────────────────────────────────────────────────────

class RunTests(unittest.TestCase):

    def test_the_plan_covers_every_cell_of_its_shape(self):
        strains = run_mod.plan_run(shape="strain", tuning=TUNING, supply=SUPPLY)
        splices = run_mod.plan_run(shape="splice", tuning=TUNING, supply=SUPPLY)
        self.assertEqual(36, len(strains.subjects))
        self.assertEqual(66, len(splices.subjects))
        self.assertTrue(strains.complete and splices.complete)
        ids = [s.entry_id for s in strains.subjects] + [s.entry_id for s in splices.subjects]
        self.assertEqual(102, len(set(ids)))

    def test_re_running_over_an_unchanged_grid_is_byte_identical(self):
        first = run_mod.plan_run(shape="strain", tuning=TUNING, supply=SUPPLY)
        second = run_mod.plan_run(shape="strain", tuning=TUNING, supply=SUPPLY)
        self.assertEqual([s.to_dict() for s in first.subjects],
                         [s.to_dict() for s in second.subjects])
        self.assertEqual([s.brief for s in first.subjects], [s.brief for s in second.subjects])
        self.assertEqual(first.summary(), second.summary())

    def test_an_unknown_shape_is_refused(self):
        with self.assertRaises(ValueError):
            run_mod.plan_run(shape="word", tuning=TUNING, supply=SUPPLY)

    def test_the_catalogue_size_is_reported_as_127_against_the_45_bar(self):
        report = catalogue_mod.report(TUNING, strains=36, splices=66)
        self.assertEqual(25, report.resonances)
        self.assertEqual(127, report.total)
        self.assertEqual(45, report.bar)
        self.assertTrue(report.over_bar)
        self.assertEqual(2822, report.ratio_permille)     # 2.8x, computed not quoted
        payload = report.to_dict()
        self.assertFalse(payload["enforced"])
        self.assertEqual(2, len(payload["requiredMitigations"]))
        self.assertTrue(all(m["owner"] == catalogue_mod.MITIGATION_OWNER
                            for m in payload["requiredMitigations"]))

    def test_the_resonance_half_of_the_count_is_derived_from_module_16s_own_tuning(self):
        sockets = json.loads(tuning_mod.SOCKETS_PATH.read_text(encoding="utf-8"))
        core = json.loads((ITEMS_ROOT / "_registry" / "core.v1.json").read_text(encoding="utf-8"))
        expected = (len(core["elements"]["concrete"]) * len(sockets["resonance"]["pureThresholds"])
                    + len(sockets["resonance"]["ringOrder"]) + 1
                    + len(sockets["resonance"]["diversityThresholds"]))
        self.assertEqual(expected, catalogue_mod.generated_resonance_count())


# ── the CLI ─────────────────────────────────────────────────────────────────────────────────────

class CliTests(unittest.TestCase):

    def _run(self, *args: str) -> "subprocess.CompletedProcess[str]":
        return subprocess.run(
            [sys.executable, "-m", "seedsmith", *args],
            cwd=str(Path(__file__).resolve().parents[1]),
            capture_output=True, text=True, encoding="utf-8", errors="replace")

    def test_items_generate_kind_combination_plans_both_shapes(self):
        for shape, expected in (("strain", 36), ("splice", 66)):
            done = self._run("items", "generate", "--kind", "combination", "--shape", shape,
                             "--dry-run")
            self.assertEqual(0, done.returncode, done.stderr)
            payload = json.loads(done.stdout)
            self.assertEqual(expected, payload["toGenerate"])
            self.assertEqual("combination", payload["kind"])
            self.assertEqual(127, payload["catalogue"]["total"])
            self.assertEqual(0, payload["legacyRetirement"]["legalAsCombinationsToday"])

    def test_write_is_refused_rather_than_writing_nothing(self):
        done = self._run("items", "generate", "--kind", "combination", "--shape", "strain",
                         "--write")
        self.assertEqual(3, done.returncode)
        self.assertIn("refused", done.stderr)

    def test_population_is_refused_for_a_combination_rather_than_ignored(self):
        done = self._run("items", "generate", "--kind", "combination", "--shape", "strain",
                         "--population", "build")
        self.assertEqual(2, done.returncode)
        self.assertIn("does not apply", done.stderr)


if __name__ == "__main__":
    unittest.main()
