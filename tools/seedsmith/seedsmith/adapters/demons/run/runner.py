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
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable, Mapping

from ..anchor.derive import clamp_variant_count, derive_posture, derive_pure, resolve_unresolved_threat_band
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

#: `start`/`resume` concurrency lock (found live, 2026-09-02 — a real duplicate-classification bug,
#: not a hypothetical: two overlapping `resume`-driving loops both read the SAME record between one
#: species' completion and the next `resume` call, both saw it as resumable, both wrote their own
#: `pid` and independently classified the same 2 species with divergent LLM output). The pre-existing
#: `record.state == "running" and is_process_alive(record.pid)` check only catches a resume racing
#: an ALREADY-recorded live process — it is a read-then-check, not a claim, so two processes that
#: both read the record before either writes can both pass it. This is a real mutual-exclusion lock,
#: acquired for the WHOLE call (through the actual classification loop, not just the record read),
#: so the existing check stays meaningful for its own purpose (crash recovery across sessions) while
#: this closes the same-session TOCTOU gap it was never meant to cover.
RESUME_LOCK_NAME = "_resume.lock"


def _acquire_run_lock(runs_dir: Path) -> None:
    """Atomic claim (`os.O_CREAT | os.O_EXCL`, portable — a POSIX and NTFS guarantee, not a
    best-effort check) on the run record before any read-check-write sequence. A lock file left by
    a process that died mid-run is stale, not a permanent lock (spec §6's own "never refuses
    forever" rule, extended to this lock the same way it already governs the record itself) —
    reclaimed once, after confirming its own recorded holder is actually dead.
    """
    runs_dir.mkdir(parents=True, exist_ok=True)
    lock_path = runs_dir / RESUME_LOCK_NAME
    for attempt in range(2):
        try:
            fd = os.open(lock_path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
            try:
                os.write(fd, str(os.getpid()).encode("utf-8"))
            finally:
                os.close(fd)
            return
        except FileExistsError:
            if attempt == 1:
                break
            try:
                holder_pid = int(lock_path.read_text(encoding="utf-8").strip())
            except (ValueError, OSError):
                holder_pid = -1
            if is_process_alive(holder_pid):
                raise RunRefused(
                    f"another start/resume is already in progress (pid {holder_pid}) — wait for it "
                    "to finish, or confirm it is genuinely dead before removing "
                    f"{lock_path}")
            try:
                lock_path.unlink()
            except FileNotFoundError:
                pass
    raise RunRefused(f"could not acquire the run lock at {lock_path}")


def _release_run_lock(runs_dir: Path) -> None:
    try:
        (runs_dir / RESUME_LOCK_NAME).unlink()
    except FileNotFoundError:
        pass

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
    """Every real anchor entry on disk — scans the tree directly, never trusts `_index.json`'s own
    value set to know which files exist (fixed 2026-09-04, demon-corpus-self-heal C2's own real
    finding, not theorized: the OLD version only read files `_index.json` currently pointed at,
    so a species whose file the index had drifted away from — the exact class of staleness A1/A2
    exist to fix — became invisible to every future run, and `_rewrite_index` then rebuilt the
    index FROM that already-incomplete read, making the loss permanent and self-reinforcing rather
    than one-off. `_index.json` stays the fast O(1) lookup `run-control`'s own spec wants for
    `resume`; this function is the ground truth it should never drift from)."""
    if not anchors_dir.exists():
        return []
    out: "list[dict]" = []
    for path in sorted(anchors_dir.rglob("*.json"), key=lambda p: str(p)):
        if path.name.startswith("_"):
            continue  # _index.json and any future notes/exemplars file, matching AtomImporter's own convention
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


def _slugify_family(name: str) -> str:
    """Lowercase kebab-case filename from an LLM-proposed family string ("Apex Predator Flora" ->
    "apex-predator-flora"), matching the id grammar every other kebab-case id in this repo already
    uses. Collapses whitespace/punctuation runs into single hyphens; empty after stripping falls
    back to the caller's own "unclassified" default rather than writing a blank filename."""
    import re
    slug = re.sub(r"[^a-z0-9]+", "-", name.strip().lower()).strip("-")
    return slug or "unclassified"


def _family_for(
    species_id: str, families: "dict[str, list[str]]", *, classified_family: "list[str] | None" = None,
) -> str:
    """The anchor's OWN `family` field — the `identity` pipeline's real, per-species LLM proposal
    (spec-classify-pipelines.md pipeline 8) — is the primary source (fixed 2026-09-02: previously
    this only ever consulted `family-assignments.json`, a lookup built for the unrelated
    fusion-product `demon` corpus, so every base species not coincidentally sharing an id with a
    fused demon landed in the generic "unclassified" bucket despite the identity pipeline already
    having classified a real family for it — found live, 2026-09-02, T2.11's own 20-species run).
    `family-assignments.json` stays a fallback for the handful of species it happens to cover with
    a hand-tuned short name (`cherrybomb` -> `cherry`), and "unclassified" is the last resort only
    when neither source has anything."""
    if classified_family:
        first = next((f for f in classified_family if f and f.strip()), None)
        if first is not None:
            return _slugify_family(first)
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
    merge_from: "Mapping[str, Any] | None" = None,
) -> str:
    """Writes/updates the one family file this species belongs to, returns the relative path
    written (for the index). Merges into whatever that file already holds — sibling species in
    the same family file are never dropped by writing one more.

    `votes`/`pipeline_attempts` come from `orchestrator.run_one_species`'s `_votes`/
    `_pipelineAttempts` — real disagreement/repair signal, not left at the provenance dataclass's
    empty-dict defaults (found live, 2026-09-02: every real anchor written before this had
    `attempts: {}` / `confidence: {}`, silently discarding the exact data `pipeline-health`'s
    disagreement-rate/repair-rate metrics (T2.12) need to work at all).

    `merge_from` (demon-corpus-self-heal B1, 2026-09-04): the species' EXISTING full entry, passed
    only for a pipeline-scoped rerun (`_run_loop`'s `pipeline_scope`). `merged_fields` there holds
    ONLY the reran pipeline's own output — every other field, and every other pipeline's own
    `attempts`/`confidence`/`minorityValues`/`promptVersions` entry, is carried over from
    `merge_from` untouched. `None` (the default, every other caller) keeps today's exact
    full-replace behaviour."""
    species_id = row["speciesId"]

    if merge_from is not None:
        full_fields = {k: v for k, v in merge_from.items() if not k.startswith("_")}
        full_fields.update(merged_fields)
        old_prov = dict(merge_from.get("_provenance") or {})
        confidence = dict(old_prov.get("confidence") or {})
        minority = dict(old_prov.get("minorityValues") or {})
        for field, v in (votes or {}).items():
            confidence[field] = v["confidence"]
            if v.get("minority"):
                minority[field] = v["minority"]
            else:
                minority.pop(field, None)
        attempts = dict(old_prov.get("attempts") or {})
        attempts.update(dict(pipeline_attempts or {}))
        prompt_versions = dict(old_prov.get("promptVersions") or {})
        prompt_versions.update({p: v for p, v in PROMPT_VERSIONS.items() if p in (pipeline_attempts or {})})
        provenance = AnchorProvenance(
            dump_hash=dump_hash, prompt_versions=prompt_versions,
            basis=full_fields.get("basis", old_prov.get("basis", "blocked")),
            confidence=confidence, minority_values=minority,
            audit_verdict=old_prov.get("auditVerdict"),
            attempts=attempts,
            emitted_utc=time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()))
        merged_fields = full_fields
    else:
        votes = votes or {}
        provenance = AnchorProvenance(
            dump_hash=dump_hash, prompt_versions=dict(PROMPT_VERSIONS),
            basis=merged_fields.get("basis", "blocked"),
            confidence={field: v["confidence"] for field, v in votes.items()},
            minority_values={field: v["minority"] for field, v in votes.items() if v.get("minority")},
            attempts=dict(pipeline_attempts or {}),
            emitted_utc=time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()))

    family = _family_for(species_id, families, classified_family=merged_fields.get("family"))
    rel_path = f"{row['side']}/{family}.json"
    entry = entry_for(species_id, merged_fields, provenance=provenance)

    # A reclassification can move a species into a DIFFERENT family file (family is model-decided,
    # not stable across runs) — found live at corpus scale, 2026-09-04 (DemonQualityReport: 217 of
    # 833 species had a stale copy left behind in their OLD file). Every other file this species
    # might already live in must lose its copy too, not just the one being written now, or the
    # stale entry sits on disk forever with _index.json correctly pointing past it while
    # DemonSpeciesGen/AtomImporter-style direct-file readers still see both.
    for other_path, other_bucket in existing_by_file.items():
        if other_path == rel_path:
            continue
        before = len(other_bucket)
        other_bucket[:] = [e for e in other_bucket if e.get("speciesId") != species_id]
        if len(other_bucket) == before:
            continue
        if other_bucket:
            write_family_file(anchors_dir / other_path, other_bucket)
        else:
            (anchors_dir / other_path).unlink(missing_ok=True)  # no species left — no orphan file either

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
    workers: int = 1,
) -> RunRecord:
    """`run start <selector>` (spec §2 `start`, §4 selectors). Refuses without a matching, real
    (non-`--skip-model`) preflight record for the CURRENT dump. On success, classifies every
    selected species not already present in the anchor tree (unless
    `force_selector_ignores_existing`, which is what `rerun`/`overwrite-all` pass), writing each
    species' family file and the run record after every single species.

    `workers` (default 1, sequential — unchanged behaviour) fans the model-call phase out across
    N threads; see `_run_loop`'s own docstring for why that phase specifically is safe to
    parallelise and the write phase deliberately is not."""
    _acquire_run_lock(paths.runs_dir)
    try:
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
                         progress=progress, config=_resolve_config(config), workers=workers)
    finally:
        _release_run_lock(paths.runs_dir)


def resume(*, paths: RunPaths = RunPaths(), call: "Callable[..., str] | None" = None,
          progress: "Progress | None" = None, config: "LlmCallerConfig | None" = None,
          workers: int = 1) -> RunRecord:
    """`run resume` (spec §2 `resume`, TRANSIENT semantics — no species already in `completed` is
    ever re-classified). Also the crash-recovery path (spec §6): a `running` record whose pid is
    dead is resumable, never a silent takeover and never a permanent refusal.

    `workers` need not match the original `start`'s value — it is a resource knob for THIS process,
    not a property of the run itself, and nothing in `RunRecord` depends on it."""
    _acquire_run_lock(paths.runs_dir)
    try:
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
                         progress=progress, resuming=True, config=_resolve_config(config), workers=workers)
    finally:
        _release_run_lock(paths.runs_dir)


def rerun(
    selector: Mapping[str, Any], *, paths: RunPaths = RunPaths(),
    call: "Callable[..., str] | None" = None, progress: "Progress | None" = None,
    config: "LlmCallerConfig | None" = None, workers: int = 1,
) -> RunRecord:
    """`run rerun <selector>` (spec §2 `rerun`) — re-generates a named subset, ignoring "already
    emitted." Unlike `start`, a species the selector names is classified again even if an anchor
    entry already exists for it (still refuses on a missing/mismatched preflight, same as `start`
    — a rerun is still a real run and needs the same gate)."""
    return start(selector, paths=paths, call=call, progress=progress, config=config,
                force_selector_ignores_existing=True, workers=workers)


def overwrite_all(
    confirm_token: str, *, paths: RunPaths = RunPaths(),
    call: "Callable[..., str] | None" = None, progress: "Progress | None" = None,
    config: "LlmCallerConfig | None" = None, workers: int = 1,
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
                force_selector_ignores_existing=True, workers=workers)


def fix_unresolved(*, paths: RunPaths = RunPaths(), dry_run: bool = False) -> "list[dict[str, Any]]":
    """demon-corpus-self-heal F1 (2026-09-04) — a deliberate FIX STEP, run on demand after a
    classification pass, never automatically during `start`/`resume`/`rerun`. A human reads
    `DemonQualityReport`'s own unresolved-rate finding, then runs this to close what has a real
    answer: `resolve_unresolved_threat_band`'s own docstring covers exactly why `threatBand` is the
    ONLY field this touches — `aptitudePrimary`/`rarity`/`elementPrimary` have no equivalent
    sanctioned fallback anywhere in this repo, and stay `"unresolved"`/reported rather than guessed
    at here. No model calls, no lock needed (this never runs concurrently with a classification
    pass in practice, and re-running it is idempotent — a species already resolved is a no-op).

    Every fixed entry's `_provenance.confidence["threatBand"]` is stamped
    `"deterministic-fallback"` (never `"high"`/`"split"`) so a future reader — or this same tool's
    own report — can always tell a sanctioned default apart from a real LLM judgment; nothing here
    pretends a judgment happened. Returns one dict per species actually fixed (or that WOULD be
    fixed, when `dry_run=True`): `{"speciesId", "before", "after"}`.
    """
    threat_tuning = ThreatTuning.load()
    anchors = _load_existing_anchors(paths.anchors_dir)
    families = _load_families(paths.family_assignments)

    existing_by_file: "dict[str, list[dict]]" = {}
    for entry in anchors:
        family = _family_for(entry.get("speciesId", ""), families, classified_family=entry.get("family"))
        side = entry.get("side", "unclassified")
        existing_by_file.setdefault(f"{side}/{family}.json", []).append(entry)

    fixed: "list[dict[str, Any]]" = []
    for entry in anchors:
        before = entry.get("threatBand")
        after, was_deterministic = resolve_unresolved_threat_band(before, tuning=threat_tuning)
        if not was_deterministic:
            continue
        fixed.append({"speciesId": entry.get("speciesId"), "before": before, "after": after})
        if dry_run:
            continue

        species_id = entry.get("speciesId", "")
        row = {"speciesId": species_id, "side": entry.get("side", "unclassified")}
        # dump_hash is PRESERVED from the entry's own original classification, never re-stamped —
        # this fix never re-reads the corpus dump, so claiming a fresh dump_hash would overstate
        # what actually happened here.
        original_dump_hash = entry.get("_provenance", {}).get("dumpHash", "")
        _write_species_entry(
            row, {"threatBand": after}, dump_hash=original_dump_hash, families=families,
            anchors_dir=paths.anchors_dir, existing_by_file=existing_by_file,
            votes={"threatBand": {"confidence": "deterministic-fallback", "minority": None}},
            pipeline_attempts={},  # no pipeline ran — the old attempts record must stay untouched
            merge_from=entry)

    if not dry_run and fixed:
        _rewrite_index(paths.anchors_dir, existing_by_file)
    return fixed


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
    workers: int = 1,
) -> RunRecord:
    """`workers` (2026-09-03, real throughput finding: the model-call phase is the entire cost of
    a run and was fully serial) splits each species into two phases that run on different sides of
    one boundary:

    - `_classify_one` — the SLOW phase (all eight pipelines' model calls for one species). Reads
      only per-call-local state (`row`, `seed`, `lore`) plus read-only closures (`by_species`,
      `seed_by_species`, `threat_tuning`, `call`, `config`) and touches no shared mutable state —
      confirmed by tracing `run_one_species` → `orchestrator._invoke` → `llm_caller`'s own request
      construction (a fresh `urllib.request.Request` per call, no session/cache/counter) and
      `permute.order_for` (seeds a fresh `random.Random` per call). Safe to run on N threads at
      once — this is genuinely the only part worth parallelising.
    - `_finalize` — the FAST phase (derive posture/pure/variants, write the family file, rewrite
      the index, update and persist `record`). Still called from exactly ONE thread for every
      species, in the SAME order and with the SAME per-species-checkpoint guarantee the sequential
      path always had (spec §2: "the run record's `completed` list is rewritten to disk after
      every species") — no lock is needed around it because nothing else ever touches it
      concurrently, and no two species' writes can interleave.

    `workers=1` keeps today's exact sequential path (same call order, same checkpoint timing) —
    the branch below is not exercised and nothing about single-worker behaviour changes.
    """
    config = _resolve_config(config)
    by_species = {r["speciesId"]: r for r in rows}
    families = _load_families(paths.family_assignments)
    existing_by_file: "dict[str, list[dict]]" = {}
    for entry in _load_existing_anchors(paths.anchors_dir):
        # `classified_family=entry.get("family")` (fixed 2026-09-04, demon-corpus-self-heal A1):
        # without it, this rebuild used ONLY the external family-assignments.json fallback, which
        # for most species resolves to "unclassified" — a DIFFERENT path than the one the entry
        # actually lives at on disk (which `_write_species_entry` computes WITH the real
        # classified family). The stale-duplicate cross-file cleanup below only works if this
        # dict's keys are the species' REAL on-disk paths, not a phantom re-bucketing of them.
        family = _family_for(entry.get("speciesId", ""), families, classified_family=entry.get("family"))
        side = entry.get("side", "unclassified")
        existing_by_file.setdefault(f"{side}/{family}.json", []).append(entry)

    if not resuming:
        record.state = transition(record.state, "start")
    paths.runs_dir.mkdir(parents=True, exist_ok=True)
    write_record(record, paths.current_record_path)

    total = len(ids) + len(record.completed)

    # Pipeline-scoped rerun (demon-corpus-self-heal B1, 2026-09-04): `record.selector` carries a
    # `pipeline` key whenever the caller asked for one — whether the selector's own `kind` IS
    # "pipeline" (picks every classified species AND scopes to it) or a DIFFERENT kind narrowed
    # WHICH species while `pipeline` narrows execution for them (`cli.py`'s own
    # `_selector_from_args` attaches it either way — checked directly here rather than gated on
    # `kind`, after finding live that gating on kind=="pipeline" silently dropped a combined
    # `--species X --pipeline Y` request into a full reclassification instead of a scoped one). No
    # new RunRecord field needed either way — a paused-then-resumed scoped rerun keeps its scope
    # automatically since the selector is what persists. Redeploying a fixed prompt corpus-wide
    # used to mean either accepting the stale field forever or paying for a full reclassify (~8x
    # the calls) — this re-runs ONLY the named pipeline and merges its own fields into the existing
    # entry, every other pipeline's work untouched.
    pipeline_scope = record.selector.get("pipeline")
    existing_entry_by_id: "dict[str, dict]" = {}
    if pipeline_scope:
        for bucket in existing_by_file.values():
            for entry in bucket:
                sid = entry.get("speciesId")
                if sid:
                    existing_entry_by_id[sid] = entry

    def _classify_one(species_id: str):
        row = by_species.get(species_id)
        if row is None:
            return species_id, None, None, None, None

        existing_entry = existing_entry_by_id.get(species_id) if pipeline_scope else None
        if pipeline_scope and existing_entry is None:
            # Nothing to merge a scoped rerun's own partial output into — a scoped rerun is for
            # ALREADY-classified species only, never a backdoor first classification.
            return species_id, row, None, None, ValueError(
                f"{species_id!r} has no existing anchor entry — a pipeline-scoped rerun cannot "
                f"merge into content that was never classified; use `start`/`rerun` without "
                f"--pipeline for a first classification")

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
            if pipeline_scope:
                initial_context = {k: v for k, v in existing_entry.items() if not k.startswith("_")}
                merged = run_one_species(
                    species_id, lore, basis=basis, threat_rung=threat_rung, call=call, config=config,
                    pipelines=[pipeline_scope], initial_context=initial_context)
            else:
                merged = run_one_species(species_id, lore, basis=basis, threat_rung=threat_rung,
                                         call=call, config=config)
            return species_id, row, basis, merged, None
        except Exception as e:  # noqa: BLE001 — a dead species falls into `failed`, never aborts the run
            return species_id, row, basis, None, e

    def _finalize(species_id: str, row, basis, merged, err) -> None:
        if row is None:
            record.failed.append(species_id)
            write_record(record, paths.current_record_path)
            return
        if err is not None:
            # A real observability gap found while diagnosing demon-corpus-self-heal C2 (2026-09-04,
            # 199 real failures with no recorded reason): the RunRecord only ever stored a species
            # id in `failed`, never WHY. Printed once per failure, matching the pattern the
            # `_run_parallel` unexpected-exception backstop already established.
            print(f"seedsmith: {species_id}: {err}", file=sys.stderr)
            record.failed.append(species_id)
            record.calls_made += 1
            write_record(record, paths.current_record_path)
            if progress:
                progress(species_id, len(record.completed) + len(record.failed), total)
            return

        merged["speciesId"] = species_id
        merged["side"] = row["side"]
        merged["gameTypeId"] = row.get("typeId")
        if basis is not None:
            merged["basis"] = basis
        merged.pop("_pipelineOutcomes", None)
        species_votes = merged.pop("_votes", None)
        species_attempts = merged.pop("_pipelineAttempts", None)
        species_calls_made = merged.pop("_callsMade", 1 if pipeline_scope else len(PIPELINES))

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
            votes=species_votes, pipeline_attempts=species_attempts,
            merge_from=existing_entry_by_id.get(species_id) if pipeline_scope else None)
        _rewrite_index(paths.anchors_dir, existing_by_file)

        record.completed.append(species_id)
        record.calls_made += species_calls_made
        record.updated_utc = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
        write_record(record, paths.current_record_path)
        if progress:
            progress(species_id, len(record.completed) + len(record.failed), total)

    try:
        if workers <= 1:
            paused = False
            for species_id in ids:
                if paths.pause_sentinel_path.exists():
                    paths.pause_sentinel_path.unlink()
                    paused = True
                    break
                _finalize(*_classify_one(species_id))
        else:
            paused = _run_parallel(ids, workers, _classify_one, _finalize, paths)

        if paused:
            record.state = transition(record.state, "pause")
            write_record(record, paths.current_record_path)
            return record
    except BaseException:
        record.state = "failed"
        write_record(record, paths.current_record_path)
        raise

    record.state = transition(record.state, "complete")
    write_record(record, paths.current_record_path)
    _commit_completed_record(record, paths)
    return record


def _run_parallel(
    ids: "list[str]", workers: int,
    classify_one: "Callable[[str], tuple]", finalize: "Callable[..., None]",
    paths: RunPaths,
) -> bool:
    """Runs `classify_one` for every id across `workers` daemon threads (the slow, independent
    model-call phase) and calls `finalize` for each result ONE AT A TIME from THIS thread only (the
    fast write phase — `_run_loop`'s own docstring covers why no lock is needed for either side).

    Pause semantics match the sequential loop's own rule (spec §2: "a species half-classified... is
    not a resumable unit") extended to N workers: once the pause sentinel is seen, workers stop
    PULLING new work, but anything already in flight finishes and still gets finalized — so a pause
    with `workers=4` may complete up to 3 more species than the exact moment `pause` was requested,
    never a partial one. Every worker puts exactly one `None` sentinel on `result_q` as its last
    action (whether it stopped because the queue emptied or because `stop` was set), so the count of
    sentinels received — not a fixed total — is what proves every worker has truly exited; relying
    on a fixed result count instead would hang forever on a pause, since some queued species then
    never produce a result at all.
    """
    import queue
    import threading

    work_q: "queue.Queue[str]" = queue.Queue()
    for species_id in ids:
        work_q.put(species_id)

    result_q: "queue.Queue[tuple | None]" = queue.Queue()
    stop = threading.Event()

    def worker() -> None:
        while not stop.is_set():
            try:
                species_id = work_q.get_nowait()
            except queue.Empty:
                break
            # `classify_one` (the real one, wired from `_run_loop`) already catches everything it
            # can predict and returns the error as data. This second net is for what it CANNOT
            # predict — a genuine bug — because an uncaught exception in a worker thread does not
            # propagate to the main thread; it would kill this worker silently, its final `None`
            # sentinel would never be sent, and `_run_parallel`'s exit loop would then wait forever
            # for a sentinel that is never coming. A hang on a run meant to take hours is far worse
            # than one more species landing in `failed`.
            try:
                result_q.put(classify_one(species_id))
            except Exception as e:  # noqa: BLE001 — see above: this is the backstop, not the path
                import sys
                print(f"seedsmith: {species_id}: unexpected error in classify_one worker: {e}",
                      file=sys.stderr)
                result_q.put((species_id, None, None, None, e))
        result_q.put(None)  # this worker is done, permanently — never another result from it

    threads = [threading.Thread(target=worker, daemon=True, name=f"seedsmith-classify-{i}")
               for i in range(workers)]
    for t in threads:
        t.start()

    paused = False
    finished_workers = 0
    try:
        while finished_workers < workers:
            if not paused and paths.pause_sentinel_path.exists():
                paths.pause_sentinel_path.unlink()
                stop.set()
                paused = True
            item = result_q.get()
            if item is None:
                finished_workers += 1
                continue
            finalize(*item)
    finally:
        stop.set()
        for t in threads:
            t.join()

    return paused
