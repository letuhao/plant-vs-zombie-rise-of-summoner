"""The `run-control` execution driver (demon-seed module 9, spec-run-control.md) — ties
`machine`/`record`/`selectors` (pure, already tested) to the real classification loop
(`orchestrator.run_one_species`, which defaults to a REAL model call) and to `anchor-emit`'s
canonical file writer, so `demons run start/resume/pause/cancel/rerun/status/overwrite-all`
means something end to end.

**Checkpoint granularity is one species** (spec §2's own warning): the run record's `completed`
list is rewritten to disk after every species, and the anchor family file that species belongs to
is rewritten too — a crash or a pause between species loses nothing, and a pause never leaves a
species half-classified across its eight pipelines.
"""
from __future__ import annotations

import json
import os
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable, Mapping

from ..anchor.derive import clamp_variant_count, derive_posture, derive_pure
from ..anchor.emit import build_index, entry_for, render_index, write_family_file
from ..anchor.prompts import PIPELINES, SpeciesLore, threat_audit_spec_for_basis
from ..anchor.provenance import PROMPT_VERSIONS, AnchorProvenance
from ..dump_ctx import load_demon_dump_ctx
from ..power.bands import ThreatTuning, classify as classify_threat
from ..preflight import DEFAULT_DUMP_DIR, PREFLIGHT_RECORD_NAME
from ....pipeline.llm_caller import LlmCallerConfig, load_config
from .machine import transition
from .orchestrator import run_one_species
from .record import (
    RunRecord, can_overwrite_all, can_resume, can_start, is_process_alive, new_run_id,
    overwrite_all_token, read_record, write_record,
)
from .selectors import resolve_selector

REPO_ROOT = Path(__file__).resolve().parents[6]
DEFAULT_ANCHORS_DIR = REPO_ROOT / "data" / "seed" / "demons" / "species"
DEFAULT_RUNS_DIR = REPO_ROOT / "data" / "seed" / "demons" / "_runs"
DEFAULT_FAMILY_ASSIGNMENTS = REPO_ROOT / "data" / "seed" / "demons" / "_generated" / "family-assignments.json"

#: In-progress record — gitignored, lives beside the checkpoint (spec §3: "committed only for
#: completed runs; in-progress records live beside the checkpoint DB and are gitignored").
CURRENT_RECORD_NAME = "_current.json"
PAUSE_SENTINEL_NAME = "_pause.request"

Progress = Callable[[str, int, int], None]  # (species_id, done_count, total_count) -> None


class RunRefused(RuntimeError):
    """Every refusal names the reason (record.py's own discipline) — never a bare exit code."""


def _species_rows(dump_dir: Path) -> "list[dict]":
    plant = json.loads((dump_dir / "almanac" / "plant.json").read_text(encoding="utf-8"))
    zombie = json.loads((dump_dir / "almanac" / "zombie.json").read_text(encoding="utf-8"))
    rows = plant + zombie
    for r in rows:
        r.setdefault("speciesId", r.get("typeName") or f"{r['side']}-{r['typeId']}")
    return rows


def _lore_for(row: Mapping[str, Any]) -> SpeciesLore:
    return SpeciesLore(
        species_id=row["speciesId"], side=row["side"], display_name=row.get("displayName"),
        flavor_info=row.get("flavorInfo"), flavor_introduce=row.get("flavorIntroduce"),
        enrichment=row.get("enrichment"))


def _load_existing_anchors(anchors_dir: Path) -> "list[dict]":
    index_path = anchors_dir / "_index.json"
    if not index_path.exists():
        return []
    index = json.loads(index_path.read_text(encoding="utf-8"))
    out: "list[dict]" = []
    for rel_path in sorted(set(index.values())):
        path = anchors_dir / rel_path
        if path.exists():
            out.extend(json.loads(path.read_text(encoding="utf-8")))
    return out


def _load_families(path: Path) -> "dict[str, list[str]]":
    """Keyed lower-case: `family-assignments.json` (generate_families.py) writes lower-case
    speciesIds, but the corpus-dump's own `speciesId` is the captured `typeName` (TitleCase, e.g.
    'Peashooter') — a case-sensitive lookup here silently missed every real species and dropped
    everything into the 'unclassified' bucket (caught on a real 2026-09-02 proof run)."""
    if not path.exists():
        return {}
    raw = json.loads(path.read_text(encoding="utf-8"))
    return {k.lower(): v for k, v in raw.items()}


def _family_for(species_id: str, families: "dict[str, list[str]]") -> str:
    return (families.get(species_id.lower()) or ["unclassified"])[0]


def _compute_dump_hash(dump_dir: Path) -> str:
    """Delegates to `preflight`'s own hash so `run-control`'s dump-pinning is the SAME hash
    `dump-preflight` recorded — two independently-computed hashes of the same tree would be a
    second source of truth for "did the dump change."""
    from ..preflight import _compute_content_hash
    return _compute_content_hash(dump_dir)


def _read_preflight(dump_dir: Path) -> "dict | None":
    path = dump_dir / PREFLIGHT_RECORD_NAME
    if not path.exists():
        return None
    return json.loads(path.read_text(encoding="utf-8"))


@dataclass(frozen=True)
class RunPaths:
    dump_dir: Path = DEFAULT_DUMP_DIR
    anchors_dir: Path = DEFAULT_ANCHORS_DIR
    runs_dir: Path = DEFAULT_RUNS_DIR
    family_assignments: Path = DEFAULT_FAMILY_ASSIGNMENTS

    @property
    def current_record_path(self) -> Path:
        return self.runs_dir / CURRENT_RECORD_NAME

    @property
    def pause_sentinel_path(self) -> Path:
        return self.runs_dir / PAUSE_SENTINEL_NAME


def _write_species_entry(
    row: Mapping[str, Any], merged_fields: "dict[str, Any]", *, dump_hash: str,
    families: "dict[str, list[str]]", anchors_dir: Path,
    existing_by_file: "dict[str, list[dict]]",
    votes: "dict[str, Any] | None" = None, pipeline_attempts: "dict[str, int] | None" = None,
) -> str:
    """Writes/updates the one family file this species belongs to, returns the relative path
    written (for the index). Merges into whatever that file already holds — sibling species in
    the same family file are never dropped by writing one more.

    `votes`/`pipeline_attempts` come from `orchestrator.run_one_species`'s `_votes`/
    `_pipelineAttempts` — real disagreement/repair signal, not left at the provenance dataclass's
    empty-dict defaults (found live, 2026-09-02: every real anchor written before this had
    `attempts: {}` / `confidence: {}`, silently discarding the exact data `pipeline-health`'s
    disagreement-rate/repair-rate metrics (T2.12) need to work at all)."""
    species_id = row["speciesId"]
    family = _family_for(species_id, families)
    rel_path = f"{row['side']}/{family}.json"

    votes = votes or {}
    provenance = AnchorProvenance(
        dump_hash=dump_hash, prompt_versions=dict(PROMPT_VERSIONS),
        basis=merged_fields.get("basis", "blocked"),
        confidence={field: v["confidence"] for field, v in votes.items()},
        minority_values={field: v["minority"] for field, v in votes.items() if v.get("minority")},
        attempts=dict(pipeline_attempts or {}),
        emitted_utc=time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()))
    entry = entry_for(species_id, merged_fields, provenance=provenance)

    bucket = existing_by_file.setdefault(rel_path, [])
    bucket[:] = [e for e in bucket if e.get("speciesId") != species_id]
    bucket.append(entry)
    write_family_file(anchors_dir / rel_path, bucket)
    return rel_path


def _rewrite_index(anchors_dir: Path, existing_by_file: "dict[str, list[dict]]") -> None:
    index = build_index(existing_by_file)
    (anchors_dir / "_index.json").write_bytes(render_index(index))


def _resolve_config(config: "LlmCallerConfig | None") -> LlmCallerConfig:
    """`None` means "use the project's own settings" — `load_config()`'s own layering
    (`.env` overrides `seedsmith.toml` overrides `LlmCallerConfig`'s built-in defaults), read
    fresh on every call so an edited `.env`/`seedsmith.toml` takes effect on the next `start`/
    `resume` without needing a code change or a restart of anything but the CLI invocation."""
    return config if config is not None else load_config()


def start(
    selector: Mapping[str, Any], *, paths: RunPaths = RunPaths(),
    call: "Callable[..., str] | None" = None, progress: "Progress | None" = None,
    force_selector_ignores_existing: bool = False, config: "LlmCallerConfig | None" = None,
) -> RunRecord:
    """`run start <selector>` (spec §2 `start`, §4 selectors). Refuses without a matching, real
    (non-`--skip-model`) preflight record for the CURRENT dump. On success, classifies every
    selected species not already present in the anchor tree (unless
    `force_selector_ignores_existing`, which is what `rerun`/`overwrite-all` pass), writing each
    species' family file and the run record after every single species."""
    dump_hash = _compute_dump_hash(paths.dump_dir)
    preflight = _read_preflight(paths.dump_dir)
    existing_record = read_record(paths.current_record_path)
    ok, reason = can_start(preflight, dump_hash=dump_hash, existing_record=existing_record)
    if not ok:
        raise RunRefused(reason)

    rows = _species_rows(paths.dump_dir)
    demon_dump = load_demon_dump_ctx(paths.dump_dir)
    if demon_dump is None:
        raise RunRefused(f"no readable corpus-dump tree at {paths.dump_dir}")
    seed_by_species = {s.side + ":" + str(s.type_id): s for s in demon_dump.seeds}
    threat_tuning = ThreatTuning.load()

    existing_anchors = _load_existing_anchors(paths.anchors_dir)
    already_done = {a["speciesId"] for a in existing_anchors} if not force_selector_ignores_existing else set()

    ids = resolve_selector(
        selector, dump_species=[{"speciesId": r["speciesId"], "side": r["side"]} for r in rows],
        anchors=existing_anchors, current_dump_hash=dump_hash,
        current_prompt_versions=PROMPT_VERSIONS)
    ids = [i for i in ids if i not in already_done]

    record = RunRecord(
        run_id=new_run_id(), state="idle", preflight=dict(preflight), dump_hash=dump_hash,
        selector=dict(selector), prompt_versions=dict(PROMPT_VERSIONS), pid=os.getpid(),
        started_utc=time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()))
    return _run_loop(record, ids, rows, seed_by_species, threat_tuning, paths=paths, call=call,
                     progress=progress, config=_resolve_config(config))


def resume(*, paths: RunPaths = RunPaths(), call: "Callable[..., str] | None" = None,
          progress: "Progress | None" = None, config: "LlmCallerConfig | None" = None) -> RunRecord:
    """`run resume` (spec §2 `resume`, TRANSIENT semantics — no species already in `completed` is
    ever re-classified). Also the crash-recovery path (spec §6): a `running` record whose pid is
    dead is resumable, never a silent takeover and never a permanent refusal."""
    record = read_record(paths.current_record_path)
    if record is None:
        raise RunRefused("no in-progress run to resume — use `start`")
    dump_hash = _compute_dump_hash(paths.dump_dir)
    ok, reason = can_resume(record, current_dump_hash=dump_hash)
    if not ok:
        raise RunRefused(reason)
    if record.state == "running" and is_process_alive(record.pid):
        raise RunRefused(f"run {record.run_id} is still running (pid {record.pid})")

    if record.state == "running":
        # spec §6: "A killed process leaves the record in `running`... offers `resume`... it does
        # not silently take over." We already proved above the recorded pid is dead — this IS the
        # crash-recovery case, semantically identical to `failed`, so it takes the same path.
        # `machine.transition` has no `("running", "resume")` edge on purpose (a LIVE "running"
        # record must never be resumed out from under its own process) — bypassing it here is
        # deliberate, not a shortcut around the state machine.
        record.state = "running"
    else:
        verb = "resume" if record.state in ("paused", "failed") else None
        if verb is None:
            raise RunRefused(f"run {record.run_id} is in state {record.state!r}, not resumable")
        record.state = transition(record.state, verb)
    record.pid = os.getpid()

    rows = _species_rows(paths.dump_dir)
    demon_dump = load_demon_dump_ctx(paths.dump_dir)
    if demon_dump is None:
        raise RunRefused(f"no readable corpus-dump tree at {paths.dump_dir}")
    seed_by_species = {s.side + ":" + str(s.type_id): s for s in demon_dump.seeds}
    threat_tuning = ThreatTuning.load()

    full_selection = resolve_selector(
        record.selector, dump_species=[{"speciesId": r["speciesId"], "side": r["side"]} for r in rows],
        anchors=_load_existing_anchors(paths.anchors_dir), current_dump_hash=dump_hash,
        current_prompt_versions=PROMPT_VERSIONS)
    remaining = [i for i in full_selection if i not in set(record.completed) and i not in set(record.failed)]
    if paths.pause_sentinel_path.exists():
        paths.pause_sentinel_path.unlink()
    return _run_loop(record, remaining, rows, seed_by_species, threat_tuning, paths=paths, call=call,
                     progress=progress, resuming=True, config=_resolve_config(config))


def rerun(
    selector: Mapping[str, Any], *, paths: RunPaths = RunPaths(),
    call: "Callable[..., str] | None" = None, progress: "Progress | None" = None,
    config: "LlmCallerConfig | None" = None,
) -> RunRecord:
    """`run rerun <selector>` (spec §2 `rerun`) — re-generates a named subset, ignoring "already
    emitted." Unlike `start`, a species the selector names is classified again even if an anchor
    entry already exists for it (still refuses on a missing/mismatched preflight, same as `start`
    — a rerun is still a real run and needs the same gate)."""
    return start(selector, paths=paths, call=call, progress=progress, config=config,
                force_selector_ignores_existing=True)


def overwrite_all(
    confirm_token: str, *, paths: RunPaths = RunPaths(),
    call: "Callable[..., str] | None" = None, progress: "Progress | None" = None,
    config: "LlmCallerConfig | None" = None,
) -> RunRecord:
    """`run overwrite-all --confirm <token>` (spec §2 `overwrite-all`, §5: "discards work that cost
    14 hours... gets a typed token"). The token is derived from the CURRENT dump hash
    (`record.overwrite_all_token`) so a stale token from a prior dump snapshot cannot authorize
    overwriting today's — call `status`/read the refusal message to get the right one."""
    dump_hash = _compute_dump_hash(paths.dump_dir)
    ok, reason = can_overwrite_all(confirm_token, dump_hash=dump_hash)
    if not ok:
        raise RunRefused(reason)
    return start({"kind": "all"}, paths=paths, call=call, progress=progress, config=config,
                force_selector_ignores_existing=True)


def request_pause(*, paths: RunPaths = RunPaths()) -> None:
    """`run pause` — a SEPARATE process signals the running one via a sentinel file, polled only
    between species (never mid-species, spec §2's own warning). Does nothing to a run that is not
    currently `running`; the sentinel is inert until a loop is polling it."""
    paths.runs_dir.mkdir(parents=True, exist_ok=True)
    paths.pause_sentinel_path.write_text(time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()), encoding="utf-8")


def cancel(*, paths: RunPaths = RunPaths()) -> RunRecord:
    """`run cancel` — stops and marks the run terminal (spec §2: "emitted species stay, they are
    already valid seeds"). Unlike `pause`, cancel does not expect the loop to still be alive; it
    marks the LAST WRITTEN record terminal directly, since a cancel is meant to work even against
    a dead process (matching `record.py`'s own crash-recovery discipline)."""
    record = read_record(paths.current_record_path)
    if record is None:
        raise RunRefused("no in-progress run to cancel")
    if record.state not in ("running", "paused"):
        raise RunRefused(f"run {record.run_id} is in state {record.state!r}, not cancellable")
    record.state = transition(record.state, "cancel")
    record.updated_utc = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    write_record(record, paths.current_record_path)
    _commit_completed_record(record, paths)
    if paths.pause_sentinel_path.exists():
        paths.pause_sentinel_path.unlink()
    return record


def status(*, paths: RunPaths = RunPaths()) -> "dict[str, Any]":
    """`run status` — state, progress, and an ETA derived from `callsMade` (spec's own Commands
    section). Reads only the record; makes no model call and touches no anchor file."""
    record = read_record(paths.current_record_path)
    if record is None:
        return {"state": "idle", "message": "no in-progress or paused run"}
    elapsed = None
    try:
        started = time.strptime(record.started_utc, "%Y-%m-%dT%H:%M:%SZ")
        elapsed = time.mktime(time.gmtime()) - time.mktime(started)
    except ValueError:
        pass
    return {
        "runId": record.run_id, "state": record.state, "completed": len(record.completed),
        "failed": len(record.failed), "callsMade": record.calls_made,
        "processAlive": is_process_alive(record.pid), "elapsedSec": elapsed,
    }


def _commit_completed_record(record: RunRecord, paths: RunPaths) -> None:
    """Terminal states get a COMMITTED record (spec §3: "committed only for completed runs") —
    written to the tracked `_runs/<runId>.json` in addition to the gitignored in-progress one, and
    the in-progress pointer is cleared so a later `status`/`start` sees a clean slate."""
    from .machine import is_terminal
    if not is_terminal(record.state):
        return
    write_record(record, paths.runs_dir / f"{record.run_id}.json")
    try:
        paths.current_record_path.unlink()
    except FileNotFoundError:
        pass


def _run_loop(
    record: RunRecord, ids: "list[str]", rows: "list[dict]", seed_by_species: "dict[str, Any]",
    threat_tuning: ThreatTuning, *, paths: RunPaths, call: "Callable[..., str] | None",
    progress: "Progress | None", resuming: bool = False, config: "LlmCallerConfig | None" = None,
) -> RunRecord:
    config = _resolve_config(config)
    by_species = {r["speciesId"]: r for r in rows}
    families = _load_families(paths.family_assignments)
    existing_by_file: "dict[str, list[dict]]" = {}
    for entry in _load_existing_anchors(paths.anchors_dir):
        family = _family_for(entry.get("speciesId", ""), families)
        side = entry.get("side", "unclassified")
        existing_by_file.setdefault(f"{side}/{family}.json", []).append(entry)

    if not resuming:
        record.state = transition(record.state, "start")
    paths.runs_dir.mkdir(parents=True, exist_ok=True)
    write_record(record, paths.current_record_path)

    total = len(ids) + len(record.completed)
    try:
        for species_id in ids:
            if paths.pause_sentinel_path.exists():
                paths.pause_sentinel_path.unlink()
                record.state = transition(record.state, "pause")
                write_record(record, paths.current_record_path)
                return record

            row = by_species.get(species_id)
            if row is None:
                record.failed.append(species_id)
                write_record(record, paths.current_record_path)
                continue

            seed = seed_by_species.get(row["side"] + ":" + str(row["typeId"]))
            basis = seed.basis if seed else "blocked"
            threat_rung = None
            if basis in ("observed", "stated") and seed is not None:
                rung = classify_threat(seed, threat_tuning)
                if rung is not None:
                    threat_rung = (rung.id, rung.rung)
            if threat_rung is None and basis in ("observed", "stated"):
                basis = "blocked"  # no computable score — the audited-rung pipeline needs one

            lore = _lore_for(row)
            try:
                merged = run_one_species(species_id, lore, basis=basis, threat_rung=threat_rung,
                                         call=call, config=config)
            except Exception as e:  # noqa: BLE001 — a dead species falls into `failed`, never aborts the run
                record.failed.append(species_id)
                record.calls_made += 1
                write_record(record, paths.current_record_path)
                if progress:
                    progress(species_id, len(record.completed) + len(record.failed), total)
                continue

            merged["speciesId"] = species_id
            merged["side"] = row["side"]
            merged["gameTypeId"] = row.get("typeId")
            merged["basis"] = basis
            merged.pop("_pipelineOutcomes", None)
            species_votes = merged.pop("_votes", None)
            species_attempts = merged.pop("_pipelineAttempts", None)
            species_calls_made = merged.pop("_callsMade", len(PIPELINES))

            # DERIVED fields (spec-classify-pipelines.md §4) — never authored by a model, computed
            # here from what the pipelines DID decide. Nothing else in the codebase calls these
            # (confirmed by grep before wiring this in) — this loop is their one real caller.
            if "aptitudePrimary" in merged:
                merged["posture"] = derive_posture(merged["aptitudePrimary"])
                merged["pure"] = derive_pure(merged["aptitudePrimary"], merged.get("aptitudeSecondary", "none"))
            if "variants" in merged and "rarity" in merged:
                merged["variants"] = clamp_variant_count(merged["variants"], merged["rarity"])

            _write_species_entry(
                row, merged, dump_hash=record.dump_hash, families=families,
                anchors_dir=paths.anchors_dir, existing_by_file=existing_by_file,
                votes=species_votes, pipeline_attempts=species_attempts)
            _rewrite_index(paths.anchors_dir, existing_by_file)

            record.completed.append(species_id)
            record.calls_made += species_calls_made
            record.updated_utc = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
            write_record(record, paths.current_record_path)
            if progress:
                progress(species_id, len(record.completed) + len(record.failed), total)
    except BaseException:
        record.state = "failed"
        write_record(record, paths.current_record_path)
        raise

    record.state = transition(record.state, "complete")
    write_record(record, paths.current_record_path)
    _commit_completed_record(record, paths)
    return record
