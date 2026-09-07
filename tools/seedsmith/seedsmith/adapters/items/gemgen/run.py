"""seedsmith.adapters.items.gemgen.run — the run plan, `RunLedger` wiring, and the partition-file
writer for `sockets-gen` (docs/architecture/item-seedgen/spec-sockets-gen.md).

⚠ **What this module does and does not do**, mirroring `setgen/run.py`'s own docstring exactly:
it assembles a batch deterministically — which unauthored families are in it, the brief and the
minted id for each, and the ledger that makes an interrupted run resumable — and hands back a plan a
caller executes against an `answer_fn`. **The model call itself is not made here**: the real
end-to-end wiring is the same `workflow`/`Pipeline` graph every other seedsmith generator uses, and
this module hands it a subject list. That is what makes `--dry-run` exercise everything except the
call, and it is also what makes this module's own tests exercise the harness discipline (resume,
reconcile, overwrite) without a live model anywhere in the loop.

Resume/reconcile/overwrite go through `pipeline.run_ledger.RunLedger` (module 1,
`generator-harness`) — never a second, competing resume mechanism.
"""
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

from . import brief as brief_mod
from . import emit
from . import schema as schema_mod
from .. import registries
from ....pipeline.model import BLOCKED_FIELD
from ....pipeline.run_ledger import RunLedger

REPO_ROOT = Path(__file__).resolve().parents[6]
GEMS_DIR = REPO_ROOT / "data" / "seed" / "items" / "gems"
DEFAULT_LEDGER_PATH = REPO_ROOT / "data" / "seed" / "items" / "_runs" / "gem-gen.ledger.json"

#: g1.json and g3.json both shipped exactly 20 entries. Matched here so a new partition's size
#: doesn't stand out from its two siblings for no reason — entry-shapes.md §1 names no required
#: batch size, this is purely a "don't look different without a reason" default.
DEFAULT_BATCH_SIZE = 20


class Blocked(Exception):
    """The answer for one subject was a legal decline (`blocked`), not a defect. Reportable, never
    retried forever — the same distinction `pipeline.model.PipelineResult` draws between `blocked`
    and `escalated`."""

    def __init__(self, subject_id: str, reason: str) -> None:
        super().__init__(f"{subject_id} blocked: {reason}")
        self.subject_id = subject_id
        self.reason = reason


@dataclass(frozen=True)
class Subject:
    """One gem to author: one family, already assigned to one partition slot and one batch
    position."""

    subject_id: str          # "gem-gems/2-atom.affliction" -- family-keyed, not index-keyed, so
                              # re-sorting the pool never orphans a ledger row for an already-done
                              # family.
    partition: str           # "gems/2"
    family_id: str
    family_row: dict
    entry_id: str             # "gem.g2-001" -- minted from this subject's own batch position
    index: int                 # zero-based position within THIS batch; feeds the power-band rotation
    brief: str

    @property
    def elemental(self) -> bool:
        return brief_mod.is_elemental(self.family_row)


@dataclass(frozen=True)
class RunPlan:
    partition: str
    subjects: "list[Subject]"
    already_done: "list[str]"

    @property
    def complete(self) -> bool:
        return not self.subjects


def _subject_id(partition: str, family_id: str) -> str:
    return f"gem-{partition}-{family_id}"


def _ledger_is_valid(_subject_id: str, entry: dict) -> bool:
    """The reconcile half `RunLedger.plan` exists for: a ledger row counts as done only if it
    actually carries an assembled entry shaped like the ones this module writes. A hand-edited or
    truncated ledger row is treated as needing work again, never silently trusted."""
    return (isinstance(entry, dict) and isinstance(entry.get("entry"), dict)
           and isinstance(entry["entry"].get("id"), str) and bool(entry["entry"].get("id")))


def _build_subjects(partition: str, pool: "list[dict]", wanted_ids: "set[str]",
                    start_seq: int) -> "list[Subject]":
    subjects: "list[Subject]" = []
    for i, family_row in enumerate(pool):
        subject_id = _subject_id(partition, family_row["id"])
        if subject_id not in wanted_ids:
            continue
        entry_id = emit.mint_gem_id(partition, start_seq + i)
        subjects.append(Subject(
            subject_id=subject_id, partition=partition, family_id=family_row["id"],
            family_row=family_row, entry_id=entry_id, index=i,
            brief=brief_mod.build_gem_brief(family_row),
        ))
    return subjects


def plan_partition(partition: str, *, batch_size: int = DEFAULT_BATCH_SIZE,
                   gems_dir: "Path | None" = None, ledger: "RunLedger | dict | None" = None,
                   start_seq: int = 1) -> RunPlan:
    """Deterministic: identical on-disk state (the gems corpus AND the ledger) produces identical
    subjects, ids and order on every call. `ledger` may be a real `RunLedger` (its own `.plan()` is
    used, so reconcile runs for real) or an already-read `done` dict (tests pass one directly so
    they don't need a file on disk)."""
    directory = gems_dir or GEMS_DIR
    pool = brief_mod.unauthored_families(limit=batch_size, gems_dir=directory)
    all_ids = [_subject_id(partition, row["id"]) for row in pool]

    if isinstance(ledger, RunLedger):
        needing = set(ledger.plan(all_ids, _ledger_is_valid))
    else:
        done = ledger or {}
        needing = {sid for sid in all_ids if sid not in done or not _ledger_is_valid(sid, done[sid])}

    subjects = _build_subjects(partition, pool, needing, start_seq)
    already = [sid for sid in all_ids if sid not in needing]
    return RunPlan(partition=partition, subjects=subjects, already_done=already)


def plan_overwrite(partition: str, target: "str | list[str]", *,
                   batch_size: int = DEFAULT_BATCH_SIZE, gems_dir: "Path | None" = None,
                   ledger: "RunLedger | None" = None, start_seq: int = 1) -> "list[Subject]":
    """`target` is either the literal `"all"` or an explicit list of subject ids — mirrors
    `RunLedger.force`'s own refusal of a bare, unscoped overwrite ("a typo'd --overwrite fails
    loudly", spec-generator-harness.md's own boundary): passing anything else raises inside
    `RunLedger.force`, never silently defaults to "all"."""
    if isinstance(target, str) and target != "all":
        raise ValueError(
            f"overwrite target must be the literal 'all' or a list of subject ids, got {target!r} "
            f"-- a bare string here would silently iterate its CHARACTERS as ids, which is exactly "
            f"the 'typo'd --overwrite fails loudly' boundary this harness exists to enforce")

    directory = gems_dir or GEMS_DIR
    pool = brief_mod.unauthored_families(limit=batch_size, gems_dir=directory)
    all_ids = [_subject_id(partition, row["id"]) for row in pool]

    scope = "all" if target == "all" else "ids"
    wanted = all_ids if scope == "all" else list(target)
    ledger = ledger or RunLedger(DEFAULT_LEDGER_PATH)
    forced = set(ledger.force(wanted, scope=scope))

    return _build_subjects(partition, pool, forced, start_seq)


def apply_answer(subject: Subject, answer: dict) -> dict:
    """Validates `answer` against `schema.validate_answer`, then assembles the final entry —
    `tags` copied from the family's own authored tags, `powerBand` from `emit.resolve_power_band`,
    never from the answer. Raises `Blocked` for a legal decline, `ValueError` for a genuinely
    malformed answer."""
    if answer.get(BLOCKED_FIELD):
        raise Blocked(subject.subject_id, str(answer[BLOCKED_FIELD]))

    errors = schema_mod.validate_answer(answer, elemental=subject.elemental)
    if errors:
        raise ValueError(f"{subject.subject_id}: invalid answer: {'; '.join(errors)}")

    tags = tuple(subject.family_row.get("tags") or ())
    power_band = emit.resolve_power_band(subject.index)
    element = answer.get("element") if subject.elemental else None

    return emit.assemble_entry(
        entry_id=subject.entry_id, name_key=answer["nameKey"], name=answer["name"],
        family_id=subject.family_id, power_band=power_band, tags=tags,
        element=element, affinity_element=element,
    )


def generate_partition(partition: str, *, answer_fn: "Callable[[Subject], dict]",
                       batch_size: int = DEFAULT_BATCH_SIZE, ledger_path: "Path | None" = None,
                       gems_dir: "Path | None" = None, overwrite: "str | list[str] | None" = None,
                       dry_run: bool = False) -> "dict[str, dict]":
    """Runs one whole batch: plan (or `plan_overwrite`) -> `answer_fn` -> `apply_answer` ->
    `RunLedger.mark_done`. Returns the assembled entries produced THIS run, keyed by subject id. A
    `Blocked` subject is reported (its `Blocked` re-raised, per this module's own class) rather than
    silently retried forever — the caller decides whether one blocked subject should stop the batch.
    """
    ledger = RunLedger(ledger_path or DEFAULT_LEDGER_PATH)
    if overwrite:
        subjects = plan_overwrite(partition, overwrite, batch_size=batch_size, gems_dir=gems_dir,
                                  ledger=ledger)
    else:
        subjects = plan_partition(partition, batch_size=batch_size, gems_dir=gems_dir,
                                  ledger=ledger).subjects

    produced: "dict[str, dict]" = {}
    for subject in subjects:
        answer = answer_fn(subject)
        entry = apply_answer(subject, answer)
        produced[subject.subject_id] = entry
        if not dry_run:
            ledger.mark_done(subject.subject_id, {"entryId": subject.entry_id, "entry": entry})
    return produced


def entries_from_ledger(partition: str, *, ledger_path: "Path | None" = None) -> "list[dict]":
    """Every assembled entry the ledger already holds for `partition`, sorted by entry id — a pure
    resume/flush read, distinct from `generate_partition`'s own answer-collecting loop. This is what
    a caller uses to assemble a partition FILE from ledger rows without re-answering anything on a
    run that was interrupted after `mark_done` but before the file write."""
    ledger = RunLedger(ledger_path or DEFAULT_LEDGER_PATH)
    done = ledger.read_done()
    prefix = f"gem-{partition}-"
    rows = [row["entry"] for key, row in done.items()
           if key.startswith(prefix) and isinstance(row, dict) and isinstance(row.get("entry"), dict)]
    return sorted(rows, key=lambda e: e["id"])


def write_partition_file(partition: str, entries: "list[dict]", *, path: "Path | None" = None,
                         batch_name: "str | None" = None, model: str = "gemgen/1",
                         authored_utc: str = "2026-09-07T00:00:00Z") -> Path:
    """Writes the actual `data/seed/items/gems/g{slot}.json` — same top-level shape as the shipped
    `g1.json`/`g3.json` (`schemaVersion`, `kind`, `_meta`, `entries`), same atomic
    temp-file-then-replace discipline `RunLedger.write_done` and `setgen/run.py`'s own
    `write_ledger` both use, so a killed process leaves either the old file or the new one, never
    half of one. `registryVersions` is read fresh via `registries.load_versions()` rather than
    copied from a sibling partition's own (potentially stale) `_meta` block.
    """
    import os
    import tempfile

    slot = emit.partition_slot(partition)
    target = path or (GEMS_DIR / f"g{slot}.json")
    doc = {
        "schemaVersion": 1,
        "kind": "gem",
        "_meta": {
            "batch": batch_name or f"gems-g{slot}",
            "partition": partition,
            "contractVersion": 1,
            "registryVersions": registries.load_versions(),
            "exemplarVersion": 1,
            "promptVersion": brief_mod.PROMPT_VERSION,
            "model": model,
            "authoredUtc": authored_utc,
            "sourceRef": "entry-shapes.md#1",
        },
        "entries": entries,
    }
    target.parent.mkdir(parents=True, exist_ok=True)
    payload = json.dumps(doc, ensure_ascii=False, indent=2) + "\n"
    handle, tmp_name = tempfile.mkstemp(dir=str(target.parent), suffix=".tmp")
    try:
        with os.fdopen(handle, "w", encoding="utf-8") as fh:
            fh.write(payload)
        os.replace(tmp_name, target)
    except BaseException:
        Path(tmp_name).unlink(missing_ok=True)
        raise
    return target
