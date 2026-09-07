"""Tests for `seedsmith.adapters.items.materialgen` (item module 3, `materials-gen`,
docs/architecture/item-seedgen/spec-materials-gen.md).

    python -m pytest tools/seedsmith/tests/test_materials_gen.py -v

⭐ **`VocabTests` is the spec's own #1 boundary.** Acceptance #2 requires confirming a target id is a
real member of the closed vocabulary BEFORE generating — refusing rather than authoring for an id
that doesn't exist mechanically. `test_a_fabricated_id_is_refused_loudly` and
`test_a_legacy_shard_id_is_known_but_refused_as_a_generation_target` are that requirement, proven
directly against `vocab.require_issuable` — the one gate every other module in this package calls
before it does anything with an id.
"""
from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.adapters.items.materialgen import brief as brief_mod  # noqa: E402
from seedsmith.adapters.items.materialgen import emit  # noqa: E402
from seedsmith.adapters.items.materialgen import run as run_mod  # noqa: E402
from seedsmith.adapters.items.materialgen import schema as schema_mod  # noqa: E402
from seedsmith.adapters.items.materialgen import vocab  # noqa: E402
from seedsmith.pipeline.run_ledger import RunLedger  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parents[3]
REAL_MATERIALS_PATH = REPO_ROOT / "data" / "seed" / "items" / "materials" / "materials.json"


def _clean_answer(**overrides) -> dict:
    draft = {"name": "Proof Shard", "flavor": "A fixture, and it says so, in one full sentence.",
             "tags": ["mineral"]}
    draft.update(overrides)
    return draft


class VocabTests(unittest.TestCase):
    """The closed, 27-id issuable vocabulary, mirrored from `MaterialCatalog.cs`."""

    def test_the_vocabulary_is_exactly_27_ids_in_class_order(self) -> None:
        self.assertEqual(len(vocab.ISSUABLE), 27)
        classes = [m.material_class for m in vocab.ISSUABLE]
        self.assertEqual(classes, ["shard"] * 10 + ["substrate"] * 8 + ["essence"] * 6 + ["catalyst"] * 3)

    def test_shard_ids_follow_the_rarity_ladder_in_rank_order(self) -> None:
        shard_ids = [m.runtime_id for m in vocab.ISSUABLE if m.material_class == "shard"]
        self.assertEqual(shard_ids, [f"shard.{r}" for r in vocab.RARITY_RUNGS])
        self.assertEqual(shard_ids[0], "shard.chaff")   # weakest rung, ordinal 0
        self.assertEqual(shard_ids[-1], "shard.almanac")  # strongest rung, ordinal 9

    def test_substrate_ids_are_frame_outer_grade_inner(self) -> None:
        substrate_ids = [m.runtime_id for m in vocab.ISSUABLE if m.material_class == "substrate"]
        self.assertEqual(substrate_ids, [
            "substrate.humanoid.crude", "substrate.humanoid.sound",
            "substrate.humanoid.fine", "substrate.humanoid.prime",
            "substrate.plant.crude", "substrate.plant.sound",
            "substrate.plant.fine", "substrate.plant.prime",
        ])

    def test_essence_ids_exclude_omni(self) -> None:
        essence_ids = [m.runtime_id for m in vocab.ISSUABLE if m.material_class == "essence"]
        self.assertEqual(essence_ids, [f"essence.{e}" for e in vocab.ELEMENTS])
        self.assertNotIn("essence.omni", essence_ids)

    def test_a_fabricated_id_is_refused_loudly(self) -> None:
        """THE refusal test. A material id outside the 27-id closed vocabulary must never reach
        content authoring — it fails loudly, never silently inventing a new material kind."""
        for bad_id in ("shard.mythic", "essence.omni", "substrate.humanoid.masterwork",
                      "catalyst.dissolve", "material.made-up", "essence.fire.pvz", ""):
            with self.subTest(bad_id=bad_id):
                self.assertFalse(vocab.is_issuable(bad_id))
                with self.assertRaises(vocab.MaterialVocabularyRejection):
                    vocab.require_issuable(bad_id)

    def test_a_legacy_shard_id_is_known_but_refused_as_a_generation_target(self) -> None:
        """The four retired bands resolve (a saved reference does not hard-fail) but are NOT
        issuable, and `require_issuable` — the generator's own gate — refuses all four with a
        message distinct from "not a real id at all"."""
        for legacy_id in ("shard.common", "shard.rare", "shard.epic", "shard.legendary"):
            with self.subTest(legacy_id=legacy_id):
                self.assertTrue(vocab.is_known(legacy_id))
                self.assertFalse(vocab.is_issuable(legacy_id))
                with self.assertRaises(vocab.MaterialVocabularyRejection) as caught:
                    vocab.require_issuable(legacy_id)
                self.assertIn("legacy", str(caught.exception))

    def test_the_real_shipped_corpus_now_carries_every_rarity_ladder_shard_rung(self) -> None:
        """Read-only sanity check against the REAL, committed corpus (never written to by this
        test): the gap this generator exists to close is now CLOSED — `materials-gen` authored the
        ten missing `shard.{rung}` rows (material.022-031, `_meta.amendments[0]`, 2026-09-07) — so
        `missing_from` reports nothing left, and every issuable id in the vocabulary resolves to a
        real corpus row. The four legacy shard-band ids (material.007-010) are untouched and still
        do not carry any of the ten rung ids, which is what keeps this a re-author rather than a
        rewrite of the legacy rows."""
        doc = json.loads(REAL_MATERIALS_PATH.read_text(encoding="utf-8"))
        existing_runtime_ids = {e["runtimeId"] for e in doc["entries"] if "runtimeId" in e}
        missing = {m.runtime_id for m in vocab.missing_from(frozenset(existing_runtime_ids))}
        self.assertEqual(missing, set())
        for rung in vocab.RARITY_RUNGS:
            self.assertIn(f"shard.{rung}", existing_runtime_ids)


class SchemaTests(unittest.TestCase):
    def test_a_clean_answer_has_no_schema_defects(self) -> None:
        from seedsmith.adapters.items.setgen.answers import schema_defects
        self.assertEqual(schema_defects(_clean_answer(), schema_mod.material_schema()), [])

    def test_each_closed_keyword_is_enforced(self) -> None:
        from seedsmith.adapters.items.setgen.answers import schema_defects
        schema = schema_mod.material_schema()
        cases = {
            "unknown field": ({**_clean_answer(), "runtimeId": "shard.chaff"}, "unknown field"),
            "tag outside the pool": ({**_clean_answer(), "tags": ["not-a-real-tag"]}, "is not one of"),
            "short name": ({**_clean_answer(), "name": "x"}, "below the minimum"),
            "too many tags": ({**_clean_answer(),
                              "tags": ["mineral", "metal", "organic", "arcane", "necrotic"]},
                             "above the maximum"),
        }
        for label, (draft, needle) in cases.items():
            with self.subTest(label):
                found = schema_defects(draft, schema)
                self.assertTrue(any(needle in d for d in found), f"{label}: {found}")

    def test_two_tags_on_one_exclusive_axis_is_a_violation(self) -> None:
        violations = schema_mod.tag_axis_violations(["light", "heavy"])  # both mass-class
        self.assertTrue(violations, "light/heavy are both mass-class, which tags.v1.json marks exclusive")

    def test_tags_from_different_axes_are_legal_together(self) -> None:
        # "metal" (material-nature) + "light" (mass-class) + "sturdy" (durability-class) — the real
        # combination shipped materials.json's own substrate-humanoid-crude entry uses two of.
        self.assertEqual(schema_mod.tag_axis_violations(["metal", "light", "sturdy"]), [])

    def test_material_is_a_legal_appliesto_for_every_axis_this_module_offers(self) -> None:
        axes = schema_mod.material_tag_axes()
        self.assertIn("mass-class", axes)
        self.assertIn("material-nature", axes)
        self.assertGreater(len(schema_mod.material_tags()), 0)


class BriefTests(unittest.TestCase):
    def test_the_brief_never_shows_a_number(self) -> None:
        material = vocab.require_issuable("shard.chaff")
        text = brief_mod.build_material_brief(material)
        self.assertIn("shard.chaff", text)
        self.assertIn("Never invent a tag", text)

    def test_a_substrate_brief_names_its_frame_and_grade(self) -> None:
        material = vocab.require_issuable("substrate.plant.fine")
        text = brief_mod.build_material_brief(material)
        self.assertIn("plant", text)
        self.assertIn("grade 3 of 4", text)


class EmitTests(unittest.TestCase):
    """`nameKey`/`iconKey` cross-checked against the REAL shipped corpus's own entries (read-only) —
    not merely against a rule this test invented independently of the data."""

    def test_derivation_matches_every_real_shipped_entry(self) -> None:
        doc = json.loads(REAL_MATERIALS_PATH.read_text(encoding="utf-8"))
        checked = 0
        for entry in doc["entries"]:
            runtime_id = entry.get("runtimeId")
            if not runtime_id:
                continue
            self.assertEqual(emit.derive_name_key(runtime_id), entry["nameKey"],
                             f"nameKey drifted for {runtime_id}")
            self.assertEqual(emit.derive_icon_key(runtime_id), entry["iconKey"],
                             f"iconKey drifted for {runtime_id}")
            checked += 1
        # 21 original rows + the 10 shard.{rung} rows materials-gen authored 2026-09-07
        # (material.022-031) to close the rarity-ladder gap.
        self.assertEqual(checked, 31, "the real corpus has 31 entries today; a changed count means "
                                      "this cross-check is running against fewer rows than it should")

    def test_next_seq_resumes_from_the_highest_existing_id(self) -> None:
        self.assertEqual(emit.next_seq([]), 1)
        self.assertEqual(emit.next_seq([{"id": "material.001"}, {"id": "material.021"}]), 22)
        # A hole in the middle never gets backfilled with a duplicate — highest, not count.
        self.assertEqual(emit.next_seq([{"id": "material.001"}, {"id": "material.099"}]), 100)

    def test_build_entry_omits_class_fields_the_id_does_not_carry(self) -> None:
        shard = vocab.require_issuable("shard.chaff")
        entry = emit.build_entry(shard, _clean_answer(), seq=22)
        self.assertNotIn("element", entry)
        self.assertNotIn("frame", entry)
        self.assertNotIn("grade", entry)
        self.assertEqual(entry["runtimeId"], "shard.chaff")
        self.assertEqual(entry["materialClass"], "shard")
        self.assertEqual(entry["id"], "material.022")


class RunPlanAndBatchTests(unittest.TestCase):
    """Harness integration: resume, reconcile, and the explicit overwrite path — the same discipline
    `run_ledger.py` itself was built to generalize from `setgen`."""

    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        tmp = Path(self._tmp.name)
        self.materials_path = tmp / "materials.json"
        self.ledger = RunLedger(tmp / "materials-gen.ledger.json")

    def _seed_corpus(self, entries: "list[dict]") -> None:
        self.materials_path.write_text(
            json.dumps({"schemaVersion": 1, "kind": "material", "entries": entries}), encoding="utf-8")

    def test_a_generated_entry_for_a_real_id_persists_and_is_schema_clean(self) -> None:
        self._seed_corpus([])
        plan = run_mod.plan_run(materials_path=self.materials_path, ledger=self.ledger)
        subject_ids = {s.subject_id for s in plan.subjects}
        self.assertIn("shard.chaff", subject_ids, "shard.chaff is real and missing — it must be planned")

        result = run_mod.run_batch(
            plan, {"shard.chaff": _clean_answer(name="Chaff Fragment")},
            materials_path=self.materials_path, ledger=self.ledger)
        self.assertEqual(result.persisted, ("shard.chaff",))
        doc = json.loads(self.materials_path.read_text(encoding="utf-8"))
        entry = next(e for e in doc["entries"] if e["runtimeId"] == "shard.chaff")
        self.assertEqual(entry["name"], "Chaff Fragment")
        self.assertEqual(entry["nameKey"], "material.shard-chaff")
        self.assertEqual(entry["iconKey"], "icon.material.shard-chaff")
        self.assertEqual(entry["materialClass"], "shard")
        self.assertEqual(entry["tags"], ["mineral"])
        self.assertTrue(self.ledger.path.exists())
        self.assertIn("shard.chaff", self.ledger.read_done())

    def test_resume_does_not_replan_an_already_generated_id(self) -> None:
        self._seed_corpus([])
        plan = run_mod.plan_run(materials_path=self.materials_path, ledger=self.ledger)
        run_mod.run_batch(plan, {"shard.chaff": _clean_answer()},
                          materials_path=self.materials_path, ledger=self.ledger)

        resumed = run_mod.plan_run(materials_path=self.materials_path, ledger=self.ledger)
        self.assertNotIn("shard.chaff", {s.subject_id for s in resumed.subjects})

    def test_reconcile_replans_a_hand_deleted_row(self) -> None:
        self._seed_corpus([])
        plan = run_mod.plan_run(materials_path=self.materials_path, ledger=self.ledger)
        run_mod.run_batch(plan, {"shard.chaff": _clean_answer()},
                          materials_path=self.materials_path, ledger=self.ledger)

        # Simulate an out-of-band hand edit that removes the generated row, but the ledger still
        # claims the subject done.
        doc = json.loads(self.materials_path.read_text(encoding="utf-8"))
        doc["entries"] = [e for e in doc["entries"] if e["runtimeId"] != "shard.chaff"]
        self.materials_path.write_text(json.dumps(doc), encoding="utf-8")

        reconciled = run_mod.plan_run(materials_path=self.materials_path, ledger=self.ledger)
        self.assertIn("shard.chaff", {s.subject_id for s in reconciled.subjects},
                      "a ledger-managed row missing from the corpus must be replanned, not skipped")

    def test_hand_authored_content_with_no_ledger_row_is_never_touched(self) -> None:
        """The 17 issuable ids the real corpus already carries hand-authored content for (essence,
        substrate, catalyst) must never be silently regenerated just because this generator now
        exists."""
        self._seed_corpus([{"id": "material.001", "nameKey": "material.essence-fire",
                            "name": "Hand-Authored Ember", "runtimeId": "essence.fire",
                            "materialClass": "essence", "element": "fire",
                            "iconKey": "icon.material.essence-fire", "tags": ["arcane"]}])
        plan = run_mod.plan_run(materials_path=self.materials_path, ledger=self.ledger)
        self.assertNotIn("essence.fire", {s.subject_id for s in plan.subjects})

    def test_overwrite_forces_regeneration_of_an_existing_hand_authored_row(self) -> None:
        self._seed_corpus([{"id": "material.001", "nameKey": "material.essence-fire",
                            "name": "Hand-Authored Ember", "runtimeId": "essence.fire",
                            "materialClass": "essence", "element": "fire",
                            "iconKey": "icon.material.essence-fire", "tags": ["arcane"]}])
        # Never planned by the ordinary resume path...
        ordinary = run_mod.plan_run(materials_path=self.materials_path, ledger=self.ledger)
        self.assertNotIn("essence.fire", {s.subject_id for s in ordinary.subjects})

        # ...but an explicit --overwrite reaches it, and replaces the SAME row (same `id`) in place.
        forced = run_mod.plan_overwrite(["essence.fire"], ledger=self.ledger)
        self.assertEqual([s.subject_id for s in forced.subjects], ["essence.fire"])
        result = run_mod.run_batch(
            forced, {"essence.fire": _clean_answer(name="Reforged Ember", tags=["arcane"])},
            materials_path=self.materials_path, ledger=self.ledger)
        self.assertEqual(result.persisted, ("essence.fire",))
        doc = json.loads(self.materials_path.read_text(encoding="utf-8"))
        self.assertEqual(len(doc["entries"]), 1, "overwrite replaces the row, it does not duplicate it")
        self.assertEqual(doc["entries"][0]["id"], "material.001")
        self.assertEqual(doc["entries"][0]["name"], "Reforged Ember")

    def test_overwrite_refuses_a_fabricated_id(self) -> None:
        with self.assertRaises(vocab.MaterialVocabularyRejection):
            run_mod.plan_overwrite(["shard.mythic"], ledger=self.ledger)

    def test_overwrite_refuses_an_empty_scope_rather_than_defaulting_to_all(self) -> None:
        with self.assertRaises(ValueError):
            run_mod.plan_overwrite([], ledger=self.ledger)

    def test_a_blocked_answer_persists_nothing(self) -> None:
        self._seed_corpus([])
        plan = run_mod.plan_run(materials_path=self.materials_path, ledger=self.ledger)
        result = run_mod.run_batch(plan, {"shard.chaff": {"blocked": "no motif lands cleanly"}},
                                   materials_path=self.materials_path, ledger=self.ledger)
        outcome = next(o for o in result.outcomes if o.subject_id == "shard.chaff")
        self.assertEqual(outcome.outcome, "blocked")
        self.assertEqual(result.persisted, ())
        self.assertFalse(self.materials_path.exists() and
                         json.loads(self.materials_path.read_text(encoding="utf-8"))["entries"])

    def test_a_missing_answer_is_reported_not_silently_skipped(self) -> None:
        self._seed_corpus([])
        plan = run_mod.plan_run(materials_path=self.materials_path, ledger=self.ledger)
        result = run_mod.run_batch(plan, {}, materials_path=self.materials_path, ledger=self.ledger)
        outcomes = {o.subject_id: o.outcome for o in result.outcomes}
        self.assertEqual(outcomes.get("shard.chaff"), "missing_answer")

    def test_a_bad_answer_is_refused_with_named_defects(self) -> None:
        self._seed_corpus([])
        plan = run_mod.plan_run(materials_path=self.materials_path, ledger=self.ledger)
        result = run_mod.run_batch(
            plan, {"shard.chaff": {"name": "x", "flavor": "too short a name above"}},
            materials_path=self.materials_path, ledger=self.ledger)
        outcome = next(o for o in result.outcomes if o.subject_id == "shard.chaff")
        self.assertEqual(outcome.outcome, "refused")
        self.assertTrue(any("below the minimum" in d for d in outcome.defects), outcome.defects)
        self.assertEqual(result.persisted, ())


if __name__ == "__main__":
    unittest.main()
