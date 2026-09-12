"""Tests for `base-types-gen` (item-seedgen module 6/Phase 3,
`docs/architecture/item-seedgen/spec-base-types-gen.md`).

Four groups:
1. Tuning/vocabulary tests — the registries this module reads fresh (core, classes, sockets, tags)
   and this module's own `data/tuning/base-types-gen.v1.json`.
2. Schema/brief tests — the closed answer schema and the construction-time `audit_schema` guard
   (spec's own testing strategy: "a bare numeric field in the brief schema fails construction").
3. Emit tests — id minting, slugging, and numeric resolution, checked against the real shipped
   corpus's own shape.
4. Harness integration — `RunLedger` resume/reconcile/overwrite wired through this module's
   `run.py`, all against a `tmp_path` ledger/corpus, never the real repo files.
"""
from __future__ import annotations

import json
from pathlib import Path

import pytest

from seedsmith.adapters.items.basetypegen import brief as brief_mod
from seedsmith.adapters.items.basetypegen import emit as emit_mod
from seedsmith.adapters.items.basetypegen import partitions as partitions_mod
from seedsmith.adapters.items.basetypegen import run as run_mod
from seedsmith.pipeline.llm_caller import LlmCallerConfig
from seedsmith.adapters.items.basetypegen import schema as schema_mod
from seedsmith.adapters.items.basetypegen import tuning as tuning_mod
from seedsmith.pipeline.model import audit_schema
from seedsmith.pipeline.run_ledger import RunLedger

REAL_CORPUS_DIR = (
    Path(__file__).resolve().parents[3] / "data" / "seed" / "items" / "base-types"
)


def _load_real(fname: str) -> dict:
    return json.loads((REAL_CORPUS_DIR / fname).read_text(encoding="utf-8"))


# ---------------------------------------------------------------------------------------------
# 1. Tuning / vocabulary
# ---------------------------------------------------------------------------------------------


def test_role_registry_has_exactly_the_15_body_roles_not_the_commander_only_one():
    roles = tuning_mod.load_role_registry()
    assert len(roles) == 15
    assert "standard" not in roles, "the commander-only role is not one of the 30 role-frames"
    assert "armament-primary" in roles


def test_frame_role_name_rule_is_verbatim_from_core_registry():
    info = tuning_mod.role_info("armament-primary")
    assert info.frame_role_name("humanoid") == "main-hand"
    assert info.frame_role_name("plant") == "muzzle"
    info2 = tuning_mod.role_info("jewel-minor-a")
    assert info2.frame_role_name("humanoid") == "ring-1"


def test_unknown_role_is_refused_not_silently_ignored():
    with pytest.raises(tuning_mod.UnknownRoleError):
        tuning_mod.role_info("not-a-role")
    with pytest.raises(tuning_mod.UnknownRoleError):
        tuning_mod.load_socket_ceiling("not-a-role")
    with pytest.raises(tuning_mod.UnknownRoleError):
        tuning_mod.load_legal_implicit_families("not-a-role")


def test_class_choices_is_the_union_of_every_ladder_a_role_draws_from():
    # armament-secondary legally draws from BOTH weapon (blade/blunt/launcher) and offhand
    # (focus/bulwark) ladders on humanoid — the union, not either alone.
    choices = tuning_mod.load_class_choices("armament-secondary", "humanoid")
    assert set(choices) >= {"blade", "blunt", "launcher", "focus", "bulwark"}
    # A pure armour role draws from exactly one ladder.
    core_guard = tuning_mod.load_class_choices("core-guard", "humanoid")
    assert set(core_guard) == {"cloth", "leather", "scale", "plate"}


def test_class_choices_differ_by_frame():
    humanoid = set(tuning_mod.load_class_choices("core-guard", "humanoid"))
    plant = set(tuning_mod.load_class_choices("core-guard", "plant"))
    assert humanoid == {"cloth", "leather", "scale", "plate"}
    assert plant == {"fibre", "husk", "bark", "heartwood"}
    assert humanoid.isdisjoint(plant)


def test_legal_implicit_families_matches_the_real_registry_slate():
    families = tuning_mod.load_legal_implicit_families("armament-primary")
    assert "atom.might" in families
    assert "atom.searing-strike" in families
    # bulwark/savagery are More-op families, globally excluded from every implicit slate.
    assert "atom.bulwark" not in families
    assert "atom.savagery" not in families


def test_socket_ceiling_matches_module_16s_real_tuning_table():
    assert tuning_mod.load_socket_ceiling("armament-primary") == 4
    assert tuning_mod.load_socket_ceiling("core-guard") == 4
    assert tuning_mod.load_socket_ceiling("jewel-major") == 1
    assert tuning_mod.load_socket_ceiling("sense") == 1


def test_gen_tuning_loads_and_validates():
    gt = tuning_mod.load_gen_tuning()
    assert gt.enhance_levels == (4, 12, 20)
    assert gt.enhance_family_count == 3
    assert "a" in gt.power_band_ladder
    assert "b" in gt.power_band_ladder


def test_gen_tuning_refuses_a_structurally_broken_file(tmp_path):
    bad = tmp_path / "bad.json"
    bad.write_text(json.dumps({"socketMaxSplit": {}, "implicitPowerBandLadder": {},
                              "enhanceTrack": {"levels": [12, 4, 20], "familyCount": 3}}),
                   encoding="utf-8")
    with pytest.raises(tuning_mod.TuningError):
        tuning_mod.load_gen_tuning(bad)


def test_milestone_families_are_read_fresh_from_the_real_corpus_not_hardcoded():
    families = tuning_mod.load_milestone_families()
    assert "atom.enhance-edge" in families
    assert "atom.enhance-savagery" in families
    assert "atom.enhance-keen" in families
    assert families == tuple(sorted(families)), "sorted, for determinism"


# ---------------------------------------------------------------------------------------------
# 2. Schema / brief
# ---------------------------------------------------------------------------------------------


def test_bare_numeric_field_in_the_brief_schema_fails_construction():
    """Spec's own testing strategy, verbatim: 'a bare numeric field in the brief schema fails
    construction, mirrors setgen's own Pipeline.__post_init__ guard.'"""
    schema = {
        "type": "object", "additionalProperties": False, "required": [],
        "properties": {"socketMax": {"type": "integer"}},
    }
    defects = audit_schema(schema)
    assert defects, "a bare integer field must be flagged by the shared schema auditor"


def test_base_type_schema_never_offers_the_fields_code_must_resolve():
    schema = schema_mod.base_type_schema(
        class_choices=("blade",), implicit_families=("atom.might",), tags=("offensive",))
    fields = schema_mod.schema_field_names(schema)
    for forbidden in ("socketMax", "band", "id", "nameKey", "iconKey", "flavorKey",
                      "enhanceTrack", "powerBand", "frame", "role"):
        assert forbidden not in fields, f"{forbidden!r} must never be an authorable field"
    assert set(fields) == {"name", "flavor", "class", "implicitFamily", "tags", "blocked"}


def test_base_type_schema_is_clean_under_the_shared_auditor():
    schema = schema_mod.base_type_schema(
        class_choices=("blade", "blunt"), implicit_families=("atom.might",), tags=("offensive",))
    assert audit_schema(schema) == []


def test_base_type_schema_refuses_empty_vocabularies():
    with pytest.raises(ValueError):
        schema_mod.base_type_schema(class_choices=(), implicit_families=("atom.might",),
                                    tags=("offensive",))
    with pytest.raises(ValueError):
        schema_mod.base_type_schema(class_choices=("blade",), implicit_families=(),
                                    tags=("offensive",))
    with pytest.raises(ValueError):
        schema_mod.base_type_schema(class_choices=("blade",), implicit_families=("atom.might",),
                                    tags=())


def test_build_base_type_brief_lists_the_partitions_own_closed_vocabularies():
    b = brief_mod.build_base_type_brief("armament-primary", "humanoid", "a")
    text = b.render()
    assert "armament-primary" in text
    assert "main-hand" in text
    assert "blade" in text and "blunt" in text and "launcher" in text
    assert "atom.might" in text
    assert "never chosen by you" not in text


def test_brief_construction_guard_fires_on_a_hand_built_bad_schema():
    partition = brief_mod.load_partition_context("armament-primary", "humanoid", "a")
    with pytest.raises(ValueError):
        brief_mod.BaseTypeBrief(
            partition=partition, tag_vocab=("offensive",),
            schema={"type": "object", "properties": {"socketMax": {"type": "integer"}}})


def test_load_partition_context_refuses_an_illegal_frame():
    with pytest.raises(ValueError):
        brief_mod.load_partition_context("armament-primary", "zombie", "a")


# ---------------------------------------------------------------------------------------------
# 3. Emit
# ---------------------------------------------------------------------------------------------


def test_slug_and_keys_match_the_real_corpus_convention():
    slug = emit_mod.slug_from_name("Honed Hatchet")
    assert slug == "honed-hatchet"
    assert emit_mod.name_key_for(slug) == "base.honed-hatchet"
    assert emit_mod.icon_key_for(slug) == "icon.base.honed-hatchet"
    assert emit_mod.flavor_key_for(slug) == "flavor.base.honed-hatchet"


def test_entry_id_matches_the_real_naming_v1_idTemplate():
    minted = emit_mod.entry_id("humanoid", "main-hand", "a", 4)
    assert minted == "item.humanoid-main-hand-a-004"
    real = _load_real("humanoid-armament-primary-a.json")["entries"][0]["id"]
    assert emit_mod.ENTRY_ID_RE.match(real), "our id grammar must accept the real corpus's own ids"


def test_next_seq_continues_past_the_highest_existing():
    assert emit_mod.next_seq(()) == 1
    assert emit_mod.next_seq(["item.humanoid-main-hand-a-001",
                             "item.humanoid-main-hand-a-002"]) == 3


def test_next_seq_refuses_the_reserved_900_range():
    with pytest.raises(emit_mod.MintRefused):
        emit_mod.next_seq(["item.humanoid-main-hand-a-899"])


def test_resolve_socket_max_never_exceeds_the_real_role_ceiling():
    gt = tuning_mod.load_gen_tuning()
    ceiling = tuning_mod.load_socket_ceiling("armament-primary")
    for band in ("a", "b"):
        for i in range(20):
            value = tuning_mod.resolve_socket_max("armament-primary", band, i, gen_tuning=gt)
            assert 0 <= value <= ceiling


def test_resolve_socket_max_band_b_reaches_higher_than_band_a_on_a_four_ceiling_role():
    gt = tuning_mod.load_gen_tuning()
    a_values = {tuning_mod.resolve_socket_max("armament-primary", "a", i, gen_tuning=gt)
               for i in range(10)}
    b_values = {tuning_mod.resolve_socket_max("armament-primary", "b", i, gen_tuning=gt)
               for i in range(10)}
    assert max(b_values) >= max(a_values)


def test_resolve_socket_max_respects_a_ceiling_of_one():
    gt = tuning_mod.load_gen_tuning()
    for i in range(6):
        assert tuning_mod.resolve_socket_max("jewel-major", "a", i, gen_tuning=gt) in (0, 1)
        assert tuning_mod.resolve_socket_max("jewel-major", "b", i, gen_tuning=gt) in (0, 1)


def test_resolve_power_band_is_one_of_the_real_bands_registry_enum():
    real_bands = {"trivial", "low", "medium", "high", "extreme"}
    gt = tuning_mod.load_gen_tuning()
    for band in ("a", "b"):
        for i in range(8):
            assert tuning_mod.resolve_power_band(band, i, gen_tuning=gt) in real_bands


def test_resolve_enhance_track_is_a_fixed_three_rung_ladder_naming_real_families():
    real_families = set(tuning_mod.load_milestone_families())
    track = tuning_mod.resolve_enhance_track("armament-primary", "humanoid", "a")
    assert [lvl for lvl, _ in track] == [4, 12, 20]
    families = [fam for _, fam in track]
    assert len(set(families)) == 3, "three distinct families, one per rung"
    for fam in families:
        assert fam in real_families, f"{fam!r} must resolve against the real milestones corpus"


def test_resolve_enhance_track_is_deterministic_and_stable_per_partition():
    t1 = tuning_mod.resolve_enhance_track("armament-primary", "humanoid", "a")
    t2 = tuning_mod.resolve_enhance_track("armament-primary", "humanoid", "a")
    assert t1 == t2
    t3 = tuning_mod.resolve_enhance_track("core-guard", "humanoid", "a")
    assert t1 != t3, "different partitions may draw a different triple"


def test_resolve_enhance_track_refuses_when_the_real_corpus_has_too_few_families(tmp_path):
    empty = tmp_path / "empty-milestones.json"
    empty.write_text(json.dumps({"entries": []}), encoding="utf-8")
    with pytest.raises(tuning_mod.TuningError):
        tuning_mod.resolve_enhance_track("armament-primary", "humanoid", "a",
                                         milestones_path=empty)


def test_assemble_entry_matches_the_real_shipped_entry_shape():
    partition = brief_mod.load_partition_context("armament-primary", "humanoid", "a")
    answer = {"name": "Test Widget", "flavor": "A grounded test object.", "class": "blade",
             "implicitFamily": "atom.might", "tags": ["light", "offensive"]}
    entry = emit_mod.assemble_entry(answer, partition, seq=999)
    real_entry = _load_real("humanoid-armament-primary-a.json")["entries"][0]
    assert set(entry.keys()) == set(real_entry.keys())
    assert entry["id"] == "item.humanoid-main-hand-a-999"
    assert entry["frame"] == "humanoid"
    assert entry["role"] == "armament-primary"
    assert entry["band"] == "a"
    assert set(entry["implicit"].keys()) == set(real_entry["implicit"].keys())
    assert isinstance(entry["socketMax"], int)
    assert all(set(t.keys()) == set(real_entry["enhanceTrack"][0].keys())
              for t in entry["enhanceTrack"])


def test_assemble_entry_refuses_an_illegal_class_or_family():
    partition = brief_mod.load_partition_context("armament-primary", "humanoid", "a")
    base_answer = {"name": "Test Widget", "flavor": "x", "tags": ["light"]}
    with pytest.raises(emit_mod.IllegalChoiceError):
        emit_mod.assemble_entry({**base_answer, "class": "not-a-class",
                                "implicitFamily": "atom.might"}, partition, seq=1)
    with pytest.raises(emit_mod.IllegalChoiceError):
        emit_mod.assemble_entry({**base_answer, "class": "blade",
                                "implicitFamily": "atom.not-a-family"}, partition, seq=1)


def test_assemble_entry_refuses_an_id_collision():
    partition = brief_mod.load_partition_context(
        "armament-primary", "humanoid", "a", base_types_dir=REAL_CORPUS_DIR)
    answer = {"name": "Test Widget", "flavor": "x", "class": "blade",
             "implicitFamily": "atom.might", "tags": ["light"]}
    with pytest.raises(emit_mod.IdCollisionError):
        emit_mod.assemble_entry(answer, partition, seq=1)  # -001 already exists in the real file


def test_assemble_entry_reuses_an_existing_partitions_own_enhance_track():
    """Real bug found while generating a sample entry (2026-09-07): appending to
    `humanoid-core-guard-b.json` (12 existing entries, all sharing one
    `[(4, atom.enhance-vigor), (12, atom.enhance-fortify), (20, atom.enhance-hardy)]` triple) first
    minted a DIFFERENT hash-derived triple for the 13th entry, breaking the shipped corpus's own
    "one triple per partition file" convention. Fixed by having `PartitionContext` read the file's
    already-established triple and `assemble_entry` prefer it over a freshly resolved one."""
    partition = brief_mod.load_partition_context(
        "core-guard", "humanoid", "b", base_types_dir=REAL_CORPUS_DIR)
    assert partition.existing_enhance_track == (
        (4, "atom.enhance-vigor"), (12, "atom.enhance-fortify"), (20, "atom.enhance-hardy"))
    answer = {"name": "Test Cuirass", "flavor": "x", "class": "plate",
             "implicitFamily": "atom.vitality", "tags": ["heavy"]}
    entry = emit_mod.assemble_entry(answer, partition, seq=999)
    assert [(t["atLevel"], t["family"]) for t in entry["enhanceTrack"]] == list(
        partition.existing_enhance_track)


def test_assemble_entry_mints_a_fresh_enhance_track_for_a_brand_new_partition(tmp_path):
    partition = brief_mod.load_partition_context(
        "core-guard", "humanoid", "b", base_types_dir=tmp_path / "empty-base-types")
    assert partition.existing_enhance_track is None
    answer = {"name": "Test Cuirass", "flavor": "x", "class": "plate",
             "implicitFamily": "atom.vitality", "tags": ["heavy"]}
    entry = emit_mod.assemble_entry(answer, partition, seq=1)
    assert len(entry["enhanceTrack"]) == 3


def test_assemble_entry_refuses_empty_tags():
    partition = brief_mod.load_partition_context("armament-primary", "humanoid", "a")
    answer = {"name": "Test Widget", "flavor": "x", "class": "blade",
             "implicitFamily": "atom.might", "tags": []}
    with pytest.raises(emit_mod.MintRefused):
        emit_mod.assemble_entry(answer, partition, seq=1)


# ---------------------------------------------------------------------------------------------
# Real-corpus regression
# ---------------------------------------------------------------------------------------------


def test_real_corpus_socket_max_never_exceeds_module_16s_ceiling():
    """`role == "standard"` is excluded here, not overlooked: `humanoid-standard.json`'s own entry
    carries `role: "standard"`, `enabled: false`, and a `retiredReason` citing D14 ("the commander
    is another unique creature, not a 16th equip slot ... the generator emits nothing into it") —
    module 16's `sockets.v1.json` deliberately has no ceiling row for it either (its own
    `socketCeilingNote`: "`standard` is deliberately ABSENT, not zero"). Confirms this generator's
    own scope decision (30 role-frames, `standard` excluded) matches a real, already-recorded
    owner ruling rather than inventing a new exclusion."""
    import glob
    for path in glob.glob(str(REAL_CORPUS_DIR / "*.json")):
        doc = json.loads(Path(path).read_text(encoding="utf-8"))
        for entry in doc["entries"]:
            role = entry.get("role")
            if role is None or role == "standard":
                continue
            ceiling = tuning_mod.load_socket_ceiling(role)
            socket_max = entry.get("socketMax")
            if socket_max is not None:
                assert socket_max <= ceiling, f"{entry['id']} exceeds role {role!r}'s ceiling"


def test_real_corpus_implicit_families_are_all_in_the_real_registry_slate():
    import glob
    for path in glob.glob(str(REAL_CORPUS_DIR / "*.json")):
        doc = json.loads(Path(path).read_text(encoding="utf-8"))
        for entry in doc["entries"]:
            role = entry.get("role")
            if role is None:
                continue
            try:
                legal = set(tuning_mod.load_legal_implicit_families(role))
            except tuning_mod.UnknownRoleError:
                continue
            family = entry["implicit"]["family"]
            assert family in legal, f"{entry['id']} names {family!r}, outside role {role!r}'s slate"


# ---------------------------------------------------------------------------------------------
# 4. Harness integration — RunLedger resume / reconcile / overwrite, via run.py
# ---------------------------------------------------------------------------------------------


def _fake_call(name: str = "Test Widget", *, block: bool = False):
    def call(brief: str, schema: dict) -> dict:
        if block:
            return {"blocked": "no theme fits"}
        cls = schema["properties"]["class"]["enum"][0]
        fam = schema["properties"]["implicitFamily"]["enum"][0]
        return {"name": name, "flavor": "A generated base type for testing.",
               "class": cls, "implicitFamily": fam, "tags": ["light", "offensive"]}
    return call


def test_plan_run_starts_the_entry_sequence_after_the_real_partitions_own_entries(tmp_path):
    ledger = RunLedger(tmp_path / "ledger.json")
    base_types_dir = tmp_path / "base-types"
    plan = run_mod.plan_run(role="armament-primary", frame="humanoid", band="a", count=2,
                            ledger=ledger, base_types_dir=base_types_dir)
    assert [s.subject_id for s in plan.subjects] == [
        "basetype-draw-armament-primary-humanoid-a-000",
        "basetype-draw-armament-primary-humanoid-a-001",
    ]
    assert [s.seq for s in plan.subjects] == [1, 2]


def test_run_draws_marks_the_ledger_done_and_partitions_fresh_vs_blocked(tmp_path):
    ledger = RunLedger(tmp_path / "ledger.json")
    base_types_dir = tmp_path / "base-types"
    plan = run_mod.plan_run(role="armament-primary", frame="humanoid", band="a", count=2,
                            ledger=ledger, base_types_dir=base_types_dir)

    calls = iter([_fake_call(name="First"), _fake_call(block=True)])
    fresh, blocked = run_mod.run_draws(plan, ledger=ledger, call=lambda b, s: next(calls)(b, s))

    assert len(fresh) == 1
    (entry,) = fresh.values()
    assert entry["id"] == "item.humanoid-main-hand-a-001"
    assert len(blocked) == 1
    assert "basetype-draw-armament-primary-humanoid-a-001" in blocked

    done = ledger.read_done()
    assert set(done) == {"basetype-draw-armament-primary-humanoid-a-000"}


def test_run_draws_keeps_going_after_one_malformed_live_response(tmp_path):
    ledger = RunLedger(tmp_path / "ledger.json")
    base_types_dir = tmp_path / "base-types"
    plan = run_mod.plan_run(role="armament-primary", frame="humanoid", band="a", count=2,
                            ledger=ledger, base_types_dir=base_types_dir)
    calls = iter([ValueError("no JSON object in model output"), _fake_call(name="Recovered Widget")])

    def call(brief: str, schema: dict) -> dict:
        next_result = next(calls)
        if isinstance(next_result, Exception):
            raise next_result
        return next_result(brief, schema)

    fresh, blocked = run_mod.run_draws(plan, ledger=ledger, call=call)

    assert {entry["name"] for entry in fresh.values()} == {"Recovered Widget"}
    assert set(blocked) == {"basetype-draw-armament-primary-humanoid-a-000"}
    assert "no JSON object" in blocked["basetype-draw-armament-primary-humanoid-a-000"]["reason"]
    assert set(ledger.read_done()) == {"basetype-draw-armament-primary-humanoid-a-001"}


def test_run_draws_does_not_checkpoint_before_persist_callback(tmp_path):
    ledger = RunLedger(tmp_path / "ledger.json")
    base_types_dir = tmp_path / "base-types"
    plan = run_mod.plan_run(role="armament-primary", frame="humanoid", band="a", count=1,
                            ledger=ledger, base_types_dir=base_types_dir)

    def persist(_entry):
        raise RuntimeError("simulated corpus write failure")

    with pytest.raises(RuntimeError, match="corpus write failure"):
        run_mod.run_draws(plan, ledger=ledger, call=_fake_call(name="Retry Me"), persist=persist)
    assert ledger.read_done() == {}


def test_resume_never_repeats_a_committed_draw_across_two_plan_run_calls(tmp_path):
    ledger = RunLedger(tmp_path / "ledger.json")
    base_types_dir = tmp_path / "base-types"
    plan1 = run_mod.plan_run(role="core-guard", frame="plant", band="a", count=2, ledger=ledger,
                             base_types_dir=base_types_dir)
    fresh1, _ = run_mod.run_draws(plan1, ledger=ledger, call=_fake_call(name="One"))
    run_mod.write_corpus("core-guard", "plant", "a", fresh1, existing=plan1.existing,
                         base_types_dir=base_types_dir)

    plan2 = run_mod.plan_run(role="core-guard", frame="plant", band="a", count=1, ledger=ledger,
                             base_types_dir=base_types_dir)
    assert plan2.subjects[0].subject_id == "basetype-draw-core-guard-plant-a-002"
    assert plan2.subjects[0].seq == 3, "the entry sequence continues past the two already written"


def test_reconcile_resurfaces_a_draw_whose_entry_was_deleted_from_the_corpus():
    ledger_entry = {"entryId": "item.humanoid-main-hand-a-999", "class": "blade",
                    "implicitFamily": "atom.might"}
    assert run_mod.is_valid("x", ledger_entry, existing={}) is False


def test_plan_run_reuses_an_invalid_ledger_slot_before_allocating_new_draws(tmp_path):
    ledger = RunLedger(tmp_path / "ledger.json")
    ledger.mark_done("basetype-draw-armament-primary-humanoid-a-000", {
        "entryId": "item.humanoid-main-hand-a-999", "class": "blade",
        "implicitFamily": "atom.might",
    })
    plan = run_mod.plan_run(role="armament-primary", frame="humanoid", band="a", count=1,
                            ledger=ledger, base_types_dir=tmp_path / "base-types")
    assert plan.subjects[0].subject_id == "basetype-draw-armament-primary-humanoid-a-000"


def test_reconcile_leaves_a_draw_alone_when_its_entry_still_matches():
    ledger_entry = {"entryId": "item.humanoid-main-hand-a-999", "class": "blade",
                    "implicitFamily": "atom.might"}
    existing = {"item.humanoid-main-hand-a-999": {
        "class": "blade", "implicit": {"family": "atom.might"}}}
    assert run_mod.is_valid("x", ledger_entry, existing=existing) is True


def test_reconcile_resurfaces_a_draw_whose_class_was_hand_edited_out_from_under_it():
    ledger_entry = {"entryId": "item.humanoid-main-hand-a-999", "class": "blade",
                    "implicitFamily": "atom.might"}
    existing = {"item.humanoid-main-hand-a-999": {
        "class": "blunt", "implicit": {"family": "atom.might"}}}
    assert run_mod.is_valid("x", ledger_entry, existing=existing) is False


def test_write_corpus_is_additive_and_never_drops_an_existing_entry(tmp_path):
    base_types_dir = tmp_path / "base-types"
    ledger = RunLedger(tmp_path / "ledger.json")
    plan = run_mod.plan_run(role="armament-primary", frame="humanoid", band="a", count=1,
                            ledger=ledger, base_types_dir=base_types_dir)
    fresh, _ = run_mod.run_draws(plan, ledger=ledger, call=_fake_call(name="First"))
    path1 = run_mod.write_corpus("armament-primary", "humanoid", "a", fresh,
                                 existing=plan.existing, base_types_dir=base_types_dir)

    ledger2 = RunLedger(tmp_path / "ledger.json")
    plan2 = run_mod.plan_run(role="armament-primary", frame="humanoid", band="a", count=1,
                             ledger=ledger2, base_types_dir=base_types_dir)
    fresh2, _ = run_mod.run_draws(plan2, ledger=ledger2, call=_fake_call(name="Second"))
    path2 = run_mod.write_corpus("armament-primary", "humanoid", "a", fresh2,
                                 existing=plan2.existing, base_types_dir=base_types_dir)

    assert path1 == path2
    written = json.loads(path2.read_text(encoding="utf-8"))
    written_ids = {e["id"] for e in written["entries"]}
    assert written_ids == {"item.humanoid-main-hand-a-001", "item.humanoid-main-hand-a-002"}


def test_nested_legacy_partition_is_read_and_rewritten_in_place(tmp_path):
    base_types_dir = tmp_path / "base-types"
    legacy_path = base_types_dir / "mantle" / "humanoid" / "a.json"
    legacy_path.parent.mkdir(parents=True)
    legacy_path.write_text(json.dumps({
        "schemaVersion": 1,
        "kind": "base-type",
        "entries": [{
            "id": "item.humanoid-back-a-001",
            "name": "Weathered Cloak",
            "frame": "humanoid",
            "role": "mantle",
            "band": "a",
            "class": "cloak",
            "implicit": {"family": "atom.evasion", "powerBand": "low"},
            "socketMax": 0,
            "tags": ["cloth"],
            "enhanceTrack": [{"atLevel": 4, "family": "atom.enhance-edge"}],
        }],
    }), encoding="utf-8")

    ledger = RunLedger(tmp_path / "ledger.json")
    plan = run_mod.plan_run(role="mantle", frame="humanoid", band="a", count=1,
                            ledger=ledger, base_types_dir=base_types_dir)
    assert set(plan.existing) == {"item.humanoid-back-a-001"}

    fresh, _ = run_mod.run_draws(plan, ledger=ledger, call=_fake_call(name="Storm Mantle"))
    written = run_mod.write_corpus("mantle", "humanoid", "a", fresh,
                                   existing=plan.existing, base_types_dir=base_types_dir)

    assert written == legacy_path
    assert not (base_types_dir / "humanoid-mantle-a.json").exists()
    assert len(json.loads(written.read_text(encoding="utf-8"))["entries"]) == 2


def test_duplicate_files_claiming_one_partition_refuse_before_any_write(tmp_path):
    base_types_dir = tmp_path / "base-types"
    nested = base_types_dir / "mantle" / "humanoid" / "a.json"
    nested.parent.mkdir(parents=True)
    entry = {"id": "item.humanoid-back-a-001", "frame": "humanoid", "role": "mantle", "band": "a"}
    document = json.dumps({"kind": "base-type", "entries": [entry]})
    nested.write_text(document, encoding="utf-8")
    (base_types_dir / "humanoid-mantle-a.json").write_text(document, encoding="utf-8")

    with pytest.raises(partitions_mod.PartitionAmbiguityError, match="multiple files"):
        run_mod.load_existing("mantle", "humanoid", "a", base_types_dir=base_types_dir)


def test_overwrite_by_id_bypasses_validity_for_named_ids_only(tmp_path):
    ledger = RunLedger(tmp_path / "ledger.json")
    ledger.mark_done("basetype-draw-armament-primary-humanoid-a-000",
                     {"entryId": "item.humanoid-main-hand-a-001"})
    forced = ledger.force(["basetype-draw-armament-primary-humanoid-a-000"], "ids")
    assert forced == ["basetype-draw-armament-primary-humanoid-a-000"]


def test_cli_dry_run_prints_a_sample_brief_and_makes_no_model_calls(capsys):
    exit_code = run_mod.main(["--role", "armament-primary", "--frame", "humanoid", "--band", "a",
                             "--dry-run", "--count", "1"])
    assert exit_code == 0
    out = capsys.readouterr().out
    assert "armament-primary" in out


def test_cli_refuses_a_real_run_with_no_model_call_wired():
    """⛔ Renamed in spirit 2026-09-08, unchanged in assertion: this no longer refuses because
    "no model call is wired" (that gap is closed below) — it refuses because `--write` was not
    passed, exactly like `items generate`'s own dry-by-default convention."""
    with pytest.raises(SystemExit):
        run_mod.main(["--role", "armament-primary", "--frame", "humanoid", "--band", "a"])


def test_cli_write_without_endpoint_refuses(tmp_path, monkeypatch):
    """Refuse only when the *resolved* transport has no endpoint (CLI empty + config empty)."""
    monkeypatch.setattr(run_mod, "DEFAULT_LEDGER_PATH", tmp_path / "ledger.json")
    monkeypatch.setattr(
        "seedsmith.pipeline.llm_caller.resolve_live_transport",
        lambda *a, **k: LlmCallerConfig(endpoint="", model="x"))
    with pytest.raises(SystemExit):
        run_mod.main(["--role", "armament-primary", "--frame", "humanoid", "--band", "a",
                     "--write"])



def test_cli_a_real_live_run_writes_a_real_partition_file(tmp_path, monkeypatch, capsys):
    """⛔ Real gap, closed 2026-09-08: before this, `--write --endpoint <url>` was UNREACHABLE —
    every path through `main()` past `--dry-run`/`--overwrite` raised `SystemExit`
    unconditionally. Proves the full wire: CLI args -> `load_config()`-based config ->
    `live_answer_caller` -> `run_draws` -> `write_corpus`, landing a real file on disk."""
    monkeypatch.setattr(run_mod, "DEFAULT_LEDGER_PATH", tmp_path / "ledger.json")
    monkeypatch.setattr(tuning_mod, "BASE_TYPES_DIR", tmp_path / "base-types")

    called_with = {}

    def _fake_live_answer_caller(config):
        called_with["config"] = config
        return _fake_call(name="Live-Wired Widget")

    monkeypatch.setattr("seedsmith.pipeline.llm_caller.live_answer_caller",
                        _fake_live_answer_caller)

    exit_code = run_mod.main(["--role", "armament-primary", "--frame", "humanoid", "--band", "a",
                             "--count", "1", "--write", "--endpoint", "http://unit-test-endpoint",
                             "--model", "unit-test-model"])

    assert exit_code == 0
    assert called_with["config"].endpoint == "http://unit-test-endpoint"
    assert called_with["config"].model == "unit-test-model"
    written = json.loads((tmp_path / "base-types" / "humanoid-armament-primary-a.json")
                        .read_text(encoding="utf-8"))
    names = {e["name"] for e in written["entries"]}
    assert "Live-Wired Widget" in names
    summary = json.loads(capsys.readouterr().out)
    assert summary == {"planned": 1, "fresh": 1, "blocked": 0, "blockedReasons": {}}


def test_cli_overwrite_routes_through_the_real_ledger(tmp_path, monkeypatch):
    ledger_path = tmp_path / "ledger.json"
    monkeypatch.setattr(run_mod, "DEFAULT_LEDGER_PATH", ledger_path)
    ledger = RunLedger(ledger_path)
    ledger.mark_done("basetype-draw-armament-primary-humanoid-a-000",
                     {"entryId": "item.humanoid-main-hand-a-001"})
    exit_code = run_mod.main(["--role", "armament-primary", "--frame", "humanoid", "--band", "a",
                             "--overwrite", "basetype-draw-armament-primary-humanoid-a-000"])
    assert exit_code == 0


def test_cli_force_is_an_accepted_alias_for_overwrite(tmp_path, monkeypatch, capsys):
    """seedsmith-content-standard Task 6: `--force` is `content-completeness-core`'s own naming
    convention (`RunLedger.force()`, `generate_commander_effects.py --force`); this CLI's real,
    already-shipped flag is `--overwrite` (spec-base-types-gen.md). Added as a second flag string on
    the same argument rather than a rename, so nothing already typing `--overwrite` breaks."""
    ledger_path = tmp_path / "ledger.json"
    monkeypatch.setattr(run_mod, "DEFAULT_LEDGER_PATH", ledger_path)
    ledger = RunLedger(ledger_path)
    ledger.mark_done("basetype-draw-armament-primary-humanoid-a-000",
                     {"entryId": "item.humanoid-main-hand-a-001"})
    exit_code = run_mod.main(["--role", "armament-primary", "--frame", "humanoid", "--band", "a",
                             "--force", "basetype-draw-armament-primary-humanoid-a-000"])
    assert exit_code == 0


# ---- corpus-wide name collision guard (2026-09-12) ---------------------------------------------

def test_collision_key_folds_case_and_punctuation():
    assert run_mod.collision_key("Tungsten Spiker") == run_mod.collision_key("tungsten  spiker")
    assert run_mod.collision_key("Weighted Censer") == run_mod.collision_key("Weighted-Censer")


def test_load_corpus_names_spans_every_partition_not_just_one(tmp_path):
    base_types_dir = tmp_path / "base-types"
    base_types_dir.mkdir()
    (base_types_dir / "humanoid-armament-primary-a.json").write_text(json.dumps(
        {"entries": [{"id": "item.a-001", "name": "Tungsten Spiker"}]}), encoding="utf-8")
    (base_types_dir / "humanoid-armament-primary-b.json").write_text(json.dumps(
        {"entries": [{"id": "item.b-001", "name": "Weighted Censer"}]}), encoding="utf-8")

    names = run_mod.load_corpus_names(base_types_dir=base_types_dir)
    assert names[run_mod.collision_key("Tungsten Spiker")] == "item.a-001"
    assert names[run_mod.collision_key("Weighted Censer")] == "item.b-001"


def test_run_draws_reasks_once_then_refuses_a_corpus_wide_name_collision(tmp_path):
    """The defect this closes: 65 display names shipped twice across partitions because the brief
    only listed the current partition's names. The corpus-wide map now makes the second draw refuse
    rather than persist a duplicate."""
    ledger = RunLedger(tmp_path / "ledger.json")
    base_types_dir = tmp_path / "base-types"
    plan = run_mod.plan_run(role="armament-primary", frame="humanoid", band="a", count=1,
                            ledger=ledger, base_types_dir=base_types_dir)

    taken = {"tungsten spiker": "item.humanoid-main-hand-a-001"}
    calls = {"n": 0}

    def stubborn_call(brief: str, schema: dict) -> dict:
        calls["n"] += 1
        cls = schema["properties"]["class"]["enum"][0]
        fam = schema["properties"]["implicitFamily"]["enum"][0]
        return {"name": "Tungsten Spiker", "flavor": "f", "class": cls,
                "implicitFamily": fam, "tags": ["light"]}

    fresh, blocked = run_mod.run_draws(plan, ledger=ledger, call=stubborn_call,
                                       corpus_names=dict(taken))

    assert fresh == {}
    assert calls["n"] == 2  # original ask + one repair ask
    assert any("already exists in the base-type corpus" in b["reason"] for b in blocked.values())


def test_run_draws_accepts_a_distinct_repair_answer(tmp_path):
    ledger = RunLedger(tmp_path / "ledger.json")
    base_types_dir = tmp_path / "base-types"
    plan = run_mod.plan_run(role="armament-primary", frame="humanoid", band="a", count=1,
                            ledger=ledger, base_types_dir=base_types_dir)

    taken = {"tungsten spiker": "item.humanoid-main-hand-a-001"}
    answers = iter(["Tungsten Spiker", "Honed Hatchet"])

    def call(brief: str, schema: dict) -> dict:
        cls = schema["properties"]["class"]["enum"][0]
        fam = schema["properties"]["implicitFamily"]["enum"][0]
        return {"name": next(answers), "flavor": "f", "class": cls,
                "implicitFamily": fam, "tags": ["light"]}

    fresh, blocked = run_mod.run_draws(plan, ledger=ledger, call=call, corpus_names=dict(taken))
    assert blocked == {}
    assert [e["name"] for e in fresh.values()] == ["Honed Hatchet"]


def test_run_draws_without_corpus_names_stays_backward_compatible(tmp_path):
    """The parameter is opt-in: an existing caller that passes no map keeps the old behaviour."""
    ledger = RunLedger(tmp_path / "ledger.json")
    plan = run_mod.plan_run(role="armament-primary", frame="humanoid", band="a", count=1,
                            ledger=ledger, base_types_dir=tmp_path / "base-types")
    fresh, blocked = run_mod.run_draws(plan, ledger=ledger, call=_fake_call(name="Tungsten Spiker"))
    assert len(fresh) == 1 and blocked == {}
