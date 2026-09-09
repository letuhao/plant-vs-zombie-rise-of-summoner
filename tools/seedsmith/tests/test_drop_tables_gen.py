"""Tests for `drop-tables-gen` (item-seedgen Phase 5, last, alone,
docs/architecture/item-seedgen/spec-drop-tables-gen.md).

Groups, mirroring `test_base_types_gen.py`'s own split:
1. Tuning / real-corpus readers -- the four (+ one bonus) reference-manifest resolvers.
2. Schema / brief -- the closed answer schema and the deterministic row-slot plan.
3. Emit -- id minting, slugging, and the assembled entry shape vs. the real shipped corpus.
4. Harness integration -- `RunLedger` resume/reconcile/overwrite via `run.py`.
5. Real-corpus tests -- run against the actual, committed `data/seed/items/drop-tables/*.json`,
   including this module's own generated sample content (`droptable.d1-011..013`) and a REAL,
   pre-existing finding this module's own reference resolvers surface: 8 `frame: "hybrid"` rows in
   the shipped d1/d2/d3 corpus that base-types-gen never authors against (it only ever ships
   `humanoid`/`plant`) -- a known, pre-existing gap this module does not "fix" (base-types-gen owns
   the frame vocabulary), and a REAL demonstration that acceptance criterion 2 ("`items validate
   --deps` catches a dangling reference before import") has something real to catch.
"""
from __future__ import annotations

import json
from pathlib import Path

import pytest

from seedsmith.adapters.items.droptablegen import brief as brief_mod
from seedsmith.adapters.items.droptablegen import emit as emit_mod
from seedsmith.adapters.items.droptablegen import run as run_mod
from seedsmith.adapters.items.droptablegen import schema as schema_mod
from seedsmith.adapters.items.droptablegen import tuning as tuning_mod
from seedsmith.pipeline import dependency_validator as dv
from seedsmith.pipeline.model import audit_schema
from seedsmith.pipeline.run_ledger import RunLedger

REAL_CORPUS_DIR = Path(__file__).resolve().parents[3] / "data" / "seed" / "items" / "drop-tables"

#: The three real tables this module generated and committed in this same session (see the module's
#: own report): d1-011/012 are the original 5-row-shape sample, d1-013 is the fuller 7-row shape
#: that exercises consumable + insert rows too.
GENERATED_TABLE_IDS = ("droptable.d1-011", "droptable.d1-012", "droptable.d1-013")


def _load_real_d1() -> dict:
    return json.loads((REAL_CORPUS_DIR / "d1.json").read_text(encoding="utf-8"))


def _rows(entry: dict):
    for g in entry["groups"]:
        for row in g["entries"]:
            yield row


# ------------------------------------------------------------------------------------------------
# 1. Tuning / real-corpus readers
# ------------------------------------------------------------------------------------------------
class TestTuning:
    def test_material_runtime_ids_resolve_on_runtimeid_not_id(self):
        """The real finding this module's docstring states: a drop-table material `.ref` (e.g.
        "essence.earth") never appears as any material's `id` (those are `material.NNN`) -- it is
        always the `runtimeId`."""
        ids = tuning_mod.load_material_runtime_ids()
        assert "essence.earth" in ids
        assert "material.004" not in ids  # the PK, never what a drop-table row names

    def test_material_pool_now_carries_the_ten_rarity_named_shards(self):
        """A concurrent sibling session's materials-content-authoring work landed mid-session
        (materials.json grew from 21 to 31 entries, adding shard.chaff..shard.almanac, one per
        rarity rung) -- this module reads the corpus FRESH every call, so it picked the new rows up
        automatically with no code change, proving the "read fresh, never cache" discipline pays
        off across a genuinely concurrent edit."""
        ids = tuning_mod.load_material_runtime_ids()
        assert "shard.sprout" in ids
        assert "shard.almanac" in ids

    def test_consumable_and_gem_ids_are_read_fresh(self):
        cons = tuning_mod.load_consumable_ids()
        gems = tuning_mod.load_gem_ids()
        assert "consumable.k1-001" in cons
        assert "gem.g1-001" in gems
        assert len(cons) >= 60
        assert len(gems) >= 60

    def test_curve_019_is_the_real_material_yield_curve(self):
        curves = tuning_mod.load_curve_ids()
        assert tuning_mod.MATERIAL_QTY_CURVE_ID in curves

    def test_drop_band_enum_matches_bands_registry_exactly(self):
        assert tuning_mod.load_drop_band_enum() == (
            "staple", "frequent", "occasional", "seldom", "exceptional")

    def test_rarity_ids_match_core_registry_ladder_order(self):
        ids = tuning_mod.load_rarity_ids()
        assert ids[0] == "chaff"
        assert ids[-1] == "almanac"
        assert len(ids) == 10

    def test_legal_role_frame_pairs_excludes_hybrid(self):
        """base-types-gen only ever authors `humanoid`/`plant` (confirmed: zero `hybrid-*.json`
        files under data/seed/items/base-types/) -- a `hybrid` frame never appears in the legal
        pair set this module resolves equipment rows against, matching a real, pre-existing gap in
        the shipped drop-table corpus (see `TestRealCorpusFindings` below)."""
        pairs = tuning_mod.legal_role_frame_pairs()
        assert all(frame in ("humanoid", "plant") for _role, frame in pairs)
        assert ("core-guard", "hybrid") not in pairs

    def test_legal_role_frame_pairs_include_the_commander_only_standard_role(self):
        """Unlike `base-types-gen` itself (which deliberately excludes `standard` from its own
        30-role-frame generation scope), the real corpus DOES ship `humanoid-standard.json` and
        `plant-standard.json` -- and the shipped drop-table corpus references `role: "standard"`
        (droptable.d1-005 among others). This module's categorical resolver reads what actually
        exists on disk, not the 30-role-frame POLICY, so `standard` legitimately resolves here."""
        pairs = tuning_mod.legal_role_frame_pairs()
        assert ("standard", "humanoid") in pairs
        assert ("standard", "plant") in pairs


# ------------------------------------------------------------------------------------------------
# 2. Schema / brief
# ------------------------------------------------------------------------------------------------
class TestSchemaBrief:
    def test_bare_numeric_field_in_the_schema_fails_the_shared_auditor(self):
        schema = {"type": "object", "additionalProperties": False,
                  "properties": {"weight": {"type": "integer"}}}
        assert audit_schema(schema), "a bare integer field must be flagged"

    def test_answer_schema_never_offers_a_role_frame_ref_or_weight(self):
        schema = schema_mod.drop_table_answer_schema(
            row_count=5, drop_band_enum=("staple", "frequent"), rarity_ids=("chaff",),
            offer_rarity_floor=True)
        assert audit_schema(schema) == []
        fields = set(schema["properties"])
        for forbidden in ("role", "frame", "ref", "weight", "qtyCurve", "minCount", "maxCount"):
            assert forbidden not in fields

    def test_validate_answer_accepts_a_well_formed_answer(self):
        errors = schema_mod.validate_answer(
            {"name": "Test Cache", "rowBands": ["staple"] * 5},
            row_count=5, drop_band_enum=tuning_mod.load_drop_band_enum(),
            rarity_ids=tuning_mod.load_rarity_ids(), offer_rarity_floor=True)
        assert errors == []

    def test_validate_answer_rejects_wrong_row_count(self):
        errors = schema_mod.validate_answer(
            {"name": "X", "rowBands": ["staple"]},
            row_count=5, drop_band_enum=tuning_mod.load_drop_band_enum(),
            rarity_ids=tuning_mod.load_rarity_ids(), offer_rarity_floor=True)
        assert errors

    def test_validate_answer_rejects_an_illegal_band(self):
        errors = schema_mod.validate_answer(
            {"name": "X", "rowBands": ["mythic"] * 5},
            row_count=5, drop_band_enum=tuning_mod.load_drop_band_enum(),
            rarity_ids=tuning_mod.load_rarity_ids(), offer_rarity_floor=True)
        assert errors

    def test_validate_answer_blocked_short_circuits(self):
        errors = schema_mod.validate_answer(
            {"blocked": "no theme fits"}, row_count=5,
            drop_band_enum=tuning_mod.load_drop_band_enum(), rarity_ids=(), offer_rarity_floor=True)
        assert errors == []

    def test_build_row_slot_plan_is_deterministic_for_a_given_index(self):
        p1 = brief_mod.build_row_slot_plan(0)
        p2 = brief_mod.build_row_slot_plan(0)
        assert p1.equipment_slots == p2.equipment_slots
        assert p1.material_refs == p2.material_refs
        assert p1.consumable_refs == p2.consumable_refs
        assert p1.gem_refs == p2.gem_refs

    def test_build_row_slot_plan_only_ever_offers_resolvable_role_frame_pairs(self):
        legal = tuning_mod.legal_role_frame_pairs()
        for i in range(10):
            plan = brief_mod.build_row_slot_plan(i)
            for pair in plan.equipment_slots:
                assert pair in legal

    def test_row_slot_plan_row_count_matches_schema_row_count(self):
        plan = brief_mod.build_row_slot_plan(3)
        brief = brief_mod.DropTableBrief(plan=plan)
        assert brief.schema["properties"]["rowBands"]["minItems"] == plan.row_count

    def test_brief_never_offers_a_role_or_material_choice_in_text(self):
        """The brief TELLS the model which role/frame/material rows are fixed (legitimately
        containing those words) but never frames them as a CHOICE -- mirrors
        `test_sockets_gen.py`'s own `test_build_gem_brief_never_offers_a_number_or_a_tag`."""
        plan = brief_mod.build_row_slot_plan(0)
        text = brief_mod.DropTableBrief(plan=plan).render()
        assert "`role`" not in text
        assert "invent a role" in text  # explicitly told NOT to invent one


# ------------------------------------------------------------------------------------------------
# 3. Emit
# ------------------------------------------------------------------------------------------------
class TestEmit:
    def test_mint_table_id_matches_the_real_naming_v1_template(self):
        assert emit_mod.mint_table_id(1, 11) == "droptable.d1-011"
        assert emit_mod.mint_table_id(4, 7) == "droptable.d4-007"

    def test_mint_table_id_refuses_a_non_frozen_slot(self):
        with pytest.raises(emit_mod.MintRefused):
            emit_mod.mint_table_id(5, 1)  # partitionCount is frozen at 4 -- see tuning.py

    def test_next_seq_continues_past_the_highest_existing_for_that_slot(self):
        assert emit_mod.next_seq((), 1) == 1
        assert emit_mod.next_seq(["droptable.d1-001", "droptable.d1-010"], 1) == 11
        # a different slot's ids never influence this slot's own counter
        assert emit_mod.next_seq(["droptable.d2-099"], 1) == 1

    def test_assemble_entry_matches_the_real_shipped_shape(self):
        plan = brief_mod.build_row_slot_plan(0)
        entry = emit_mod.assemble_entry(
            table_id="droptable.d1-900", name="Test Table",
            drop_bands=["staple"] * plan.row_count,
            equipment_slots=list(plan.equipment_slots), material_refs=list(plan.material_refs),
            consumable_refs=list(plan.consumable_refs), gem_refs=list(plan.gem_refs),
            rarity_floor=None, legal_role_frames=plan.legal_role_frames,
            legal_material_refs=plan.legal_material_refs,
            legal_consumable_refs=plan.legal_consumable_refs, legal_gem_refs=plan.legal_gem_refs,
            legal_drop_bands=plan.drop_band_enum, legal_rarity_ids=plan.rarity_ids)
        real = _load_real_d1()["entries"][0]
        assert set(entry.keys()) == set(real.keys())
        assert entry["sourceAllow"] == ["web"]
        for row in _rows(entry):
            assert "dropBand" in row
            assert "weight" not in row and "minCount" not in row and "maxCount" not in row

    def test_assemble_entry_refuses_an_illegal_role_frame_pair(self):
        plan = brief_mod.build_row_slot_plan(0)
        with pytest.raises(emit_mod.IllegalChoiceError):
            emit_mod.assemble_entry(
                table_id="droptable.d1-900", name="Bad", drop_bands=["staple"] * plan.row_count,
                equipment_slots=[("core-guard", "hybrid"), plan.equipment_slots[1]],
                material_refs=list(plan.material_refs), consumable_refs=list(plan.consumable_refs),
                gem_refs=list(plan.gem_refs), rarity_floor=None,
                legal_role_frames=plan.legal_role_frames,
                legal_material_refs=plan.legal_material_refs,
                legal_consumable_refs=plan.legal_consumable_refs,
                legal_gem_refs=plan.legal_gem_refs, legal_drop_bands=plan.drop_band_enum,
                legal_rarity_ids=plan.rarity_ids)

    def test_assemble_entry_refuses_an_unresolvable_material_ref(self):
        plan = brief_mod.build_row_slot_plan(0)
        with pytest.raises(emit_mod.IllegalChoiceError):
            emit_mod.assemble_entry(
                table_id="droptable.d1-900", name="Bad", drop_bands=["staple"] * plan.row_count,
                equipment_slots=list(plan.equipment_slots),
                material_refs=["not-a-real-material", plan.material_refs[1]],
                consumable_refs=list(plan.consumable_refs), gem_refs=list(plan.gem_refs),
                rarity_floor=None, legal_role_frames=plan.legal_role_frames,
                legal_material_refs=plan.legal_material_refs,
                legal_consumable_refs=plan.legal_consumable_refs,
                legal_gem_refs=plan.legal_gem_refs, legal_drop_bands=plan.drop_band_enum,
                legal_rarity_ids=plan.rarity_ids)

    def test_assemble_entry_qty_curve_is_always_the_one_real_curve(self):
        plan = brief_mod.build_row_slot_plan(0)
        entry = emit_mod.assemble_entry(
            table_id="droptable.d1-900", name="Test Table", drop_bands=["staple"] * plan.row_count,
            equipment_slots=list(plan.equipment_slots), material_refs=list(plan.material_refs),
            consumable_refs=list(plan.consumable_refs), gem_refs=list(plan.gem_refs),
            rarity_floor=None, legal_role_frames=plan.legal_role_frames,
            legal_material_refs=plan.legal_material_refs,
            legal_consumable_refs=plan.legal_consumable_refs, legal_gem_refs=plan.legal_gem_refs,
            legal_drop_bands=plan.drop_band_enum, legal_rarity_ids=plan.rarity_ids)
        mat_rows = [r for r in _rows(entry) if r["entryKind"] == "material"]
        assert mat_rows and all(r["qtyCurve"] == "curve.019" for r in mat_rows)

    def test_pipeline_dependency_validator_resolves_the_material_hard_reference(self):
        """Exercises the SHARED `dependency_validator` (module 1, `generator-harness`) directly,
        rather than only this module's own hand-rolled checks above.

        Real limitation found while wiring this up: `dependency_validator`'s dotted `field_path`
        extractor has no way to disambiguate entryKind (a row's `.ref` means a different target
        corpus depending on its OWN sibling `entryKind` field, and the extractor sees only the one
        path it is given) or to combine two sibling fields (`role`+`frame`) into one categorical
        key. So this module resolves those checks itself (`TestRealCorpusReferenceManifest` above)
        rather than forcing an ill-fitting generic path through the shared validator. Here, the
        shared validator IS exercised directly on a pre-filtered view (material-kind rows only,
        keyed by table id) -- the one shape it fits without modification."""
        plan = brief_mod.build_row_slot_plan(0)
        entry = emit_mod.assemble_entry(
            table_id="droptable.d1-900", name="Test Table", drop_bands=["staple"] * plan.row_count,
            equipment_slots=list(plan.equipment_slots), material_refs=list(plan.material_refs),
            consumable_refs=list(plan.consumable_refs), gem_refs=list(plan.gem_refs),
            rarity_floor=None, legal_role_frames=plan.legal_role_frames,
            legal_material_refs=plan.legal_material_refs,
            legal_consumable_refs=plan.legal_consumable_refs, legal_gem_refs=plan.legal_gem_refs,
            legal_drop_bands=plan.drop_band_enum, legal_rarity_ids=plan.rarity_ids)
        material_rows = {f"{entry['id']}#{i}": row for i, row in enumerate(_rows(entry))
                        if row["entryKind"] == "material"}
        manifest = [dv.ReferenceManifestEntry("ref", dv.HARD, "materials-gen")]
        material_ids = tuning_mod.load_material_runtime_ids()
        report = dv.validate(material_rows, manifest,
                            resolve_hard=lambda _mod, value: value in material_ids,
                            resolve_categorical=lambda _mod, _value: 0)
        assert report.unresolved == []
        assert len(report.results) == len(material_rows)


# ------------------------------------------------------------------------------------------------
# 4. Harness integration -- RunLedger resume / reconcile / overwrite
# ------------------------------------------------------------------------------------------------
def _fake_call(name: str = "Test Table", *, block: bool = False):
    def call(brief: str, schema: dict) -> dict:
        if block:
            return {"blocked": "no theme fits"}
        n = schema["properties"]["rowBands"]["minItems"]
        return {"name": name, "rowBands": ["staple"] * n}
    return call


class TestHarness:
    def test_plan_run_assigns_sequential_table_ids_starting_after_existing(self, tmp_path):
        ledger = RunLedger(tmp_path / "ledger.json")
        dt_dir = tmp_path / "drop-tables"
        plan = run_mod.plan_run(slot=2, count=2, ledger=ledger, drop_tables_dir=dt_dir)
        assert [s.seq for s in plan.subjects] == [1, 2]
        assert [s.subject_id for s in plan.subjects] == [
            "droptable-draw-d2-000", "droptable-draw-d2-001"]

    def test_plan_run_refuses_a_non_frozen_slot(self, tmp_path):
        ledger = RunLedger(tmp_path / "ledger.json")
        with pytest.raises(ValueError):
            run_mod.plan_run(slot=9, count=1, ledger=ledger, drop_tables_dir=tmp_path / "dt")

    def test_run_draws_marks_ledger_done_and_separates_blocked(self, tmp_path):
        ledger = RunLedger(tmp_path / "ledger.json")
        dt_dir = tmp_path / "drop-tables"
        plan = run_mod.plan_run(slot=2, count=2, ledger=ledger, drop_tables_dir=dt_dir)
        calls = iter([_fake_call(name="First"), _fake_call(block=True)])
        fresh, blocked = run_mod.run_draws(plan, ledger=ledger, call=lambda b, s: next(calls)(b, s))
        assert len(fresh) == 1
        (entry,) = fresh.values()
        assert entry["id"] == "droptable.d2-001"
        assert len(blocked) == 1
        done = ledger.read_done()
        assert set(done) == {"droptable-draw-d2-000", "droptable-draw-d2-001"}
        assert done["droptable-draw-d2-001"]["outcome"] == "blocked"

    def test_run_draws_does_not_checkpoint_before_persist_callback(self, tmp_path):
        ledger = RunLedger(tmp_path / "ledger.json")
        dt_dir = tmp_path / "drop-tables"
        plan = run_mod.plan_run(slot=2, count=1, ledger=ledger, drop_tables_dir=dt_dir)

        def persist(_entry):
            raise RuntimeError("simulated corpus write failure")

        with pytest.raises(RuntimeError, match="corpus write failure"):
            run_mod.run_draws(plan, ledger=ledger, call=_fake_call(name="Retry Me"), persist=persist)
        assert ledger.read_done() == {}

    def test_run_draws_continues_after_an_invalid_model_answer(self, tmp_path):
        ledger = RunLedger(tmp_path / "ledger.json")
        dt_dir = tmp_path / "drop-tables"
        plan = run_mod.plan_run(slot=2, count=2, ledger=ledger, drop_tables_dir=dt_dir)
        calls = iter([
            lambda _brief, _schema: {"name": "Bad", "rowBands": []},
            _fake_call(name="Recovered"),
        ])
        fresh, blocked = run_mod.run_draws(plan, ledger=ledger, call=lambda b, s: next(calls)(b, s))
        assert set(blocked) == {"droptable-draw-d2-000"}
        assert {entry["name"] for entry in fresh.values()} == {"Recovered"}
        assert set(ledger.read_done()) == {"droptable-draw-d2-000", "droptable-draw-d2-001"}
        assert ledger.read_done()["droptable-draw-d2-000"]["outcome"] == "escalated"

    def test_resume_never_repeats_a_committed_draw(self, tmp_path):
        ledger = RunLedger(tmp_path / "ledger.json")
        dt_dir = tmp_path / "drop-tables"
        plan1 = run_mod.plan_run(slot=3, count=1, ledger=ledger, drop_tables_dir=dt_dir)
        fresh1, _ = run_mod.run_draws(plan1, ledger=ledger, call=_fake_call(name="One"))
        run_mod.write_corpus(3, fresh1, existing=plan1.existing, drop_tables_dir=dt_dir)

        plan2 = run_mod.plan_run(slot=3, count=1, ledger=ledger, drop_tables_dir=dt_dir)
        assert plan2.subjects[0].subject_id == "droptable-draw-d3-001"
        assert plan2.subjects[0].seq == 2, "the entry sequence continues past the one already written"

    def test_reconcile_resurfaces_a_draw_whose_table_was_deleted(self):
        ledger_entry = {"entryId": "droptable.d1-999", "name": "Ghost Table"}
        assert run_mod.is_valid("x", ledger_entry, existing={}) is False

    def test_plan_run_reuses_an_invalid_ledger_slot_before_allocating_new_draws(self, tmp_path):
        ledger = RunLedger(tmp_path / "ledger.json")
        ledger.mark_done("droptable-draw-d2-000", {
            "entryId": "droptable.d2-999", "name": "Ghost Table",
        })
        plan = run_mod.plan_run(slot=2, count=1, ledger=ledger, drop_tables_dir=tmp_path / "dt")
        assert plan.subjects[0].subject_id == "droptable-draw-d2-000"

    def test_reconcile_leaves_a_draw_alone_when_it_still_matches(self):
        ledger_entry = {"entryId": "droptable.d1-999", "name": "Ghost Table"}
        existing = {"droptable.d1-999": {"name": "Ghost Table"}}
        assert run_mod.is_valid("x", ledger_entry, existing=existing) is True

    def test_reconcile_resurfaces_a_draw_whose_name_was_hand_edited(self):
        ledger_entry = {"entryId": "droptable.d1-999", "name": "Ghost Table"}
        existing = {"droptable.d1-999": {"name": "Renamed Table"}}
        assert run_mod.is_valid("x", ledger_entry, existing=existing) is False

    def test_write_corpus_is_additive_and_never_drops_an_existing_entry(self, tmp_path):
        dt_dir = tmp_path / "drop-tables"
        ledger = RunLedger(tmp_path / "ledger.json")
        plan1 = run_mod.plan_run(slot=1, count=1, ledger=ledger, drop_tables_dir=dt_dir)
        fresh1, _ = run_mod.run_draws(plan1, ledger=ledger, call=_fake_call(name="First"))
        path1 = run_mod.write_corpus(1, fresh1, existing=plan1.existing, drop_tables_dir=dt_dir)

        ledger2 = RunLedger(tmp_path / "ledger.json")
        plan2 = run_mod.plan_run(slot=1, count=1, ledger=ledger2, drop_tables_dir=dt_dir)
        fresh2, _ = run_mod.run_draws(plan2, ledger=ledger2, call=_fake_call(name="Second"))
        path2 = run_mod.write_corpus(1, fresh2, existing=plan2.existing, drop_tables_dir=dt_dir)

        assert path1 == path2
        written = json.loads(path2.read_text(encoding="utf-8"))
        ids = {e["id"] for e in written["entries"]}
        assert ids == {"droptable.d1-001", "droptable.d1-002"}

    def test_overwrite_all_requires_the_literal_string(self, tmp_path):
        ledger = RunLedger(tmp_path / "ledger.json")
        ledger.mark_done("droptable-draw-d1-000", {"entryId": "droptable.d1-001", "name": "X"})
        with pytest.raises(ValueError):
            ledger.force(["droptable-draw-d1-000"], "al")  # typo of "all"
        forced = ledger.force(["droptable-draw-d1-000"], "ids")
        assert forced == ["droptable-draw-d1-000"]

    def test_cli_dry_run_prints_a_sample_brief_and_makes_no_model_call(self, capsys, monkeypatch, tmp_path):
        monkeypatch.setattr(run_mod, "DEFAULT_LEDGER_PATH", tmp_path / "ledger.json")
        exit_code = run_mod.main(["--slot", "1", "--dry-run", "--count", "1"])
        assert exit_code == 0
        out = capsys.readouterr().out
        assert "Author ONE NEW drop table" in out

    def test_cli_refuses_a_real_run_with_no_model_call_wired(self, monkeypatch, tmp_path):
        """⛔ Renamed in spirit 2026-09-08, unchanged in assertion — see basetypegen's identical
        test for the full account: this now refuses for lack of `--write`, not lack of wiring."""
        monkeypatch.setattr(run_mod, "DEFAULT_LEDGER_PATH", tmp_path / "ledger.json")
        with pytest.raises(SystemExit):
            run_mod.main(["--slot", "1", "--count", "1"])

    def test_cli_write_without_endpoint_refuses(self, monkeypatch, tmp_path):
        """Refuse only when the *resolved* transport has no endpoint."""
        from seedsmith.pipeline.llm_caller import LlmCallerConfig
        monkeypatch.setattr(run_mod, "DEFAULT_LEDGER_PATH", tmp_path / "ledger.json")
        monkeypatch.setattr(
            "seedsmith.pipeline.llm_caller.resolve_live_transport",
            lambda *a, **k: LlmCallerConfig(endpoint="", model="x"))
        with pytest.raises(SystemExit):
            run_mod.main(["--slot", "1", "--count", "1", "--write"])

    def test_cli_a_real_live_run_writes_a_real_partition_file(self, monkeypatch, tmp_path, capsys):
        """⛔ Real gap, closed 2026-09-08 — see basetypegen's identical test for the full account:
        `--write --endpoint <url>` was unreachable before this."""
        monkeypatch.setattr(run_mod, "DEFAULT_LEDGER_PATH", tmp_path / "ledger.json")
        monkeypatch.setattr(tuning_mod, "DROP_TABLES_DIR", tmp_path / "drop-tables")

        called_with = {}

        def _fake_live_answer_caller(config):
            called_with["config"] = config
            return _fake_call(name="Live-Wired Table")

        monkeypatch.setattr("seedsmith.pipeline.llm_caller.live_answer_caller",
                            _fake_live_answer_caller)

        exit_code = run_mod.main(["--slot", "1", "--count", "1", "--write",
                                 "--endpoint", "http://unit-test-endpoint",
                                 "--model", "unit-test-model"])

        assert exit_code == 0
        assert called_with["config"].endpoint == "http://unit-test-endpoint"
        assert called_with["config"].model == "unit-test-model"
        written = json.loads((tmp_path / "drop-tables" / "d1.json").read_text(encoding="utf-8"))
        names = {e["name"] for e in written["entries"]}
        assert "Live-Wired Table" in names
        summary = json.loads(capsys.readouterr().out)
        assert summary == {"planned": 1, "fresh": 1, "blocked": 0, "blockedReasons": {}}

    def test_cli_force_is_an_accepted_alias_for_overwrite(self, tmp_path, monkeypatch, capsys):
        """seedsmith-content-standard Task 6: `--force` is `content-completeness-core`'s own naming
        convention (`RunLedger.force()`, `generate_commander_effects.py --force`); this CLI's real,
        already-shipped flag is `--overwrite` (spec-drop-tables-gen.md). Added as a second flag
        string on the same argument rather than a rename, so nothing already typing `--overwrite`
        breaks."""
        ledger_path = tmp_path / "ledger.json"
        monkeypatch.setattr(run_mod, "DEFAULT_LEDGER_PATH", ledger_path)
        ledger = RunLedger(ledger_path)
        ledger.mark_done("droptable-draw-d1-000", {"entryId": "droptable.d1-001", "name": "X"})

        exit_code = run_mod.main(["--slot", "1", "--force", "droptable-draw-d1-000"])
        assert exit_code == 0
        assert "droptable-draw-d1-000" in capsys.readouterr().out


# ------------------------------------------------------------------------------------------------
# 5. Real corpus -- the four reference-manifest rows, one assertion each, against the REAL corpus
# ------------------------------------------------------------------------------------------------
class TestRealCorpusReferenceManifest:
    """Four SEPARATE assertions matching the four rows in spec-drop-tables-gen.md's own reference
    manifest -- never one generic "references resolve" check, per that spec's own testing
    strategy. Run against `droptable.d1-011/012/013`, the real sample content this module
    generated and committed, PLUS a documented pre-existing gap the same resolvers surface in the
    rest of the corpus (the 8 `hybrid`-frame rows)."""

    @staticmethod
    def _generated_entries():
        doc = _load_real_d1()
        return {e["id"]: e for e in doc["entries"] if e["id"] in GENERATED_TABLE_IDS}

    def test_manifest_row_1_role_frame_categorical_resolves_for_generated_tables(self):
        legal = tuning_mod.legal_role_frame_pairs()
        checked = 0
        for entry in self._generated_entries().values():
            for row in _rows(entry):
                if row["entryKind"] == "equipment":
                    assert (row["role"], row["frame"]) in legal, entry["id"]
                    checked += 1
        assert checked >= 6  # 2 equipment rows x 3 generated tables

    def test_manifest_row_2_material_ref_hard_resolves_on_runtimeid(self):
        material_ids = tuning_mod.load_material_runtime_ids()
        checked = 0
        for entry in self._generated_entries().values():
            for row in _rows(entry):
                if row["entryKind"] == "material":
                    assert row["ref"] in material_ids, entry["id"]
                    checked += 1
        assert checked >= 6

    def test_manifest_row_3_consumable_ref_hard_resolves(self):
        consumable_ids = tuning_mod.load_consumable_ids()
        checked = 0
        for entry in self._generated_entries().values():
            for row in _rows(entry):
                if row["entryKind"] == "consumable":
                    assert row["ref"] in consumable_ids, entry["id"]
                    checked += 1
        assert checked >= 1  # droptable.d1-013

    def test_manifest_row_4_gem_insert_ref_hard_resolves(self):
        gem_ids = tuning_mod.load_gem_ids()
        checked = 0
        for entry in self._generated_entries().values():
            for row in _rows(entry):
                if row["entryKind"] == "insert":
                    assert row["ref"] in gem_ids, entry["id"]
                    checked += 1
        assert checked >= 1  # droptable.d1-013

    def test_bonus_qty_curve_hard_reference_resolves(self):
        """Not one of the spec's own four rows -- a real fifth reference this module found while
        reading the corpus (`qtyCurve` -> `curves.json`). Checked as a courtesy, separately from
        the four required assertions above."""
        curve_ids = tuning_mod.load_curve_ids()
        for entry in self._generated_entries().values():
            for row in _rows(entry):
                if row["entryKind"] == "material":
                    assert row["qtyCurve"] in curve_ids, entry["id"]


class TestRealCorpusFindings:
    """Findings surfaced by this module's own resolvers against the PRE-EXISTING corpus (not this
    module's generated content) -- recorded as tests so a future fix is provable, per this repo's
    own "an assumed constraint... is the same defect as a wrong line of code" discipline."""

    def test_generated_tables_exist_and_are_well_formed(self):
        doc = _load_real_d1()
        ids = {e["id"] for e in doc["entries"]}
        for gid in GENERATED_TABLE_IDS:
            assert gid in ids, f"{gid} missing from the real d1.json -- generation did not land"

    def test_hybrid_frame_rows_are_a_known_pre_existing_dangling_categorical_reference(self):
        """Real finding (2026-09-07): 8 `frame: "hybrid"` equipment rows exist across the shipped
        d1/d2/d3 corpus (droptable.d1-010, d2-003, d2-006, d2-007, d2-009 x2, d3-007), and NONE of
        them resolve against base-types-gen's real corpus, because base-types-gen only ever
        authors `humanoid`/`plant` (zero `hybrid-*.json` files exist). This is base-types-gen's
        frame vocabulary to extend, not this module's to invent around -- recorded here so
        `items validate --deps` has a real, reproducible defect to catch, and so this test FLIPS
        the day base-types-gen (or a frame-vocabulary decision) adds a `hybrid` frame."""
        legal = tuning_mod.legal_role_frame_pairs()
        hybrid_rows = []
        for slot_entries in tuning_mod.entries_by_partition().values():
            for entry in slot_entries:
                for g in entry.get("groups", []):
                    for row in g.get("entries", []):
                        if row.get("entryKind") == "equipment" and row.get("frame") == "hybrid":
                            hybrid_rows.append((entry["id"], row["role"], row["frame"]))
        assert len(hybrid_rows) >= 8, "expected the known 8 hybrid-frame rows, found fewer"
        for _table_id, role, frame in hybrid_rows:
            assert (role, frame) not in legal, (
                "a hybrid-frame pair now resolves -- base-types-gen must have shipped a hybrid "
                "frame; this test is written to flip when that happens")

    def test_real_corpus_material_refs_all_resolve_except_where_the_source_is_pre_existing(self):
        """Every `.ref` on a `material`-kind row across the WHOLE real corpus resolves against
        `materials-gen`'s `runtimeId` set -- proving manifest row 2 isn't only true for this
        module's own new content."""
        material_ids = tuning_mod.load_material_runtime_ids()
        unresolved = []
        for slot_entries in tuning_mod.entries_by_partition().values():
            for entry in slot_entries:
                for g in entry.get("groups", []):
                    for row in g.get("entries", []):
                        if row.get("entryKind") == "material" and row.get("ref") not in material_ids:
                            unresolved.append((entry["id"], row["ref"]))
        assert unresolved == [], f"dangling material refs: {unresolved}"

    def test_real_corpus_consumable_and_gem_refs_all_resolve(self):
        consumable_ids = tuning_mod.load_consumable_ids()
        gem_ids = tuning_mod.load_gem_ids()
        unresolved = []
        for slot_entries in tuning_mod.entries_by_partition().values():
            for entry in slot_entries:
                for g in entry.get("groups", []):
                    for row in g.get("entries", []):
                        kind, ref = row.get("entryKind"), row.get("ref")
                        if kind == "consumable" and ref not in consumable_ids:
                            unresolved.append((entry["id"], ref))
                        if kind == "insert" and ref not in gem_ids:
                            unresolved.append((entry["id"], ref))
        assert unresolved == [], f"dangling consumable/insert refs: {unresolved}"


# ------------------------------------------------------------------------------------------------
# 6. Symbolic-shape byte-compatibility -- the (separate, unbuilt) band->row expander's own contract
# ------------------------------------------------------------------------------------------------
class TestSymbolicShape:
    """entry-shapes.md §9's own contract: every weight is a `dropBand`, every count is a `qtyCurve`
    reference -- never a literal int. Asserted on this module's own generated output so the
    (out-of-scope) expander's input contract stays provably stable."""

    def test_generated_entries_carry_no_concrete_weight_or_count_field(self):
        doc = _load_real_d1()
        forbidden = {"weight", "minCount", "maxCount", "rarityWeightShift", "min", "max"}
        for entry in doc["entries"]:
            if entry["id"] not in GENERATED_TABLE_IDS:
                continue
            for row in _rows(entry):
                assert not (forbidden & set(row)), f"{entry['id']} row {row} carries a concrete field"

    def test_generated_entries_use_only_the_documented_entry_kind_enum(self):
        legal_kinds = {"equipment", "material", "currency", "insert", "charm", "consumable",
                      "unique", "table", "nothing"}
        doc = _load_real_d1()
        for entry in doc["entries"]:
            if entry["id"] not in GENERATED_TABLE_IDS:
                continue
            for row in _rows(entry):
                assert row["entryKind"] in legal_kinds

    def test_generated_entries_source_allow_satisfies_the_standalone_first_rule(self):
        doc = _load_real_d1()
        for entry in doc["entries"]:
            if entry["id"] not in GENERATED_TABLE_IDS:
                continue
            assert "web" in entry["sourceAllow"]
