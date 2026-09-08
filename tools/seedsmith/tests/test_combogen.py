"""Tests for item module 21's `combination-write-unblock` unblock work
(docs/architecture/item-seedgen/spec-combination-write-unblock.md).

    python -m pytest tools/seedsmith/tests/test_combogen.py -v

⚠ **A pre-existing sibling file already covers the grid/tuning/schema/supply/migration/emit/brief/
run/CLI surface exhaustively**: `test_strain_splice_gen.py` (52 tests, all green except one
corpus-count assertion that drifted for reasons unrelated to this module — the live gem corpus grew
from 40 to 60 gems between when that test was written and now). Missing that file on the first
search (its name does not match `*combo*`) was this session's own investigation gap; it is corrected
here rather than duplicated. This file therefore covers ONLY what did not exist before this module's
work: the two-field regression in `schema.combination_schema` (nameKey removed from the model-facing
schema), `emit.assemble_entry` (new), `combogen.deps` (new — acceptance 3a's dependency_validator
wiring), and `combogen.authored` + `workflow/graphs/item_combination.py` (new — the generation graph
and the RunLedger-based resume/reconcile/overwrite harness this module was built to wire in).

- **Regression tests** (`SchemaTests`, `EmitTests`) — the two files this module edited beyond what
  `test_strain_splice_gen.py` already exercises.
- **Real-corpus tests** (`DepsTests`) — run `combogen.deps.preflight`/`validate_entries` against
  the actual shipped gems/base-types/atom corpora, proving acceptance 3a for real.
- **Harness-mechanics tests** (`AuthoredBatchTests`) — resume/reconcile/overwrite through
  `pipeline.run_ledger.RunLedger`, run against an isolated `tmp_path` ledger/out-dir over a REAL
  (sliced-to-two) plan, with the authored-answer transport standing in for a model — the same proof
  `test_sockets_gen.py`'s own `RunPlanTests` gives for `gemgen`.
"""
from __future__ import annotations

import dataclasses
import json
import sys
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.adapters.items.combogen import authored as authored_mod  # noqa: E402
from seedsmith.adapters.items.combogen import deps as deps_mod  # noqa: E402
from seedsmith.adapters.items.combogen import emit  # noqa: E402
from seedsmith.adapters.items.combogen import run as run_mod  # noqa: E402
from seedsmith.adapters.items.combogen import schema as schema_mod  # noqa: E402
from seedsmith.adapters.items.combogen import supply as supply_mod  # noqa: E402
from seedsmith.adapters.items.combogen import tuning as tuning_mod  # noqa: E402
from seedsmith.adapters.items.setgen.answers import AnswerFile  # noqa: E402
from seedsmith.pipeline.model import audit_schema  # noqa: E402
from seedsmith.pipeline.run_ledger import RunLedger  # noqa: E402
from seedsmith.report import cli as cli_mod  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parents[3]


def _real_tuning():
    return tuning_mod.load()


def _real_supply():
    return supply_mod.build()


# ------------------------------------------------------------------------------------------------
# schema.py — the net-new regression: `nameKey` is NOT a model-writable field (this module's own
# P1 fix — nameKey is PLANNED from the grid cell, never AUTHORED by the model). Everything else
# about this schema (audit_schema-clean, ingredient count, empty-universe refusal) is already
# covered by `test_strain_splice_gen.py::SchemaTests`.
# ------------------------------------------------------------------------------------------------
class SchemaTests(unittest.TestCase):
    def setUp(self) -> None:
        self.tuning = _real_tuning()
        self.supply = _real_supply()
        self.host_roles = self.tuning.host_roles()

    def _schema(self):
        granted = run_mod.granted_family_vocabulary(self.supply)
        return schema_mod.combination_schema(
            self.tuning, supplied_families=self.supply.families,
            host_roles=self.host_roles, granted_families=granted)

    def test_nameKey_is_not_offered_to_the_model(self) -> None:
        names = schema_mod.schema_field_names(self._schema())
        self.assertNotIn("nameKey", names)
        self.assertIn("name", names)
        self.assertIn("flavor", names)

    def test_the_schema_is_still_audit_schema_clean_after_the_nameKey_removal(self) -> None:
        defects = audit_schema(self._schema())
        self.assertEqual(defects, [], [str(d) for d in defects])


# ------------------------------------------------------------------------------------------------
# emit.py — the net-new `assemble_entry` (`ingredient_rows`/id-minting are already covered by
# `test_strain_splice_gen.py::EmitTests`/`GridTests`).
# ------------------------------------------------------------------------------------------------
class EmitTests(unittest.TestCase):
    def setUp(self) -> None:
        self.tuning = _real_tuning()

    def test_assemble_entry_shape_and_grantedTier_is_never_model_supplied(self) -> None:
        entry = emit.assemble_entry(
            entry_id="combo.strain-might-offense", name_key="combination.strain-might-offense",
            name="Test Name", flavor="A flavour string that is long enough.", shape="strain",
            aptitudes=("Might",), archetype="offense",
            ingredient_families=["atom.might", "atom.might", "atom.cruelty", "atom.ferocity"],
            grants=["atom.savagery"], tuning=self.tuning, host_role="armament-primary")
        self.assertEqual(entry["id"], "combo.strain-might-offense")
        self.assertEqual(entry["nameKey"], "combination.strain-might-offense")
        self.assertEqual(entry["minSockets"], self.tuning.ingredient_count)
        self.assertEqual(entry["grantedTier"], self.tuning.base_tier_for("strain"))
        self.assertNotIn("hostFrame", entry)  # omitted, never null
        self.assertEqual(sum(r["quantity"] for r in entry["ingredients"]),
                         self.tuning.ingredient_count)

    def test_assemble_entry_refuses_zero_grants(self) -> None:
        with self.assertRaises(emit.IdRefused):
            emit.assemble_entry(
                entry_id="combo.strain-might-offense", name_key="combination.strain-might-offense",
                name="X", flavor="A flavour string that is long enough.", shape="strain",
                aptitudes=("Might",), archetype="offense",
                ingredient_families=["atom.might"] * self.tuning.ingredient_count,
                grants=[], tuning=self.tuning)


# ------------------------------------------------------------------------------------------------
# deps.py — acceptance 3a, run against the REAL corpus
# ------------------------------------------------------------------------------------------------
class DepsTests(unittest.TestCase):
    def setUp(self) -> None:
        self.tuning = _real_tuning()
        self.supply = _real_supply()

    def test_preflight_over_the_real_corpus_is_not_refused(self) -> None:
        report = deps_mod.preflight(self.tuning, supply=self.supply)
        self.assertFalse(report.refused, report.to_dict())
        self.assertGreater(report.ingredient_families_checked, 0)
        self.assertGreater(report.host_roles_checked, 0)

    def test_preflight_is_refused_when_no_family_is_supplied(self) -> None:
        empty = dataclasses.replace(self.supply, families=(), bands={})
        report = deps_mod.preflight(self.tuning, supply=empty)
        self.assertTrue(report.refused)
        self.assertIn("no ingredient family is supplied by any live gem", report.to_dict()["reasons"][0])

    def test_validate_entries_resolves_a_real_generated_entry(self) -> None:
        entry = emit.assemble_entry(
            entry_id="combo.strain-might-offense", name_key="combination.strain-might-offense",
            name="Test", flavor="A flavour string that is long enough.", shape="strain",
            aptitudes=("Might",), archetype="offense",
            ingredient_families=["atom.might", "atom.might", "atom.cruelty", "atom.ferocity"],
            grants=["atom.savagery"], tuning=self.tuning, host_role="armament-primary")
        report = deps_mod.validate_entries(
            {entry["id"]: entry}, supply=self.supply, host_roles=self.tuning.host_roles())
        self.assertEqual(report.unresolved, [], [r.value for r in report.unresolved])

    def test_validate_entries_reports_an_unresolved_external_grant_without_backfilling(self) -> None:
        entry = emit.assemble_entry(
            entry_id="combo.strain-might-offense", name_key="combination.strain-might-offense",
            name="Test", flavor="A flavour string that is long enough.", shape="strain",
            aptitudes=("Might",), archetype="offense",
            ingredient_families=["atom.might", "atom.might", "atom.cruelty", "atom.ferocity"],
            grants=["atom.this-family-does-not-exist"], tuning=self.tuning,
            host_role="armament-primary")
        report = deps_mod.validate_entries(
            {entry["id"]: entry}, supply=self.supply, host_roles=self.tuning.host_roles())
        unresolved = report.unresolved
        self.assertEqual(len(unresolved), 1)
        self.assertEqual(unresolved[0].manifest.kind, "external")
        self.assertEqual(unresolved[0].value, "atom.this-family-does-not-exist")
        # 3b: EXTERNAL is never backfillable by this program.
        from seedsmith.pipeline.dependency_validator import plan_backfill
        self.assertEqual(plan_backfill(report), [])


# ------------------------------------------------------------------------------------------------
# authored.py — the generator-harness wiring: resume / reconcile / overwrite, zero live calls
# ------------------------------------------------------------------------------------------------
class AuthoredBatchTests(unittest.TestCase):
    def setUp(self) -> None:
        import tempfile

        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.out_dir = Path(self._tmp.name) / "out"
        self.out_dir.mkdir()
        self.ledger_path = self.out_dir / "combination-gen.ledger.json"

        self.tuning = _real_tuning()
        self.supply = _real_supply()
        full_plan = run_mod.plan_run(shape="strain", tuning=self.tuning, supply=self.supply)
        # Slice to the first two subjects — cheap, deterministic, and enough to exercise
        # resume/reconcile/overwrite without driving all 36.
        self.plan = dataclasses.replace(full_plan, subjects=full_plan.subjects[:2])
        self.family = sorted(self.supply.families)[0]
        self.grant_family = self._first_real_external_grant()

    def _first_real_external_grant(self) -> str:
        from seedsmith.adapters.items import registries

        real = registries.load_atom_families()
        supplied = set(self.supply.families)
        return sorted(supplied & real)[0]

    def _answers_for_plan(self) -> AnswerFile:
        by_subject = {}
        for subject in self.plan.subjects:
            by_subject[subject.subject_id] = ({
                "name": f"Test {subject.entry_id}",
                "flavor": "A flavour string that is long enough to pass the schema minimum.",
                "ingredients": [self.family] * self.tuning.ingredient_count,
                "grants": [self.grant_family],
            },)
        return AnswerFile(kind="combination", population="n/a", prompt_version="test/1",
                          by_subject=by_subject)

    def test_run_batch_persists_every_planned_subject(self) -> None:
        result = authored_mod.run_batch(
            plan=self.plan, answers=self._answers_for_plan(), tuning=self.tuning,
            out_dir=self.out_dir, authored_utc="1970-01-01T00:00:00Z", model="test/1",
            ledger_path=self.ledger_path)
        self.assertEqual(len(result.persisted), 2)
        self.assertEqual(len(result.outcomes), 2)
        self.assertTrue(result.file and result.file.exists())
        doc = json.loads(result.file.read_text(encoding="utf-8"))
        self.assertEqual(doc["kind"], "combination")
        self.assertEqual(len(doc["entries"]), 2)

    def test_resume_produces_no_new_work_the_second_time(self) -> None:
        authored_mod.run_batch(
            plan=self.plan, answers=self._answers_for_plan(), tuning=self.tuning,
            out_dir=self.out_dir, authored_utc="1970-01-01T00:00:00Z", model="test/1",
            ledger_path=self.ledger_path)
        ledger = RunLedger(self.ledger_path)
        needing = authored_mod.plan_needing_work(self.plan, ledger)
        self.assertEqual(needing, [])

        # A second `run_batch` with the SAME answers must not raise `AnswerExhausted` even though
        # nothing new is planned — its subject list is simply empty.
        result_again = authored_mod.run_batch(
            plan=self.plan, answers=self._answers_for_plan(), tuning=self.tuning,
            out_dir=self.out_dir, authored_utc="1970-01-01T00:00:00Z", model="test/1",
            ledger_path=self.ledger_path)
        self.assertEqual(result_again.outcomes, [])
        # The file still reflects both already-done entries — resume does not drop them.
        self.assertEqual(len(result_again.entries), 2)

    def test_reconcile_recovers_a_corrupted_ledger_row(self) -> None:
        authored_mod.run_batch(
            plan=self.plan, answers=self._answers_for_plan(), tuning=self.tuning,
            out_dir=self.out_dir, authored_utc="1970-01-01T00:00:00Z", model="test/1",
            ledger_path=self.ledger_path)
        doc = json.loads(self.ledger_path.read_text(encoding="utf-8"))
        first_key = self.plan.subjects[0].subject_id
        self.assertIn(first_key, doc["done"])
        doc["done"][first_key] = {"entryId": self.plan.subjects[0].entry_id}  # no "entry" -- corrupt
        self.ledger_path.write_text(json.dumps(doc), encoding="utf-8")

        ledger = RunLedger(self.ledger_path)
        needing = authored_mod.plan_needing_work(self.plan, ledger)
        self.assertEqual([s.subject_id for s in needing], [first_key])

    def test_overwrite_by_explicit_ids_only_touches_those_subjects(self) -> None:
        authored_mod.run_batch(
            plan=self.plan, answers=self._answers_for_plan(), tuning=self.tuning,
            out_dir=self.out_dir, authored_utc="1970-01-01T00:00:00Z", model="test/1",
            ledger_path=self.ledger_path)
        target_id = self.plan.subjects[0].subject_id
        result = authored_mod.run_batch(
            plan=self.plan, answers=self._answers_for_plan(), tuning=self.tuning,
            out_dir=self.out_dir, authored_utc="1970-01-01T00:00:00Z", model="test/1",
            ledger_path=self.ledger_path, overwrite=[target_id])
        self.assertEqual([o.subject_id for o in result.outcomes], [target_id])

    def test_a_typod_overwrite_scope_fails_loudly(self) -> None:
        with self.assertRaises(ValueError):
            authored_mod.run_batch(
                plan=self.plan, answers=self._answers_for_plan(), tuning=self.tuning,
                out_dir=self.out_dir, authored_utc="1970-01-01T00:00:00Z", model="test/1",
                ledger_path=self.ledger_path, overwrite="al")  # a typo of "all"

    def test_a_blocked_answer_writes_nothing_for_that_subject(self) -> None:
        answers = self._answers_for_plan()
        first_id = self.plan.subjects[0].subject_id
        answers.by_subject[first_id] = ({"blocked": "no fitting mechanism in this cell"},)
        result = authored_mod.run_batch(
            plan=self.plan, answers=answers, tuning=self.tuning, out_dir=self.out_dir,
            authored_utc="1970-01-01T00:00:00Z", model="test/1", ledger_path=self.ledger_path)
        blocked = [o for o in result.outcomes if o.subject_id == first_id]
        self.assertEqual(blocked[0].outcome, "blocked")
        self.assertEqual(len(result.persisted), 1)

    def test_authored_module_never_imports_the_live_model_caller(self) -> None:
        """Mirrors `setgen.answers`'s own "cannot reach the network" discipline: the replay
        transport this module defaults to imports nothing CALLABLE from `pipeline.llm_caller`, so
        a run driven by `run_batch` without an explicit `call=` cannot make a live request. Scoped
        to actual `import`/call syntax, not a bare substring match — the module's own docstring
        prose mentions `pipeline.llm_caller.call_model` by name when explaining what `call=`
        overrides, which a plain substring check would misread as a real import."""
        source = Path(authored_mod.__file__).read_text(encoding="utf-8")
        self.assertNotIn("import call_model", source)
        self.assertNotIn(" call_model(", source)
        self.assertNotIn("live_caller(", source)


class CombinationLiveTransportTests(unittest.TestCase):
    """The CLI must bind the existing injected graph seam to a live model caller."""

    def test_write_with_endpoint_uses_the_live_caller(self) -> None:
        import tempfile

        tuning = _real_tuning()
        supply = _real_supply()
        plan = run_mod.plan_run(shape="strain", tuning=tuning, supply=supply)
        plan = dataclasses.replace(plan, subjects=plan.subjects[:1])
        with tempfile.TemporaryDirectory() as temp:
            args = SimpleNamespace(
                out_dir=temp, answers="", endpoint="http://127.0.0.1:9876/v1/chat/completions",
                model="test-live-model", allow_production_tree=False, ledger="",
                authored_utc="1970-01-01T00:00:00Z")
            result = SimpleNamespace(persisted=[object()], outcomes=[], to_dict=lambda: {})
            with patch("seedsmith.adapters.items.combogen.authored.run_batch", return_value=result) as batch:
                done = cli_mod._cmd_items_combination_write(args, plan=plan, tuning=tuning)

        self.assertEqual(0, done)
        kwargs = batch.call_args.kwargs
        self.assertEqual("test-live-model", kwargs["model"])
        self.assertIsNotNone(kwargs["call"])


if __name__ == "__main__":
    unittest.main()
