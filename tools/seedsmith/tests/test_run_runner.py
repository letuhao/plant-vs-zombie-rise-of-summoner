"""Tests for the `run-control` execution driver (spec-run-control.md, demon-seed module 9).

Uses `test_run_orchestrator.py`'s own `always_valid_call` stub — the REAL LangGraph pipeline
graphs run end to end, only the network call is replaced, so these tests exercise the actual
`run_one_species`/anchor-emit/record wiring, never a re-implementation of it.
"""
from __future__ import annotations

import json
import time
from pathlib import Path

import pytest

pytest.importorskip("langgraph.graph")

from seedsmith.adapters.demons.anchor.prompts import PIPELINES  # noqa: E402
from seedsmith.adapters.demons.preflight import PREFLIGHT_RECORD_NAME, _compute_content_hash  # noqa: E402
from seedsmith.adapters.demons.run import runner  # noqa: E402
from seedsmith.adapters.demons.run.record import RunRecord, write_record  # noqa: E402
from test_run_orchestrator import always_valid_call  # noqa: E402

# Every SPECIES row below has statsObserved=True -> basis="observed" -> threat-audit is NOT one of
# the voted pipelines (Q26: only inferred/blocked genuinely choose a rung — observed/stated AUDIT
# a deterministic computed one). So 4 of the 8 pipelines vote (3 samples each) and 4 don't (1 each):
# 4*3 + 4*1 = 16, never a flat `len(PIPELINES)` (spec-option-permutation.md §6's own budget).
CALLS_PER_OBSERVED_SPECIES = 16


def make_dump(tmp_path: Path, species: "list[dict]") -> Path:
    """A minimal, real-shaped corpus-dump tree — same row schema `dump_ctx.py`/`preflight.py`
    actually read (side/typeId/typeName/displayName/flavorInfo/hp/attack/statsObserved)."""
    dump_dir = tmp_path / "_dump"
    almanac = dump_dir / "almanac"
    almanac.mkdir(parents=True)
    plants = [s for s in species if s["side"] == "plant"]
    zombies = [s for s in species if s["side"] == "zombie"]
    (almanac / "plant.json").write_text(json.dumps(plants), encoding="utf-8")
    (almanac / "zombie.json").write_text(json.dumps(zombies), encoding="utf-8")
    # `_compute_content_hash` (preflight.py) hashes all FOUR payload files together — these two
    # are otherwise-unused-by-this-test but must exist or the hash is None on every dump,
    # silently defeating any test that checks the hash actually changed.
    (dump_dir / "spawn-baseline.json").write_text("[]", encoding="utf-8")
    (dump_dir / "recipes.json").write_text("[]", encoding="utf-8")
    (dump_dir / "_manifest.json").write_text(
        json.dumps({"plantCount": len(plants), "zombieCount": len(zombies)}), encoding="utf-8")
    return dump_dir


def species_row(species_id: str, side: str, type_id: int) -> dict:
    return {
        "side": side, "typeId": type_id, "typeName": species_id, "displayName": species_id,
        "flavorInfo": f"{species_id} flavor text, no numbers spoken here.",
        "flavorIntroduce": None, "hp": 300, "attack": 20, "statsObserved": True,
        "enrichment": None,
    }


SPECIES = [species_row("alpha", "plant", 1), species_row("beta", "plant", 2)]


def write_real_preflight(dump_dir: Path) -> None:
    dump_hash = _compute_content_hash(dump_dir)
    record = {"dumpHash": dump_hash, "modelId": "stub", "lockHash": None, "skipModel": False,
              "writtenUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())}
    (dump_dir / PREFLIGHT_RECORD_NAME).write_text(json.dumps(record), encoding="utf-8")


def make_paths(tmp_path: Path, species=SPECIES) -> runner.RunPaths:
    dump_dir = make_dump(tmp_path, species)
    write_real_preflight(dump_dir)
    return runner.RunPaths(
        dump_dir=dump_dir, anchors_dir=tmp_path / "species", runs_dir=tmp_path / "_runs",
        family_assignments=tmp_path / "families.json")  # absent on purpose: falls back to "unclassified"


def test_start_classifies_every_selected_species_and_writes_anchor_files(tmp_path):
    paths = make_paths(tmp_path)
    record = runner.start({"kind": "all"}, paths=paths, call=always_valid_call)

    assert record.state == "completed"
    assert sorted(record.completed) == ["alpha", "beta"]
    assert record.calls_made == 2 * CALLS_PER_OBSERVED_SPECIES

    index = json.loads((paths.anchors_dir / "_index.json").read_text(encoding="utf-8"))
    assert set(index) == {"alpha", "beta"}
    for species_id, rel in index.items():
        entries = json.loads((paths.anchors_dir / rel).read_text(encoding="utf-8"))
        assert any(e["speciesId"] == species_id for e in entries)
        entry = next(e for e in entries if e["speciesId"] == species_id)
        assert entry["_provenance"]["dumpHash"] == record.dump_hash
        assert "_derived" in entry


def test_skip_model_preflight_cannot_start_a_run(tmp_path):
    paths = make_paths(tmp_path)
    dump_hash = _compute_content_hash(paths.dump_dir)
    (paths.dump_dir / PREFLIGHT_RECORD_NAME).write_text(
        json.dumps({"dumpHash": dump_hash, "skipModel": True}), encoding="utf-8")

    with pytest.raises(runner.RunRefused, match="skip-model"):
        runner.start({"kind": "all"}, paths=paths, call=always_valid_call)


def test_no_preflight_record_cannot_start(tmp_path):
    paths = make_paths(tmp_path)
    (paths.dump_dir / PREFLIGHT_RECORD_NAME).unlink()

    with pytest.raises(runner.RunRefused, match="preflight"):
        runner.start({"kind": "all"}, paths=paths, call=always_valid_call)


def test_pause_never_splits_a_species_and_resume_makes_no_new_call_for_it(tmp_path):
    paths = make_paths(tmp_path)
    calls = {"n": 0}

    def counting_call(system, user, *, config=None, schema=None):
        calls["n"] += 1
        return always_valid_call(system, user, config=config, schema=schema)

    seen: "list[str]" = []

    def progress(species_id, done, total):
        seen.append(species_id)
        if len(seen) == 1:
            runner.request_pause(paths=paths)  # pause is polled BETWEEN species only

    record = runner.start({"kind": "all"}, paths=paths, call=counting_call, progress=progress)
    assert record.state == "paused"
    assert len(record.completed) == 1  # the first species finished ALL eight pipelines
    calls_after_pause = calls["n"]
    assert calls_after_pause == CALLS_PER_OBSERVED_SPECIES  # one full species, never a partial one

    resumed = runner.resume(paths=paths, call=counting_call, progress=progress)
    assert resumed.state == "completed"
    assert sorted(resumed.completed) == ["alpha", "beta"]
    # the resumed species made exactly one more species' worth of calls — the paused one was
    # NOT re-classified (TRANSIENT semantics, spec §1).
    assert calls["n"] == calls_after_pause + CALLS_PER_OBSERVED_SPECIES


def test_resume_against_changed_dump_refuses(tmp_path):
    paths = make_paths(tmp_path)

    def pause_after_first(system, user, *, config=None, schema=None):
        return always_valid_call(system, user, config=config, schema=schema)

    def progress(species_id, done, total):
        if done == 1:
            runner.request_pause(paths=paths)

    runner.start({"kind": "all"}, paths=paths, call=pause_after_first, progress=progress)

    # The dump changes underneath the paused run (a new corpus-dump was captured).
    (paths.dump_dir / "almanac" / "plant.json").write_text(
        json.dumps([species_row("alpha", "plant", 1), species_row("gamma", "plant", 3)]),
        encoding="utf-8")

    with pytest.raises(runner.RunRefused, match="changed"):
        runner.resume(paths=paths, call=always_valid_call)


def test_dead_process_record_offers_resume_not_a_permanent_refusal(tmp_path):
    paths = make_paths(tmp_path)
    dump_hash = _compute_content_hash(paths.dump_dir)
    dead_record = RunRecord(
        run_id="dead-run", state="running", preflight={"dumpHash": dump_hash}, dump_hash=dump_hash,
        selector={"kind": "all"}, prompt_versions={}, pid=999_999_999,  # never a real live pid
        completed=[], failed=[], started_utc="2020-01-01T00:00:00Z")
    write_record(dead_record, paths.current_record_path)

    resumed = runner.resume(paths=paths, call=always_valid_call)
    assert resumed.state == "completed"


def test_two_concurrent_starts_refuse_the_second(tmp_path):
    paths = make_paths(tmp_path)
    dump_hash = _compute_content_hash(paths.dump_dir)
    import os
    live_record = RunRecord(
        run_id="live-run", state="running", preflight={"dumpHash": dump_hash}, dump_hash=dump_hash,
        selector={"kind": "all"}, prompt_versions={}, pid=os.getpid(),  # THIS process — always alive
        completed=[], failed=[], started_utc="2020-01-01T00:00:00Z")
    write_record(live_record, paths.current_record_path)

    with pytest.raises(runner.RunRefused, match="live-run"):
        runner.start({"kind": "all"}, paths=paths, call=always_valid_call)


def test_cancel_marks_terminal_and_commits_the_record(tmp_path):
    paths = make_paths(tmp_path)

    def progress(species_id, done, total):
        if done == 1:
            runner.request_pause(paths=paths)

    runner.start({"kind": "all"}, paths=paths, call=always_valid_call, progress=progress)
    assert runner.status(paths=paths)["state"] == "paused"

    cancelled = runner.cancel(paths=paths)
    assert cancelled.state == "cancelled"
    assert not paths.current_record_path.exists()
    assert (paths.runs_dir / f"{cancelled.run_id}.json").exists()
    # the one species finished before the pause is a real, already-emitted seed — untouched.
    index = json.loads((paths.anchors_dir / "_index.json").read_text(encoding="utf-8"))
    assert "alpha" in index


def test_overwrite_all_requires_the_correct_token(tmp_path):
    paths = make_paths(tmp_path)
    with pytest.raises(runner.RunRefused, match="token"):
        runner.overwrite_all("wrong-token", paths=paths, call=always_valid_call)

    dump_hash = _compute_content_hash(paths.dump_dir)
    from seedsmith.adapters.demons.run.record import overwrite_all_token
    token = overwrite_all_token(dump_hash)
    record = runner.overwrite_all(token, paths=paths, call=always_valid_call)
    assert record.state == "completed"


def test_rerun_reclassifies_even_an_already_emitted_species(tmp_path):
    paths = make_paths(tmp_path)
    runner.start({"kind": "all"}, paths=paths, call=always_valid_call)

    calls = {"n": 0}

    def counting_call(system, user, *, config=None, schema=None):
        calls["n"] += 1
        return always_valid_call(system, user, config=config, schema=schema)

    record = runner.rerun({"kind": "species", "species": ["alpha"]}, paths=paths, call=counting_call)
    assert record.state == "completed"
    assert record.completed == ["alpha"]
    assert calls["n"] == CALLS_PER_OBSERVED_SPECIES  # re-classified despite already being emitted


def test_status_reports_state_and_progress(tmp_path):
    paths = make_paths(tmp_path)
    assert runner.status(paths=paths)["state"] == "idle"

    def progress(species_id, done, total):
        if done == 1:
            runner.request_pause(paths=paths)

    runner.start({"kind": "all"}, paths=paths, call=always_valid_call, progress=progress)
    s = runner.status(paths=paths)
    assert s["state"] == "paused"
    assert s["completed"] == 1


# ---- family-file bucketing (2026-09-02) ------------------------------------------------------
#
# Real bug found on T2.11's own 20-species run: `_family_for` only ever consulted
# `family-assignments.json` (built for the unrelated fusion-product `demon` corpus) and ignored
# the anchor's own just-classified `family` field entirely — every base species not coincidentally
# sharing an id with that other corpus landed in the generic "unclassified" bucket despite the
# `identity` pipeline (spec-classify-pipelines.md pipeline 8) already having proposed a real family
# for it in the SAME run.


def test_family_for_prefers_the_anchors_own_classified_family():
    # No entry in the external lookup at all — the anchor's own LLM-proposed family still wins.
    assert runner._family_for("chomper", {}, classified_family=["Apex Predator Flora"]) == "apex-predator-flora"


def test_family_for_falls_back_to_the_external_lookup_when_unclassified():
    families = {"cherrybomb": ["cherry"]}
    assert runner._family_for("CherryBomb", families, classified_family=None) == "cherry"
    assert runner._family_for("CherryBomb", families, classified_family=[]) == "cherry"


def test_family_for_falls_back_to_unclassified_when_neither_source_has_anything():
    assert runner._family_for("nobody", {}, classified_family=None) == "unclassified"
    assert runner._family_for("nobody", {}, classified_family=[""]) == "unclassified"


def test_family_for_ignores_blank_entries_in_the_classified_list():
    # An open array can contain a blank string without being empty — skip to the next real value.
    assert runner._family_for("x", {}, classified_family=["", "  ", "Voidkin"]) == "voidkin"


def test_slugify_family_matches_the_repos_own_kebab_case_grammar():
    assert runner._slugify_family("Apex Predator Flora") == "apex-predator-flora"
    assert runner._slugify_family("Ice & Frost!!") == "ice-frost"
    assert runner._slugify_family("  already-kebab  ") == "already-kebab"


# ---- start/resume mutual exclusion (2026-09-02) ------------------------------------------------
#
# Real bug: two overlapping `resume`-driving loops (this session's own process management, not a
# hypothetical) both read the SAME record between one species' completion and the next `resume`
# call, both saw it as resumable (the pre-existing `state == "running" and is_process_alive(pid)`
# check is a read-then-check, not an atomic claim), and both proceeded — producing two divergent
# real anchor files for the same 2 species. These tests exercise the lock directly (deterministic)
# rather than racing two real `resume()` calls via threads (timing-sensitive, and the pre-existing
# `always_valid_call` stub is fast enough that a thread race would be flaky either way) — the lock
# IS the mechanism under test, so holding it and asserting the second caller is refused proves
# exactly the same thing a true race would, without the flake risk.


def test_a_held_lock_refuses_a_concurrent_start(tmp_path):
    paths = make_paths(tmp_path)
    runner._acquire_run_lock(paths.runs_dir)
    try:
        with pytest.raises(runner.RunRefused, match="already in progress"):
            runner.start({"kind": "all"}, paths=paths, call=always_valid_call)
    finally:
        runner._release_run_lock(paths.runs_dir)

    # Released, the SAME call now succeeds — the lock refuses while held, not permanently.
    record = runner.start({"kind": "all"}, paths=paths, call=always_valid_call)
    assert record.state == "completed"


def test_a_held_lock_refuses_a_concurrent_resume(tmp_path):
    paths = make_paths(tmp_path)
    seen: "list[str]" = []

    def progress(species_id, done, total):
        seen.append(species_id)
        if len(seen) == 1:
            runner.request_pause(paths=paths)

    paused = runner.start({"kind": "all"}, paths=paths, call=always_valid_call, progress=progress)
    assert paused.state == "paused"

    runner._acquire_run_lock(paths.runs_dir)
    try:
        with pytest.raises(runner.RunRefused, match="already in progress"):
            runner.resume(paths=paths, call=always_valid_call)
    finally:
        runner._release_run_lock(paths.runs_dir)

    resumed = runner.resume(paths=paths, call=always_valid_call)
    assert resumed.state == "completed"


def test_a_stale_lock_from_a_dead_process_is_reclaimed_not_a_permanent_refusal(tmp_path):
    paths = make_paths(tmp_path)
    paths.runs_dir.mkdir(parents=True, exist_ok=True)
    lock_path = paths.runs_dir / runner.RESUME_LOCK_NAME
    # A pid nothing on this machine will ever hold -- 2**31-1, matching this repo's own
    # `is_process_alive`-tests-a-dead-pid convention elsewhere.
    lock_path.write_text("2147483647", encoding="utf-8")

    record = runner.start({"kind": "all"}, paths=paths, call=always_valid_call)

    assert record.state == "completed"
    # The stale lock was reclaimed and released after the call, not left behind.
    assert not lock_path.exists()


def test_a_normal_call_leaves_no_lock_file_behind(tmp_path):
    paths = make_paths(tmp_path)
    runner.start({"kind": "all"}, paths=paths, call=always_valid_call)
    assert not (paths.runs_dir / runner.RESUME_LOCK_NAME).exists()
