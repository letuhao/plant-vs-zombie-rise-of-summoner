"""seedsmith.adapters.items.materialgen.run — the run plan and the batch driver, wired to
`pipeline.run_ledger.RunLedger` (`generator-harness`, spec-generator-harness.md).

⛔ **Two different "already exists" questions, and the ledger only answers one of them.** The corpus
file (`data/seed/items/materials/materials.json`) already carries hand-authored content for 17 of
the 27 issuable ids — content this generator did not produce and has no ledger record of. Those are
never touched: `plan_run` only ever proposes an id that is EITHER missing from the corpus entirely,
OR present but ledger-managed and no longer valid (a hand edit broke a row this generator produced —
`RunLedger.plan`'s own stated reconcile purpose). An id with real content and no ledger row is left
alone regardless of what `is_valid` would say, because "no ledger row" there means "not ours to
touch," not "not yet done."

**The explicit `--overwrite <id>` path is different on purpose.** `plan_overwrite` bypasses all of
that via `RunLedger.force` — an explicit, named request always wins, on any issuable id, ledger-
managed or not. Never a bare, unscoped overwrite (`force`'s own boundary): a caller passes the exact
ids to replace.
"""
from __future__ import annotations

import json
import os
import tempfile
from dataclasses import dataclass, field
from pathlib import Path

from seedsmith.pipeline.run_ledger import RunLedger

from . import brief as brief_mod
from . import emit
from . import vocab
from ..setgen.answers import schema_defects
from .schema import material_schema, tag_axis_violations

REPO_ROOT = Path(__file__).resolve().parents[6]
MATERIALS_PATH = REPO_ROOT / "data" / "seed" / "items" / "materials" / "materials.json"
DEFAULT_LEDGER_PATH = REPO_ROOT / "data" / "seed" / "items" / "_runs" / "materials-gen.ledger.json"


@dataclass(frozen=True)
class Subject:
    """One unit of work: one material id, already confirmed against the closed vocabulary."""

    subject_id: str            # the runtime id, e.g. "shard.chaff" — never a fabricated id
    material: vocab.MaterialId
    brief: str


@dataclass
class RunPlan:
    subjects: "list[Subject]"
    already_present: "list[str]" = field(default_factory=list)

    @property
    def complete(self) -> bool:
        return not self.subjects


def load_entries(path: "Path | None" = None) -> "list[dict]":
    materials_path = path or MATERIALS_PATH
    if not materials_path.exists():
        return []
    doc = json.loads(materials_path.read_text(encoding="utf-8"))
    return list(doc.get("entries") or [])


def _existing_by_runtime_id(entries: "list[dict]") -> "dict[str, dict]":
    return {e["runtimeId"]: e for e in entries if isinstance(e.get("runtimeId"), str)}


def _row_still_matches(by_runtime: "dict[str, dict]"):
    """The `is_valid` callback `RunLedger.plan` calls for every subject its own ledger already
    claims done: true only if the corpus still carries a row for that runtime id whose `id` is the
    exact one this generator minted. False for "row gone" (hand-deleted) and for "row present but
    its `id` changed" (hand-edited) alike — both are the reconcile case."""
    def is_valid(subject_id: str, ledger_entry: dict) -> bool:
        current = by_runtime.get(subject_id)
        return current is not None and current.get("id") == ledger_entry.get("id")
    return is_valid


def plan_run(*, materials_path: "Path | None" = None, ledger: "RunLedger | None" = None) -> RunPlan:
    """Resume/append/reconcile — never overwrite by default. See the module docstring for exactly
    which ids this proposes."""
    entries = load_entries(materials_path)
    have = _existing_by_runtime_id(entries)
    ledger = ledger or RunLedger(DEFAULT_LEDGER_PATH)
    done = ledger.read_done()

    all_ids = [m.runtime_id for m in vocab.ISSUABLE]
    needing_work = set(ledger.plan(all_ids, _row_still_matches(have)))

    subjects = [
        Subject(subject_id=m.runtime_id, material=m, brief=brief_mod.build_material_brief(m))
        for m in vocab.ISSUABLE
        if m.runtime_id in needing_work
        # A row with real content and no ledger record is hand-authored, pre-dating this
        # generator — never ours to regenerate just because `is_valid` was never asked about it.
        and not (m.runtime_id in have and m.runtime_id not in done)
    ]
    return RunPlan(subjects=subjects, already_present=sorted(have))


def plan_overwrite(ids: "list[str]", *, ledger: "RunLedger | None" = None) -> RunPlan:
    """The explicit `--overwrite <id>` path. Every id must be issuable — a typo or a fabricated id
    fails loudly here rather than silently authoring nothing (or, worse, authoring something)."""
    if not ids:
        raise ValueError("plan_overwrite requires at least one id — an unscoped overwrite is refused")
    ledger = ledger or RunLedger(DEFAULT_LEDGER_PATH)
    forced_ids = ledger.force(ids, scope="ids")
    materials = [vocab.require_issuable(i) for i in forced_ids]
    subjects = [
        Subject(subject_id=m.runtime_id, material=m, brief=brief_mod.build_material_brief(m))
        for m in materials
    ]
    return RunPlan(subjects=subjects, already_present=[])


@dataclass(frozen=True)
class Outcome:
    subject_id: str
    outcome: str            # "persisted" | "blocked" | "refused" | "missing_answer"
    defects: "tuple[str, ...]" = ()


@dataclass(frozen=True)
class BatchResult:
    outcomes: "tuple[Outcome, ...]"
    entries: "tuple[dict, ...]"          # the entries this batch actually persisted
    materials_path: Path

    @property
    def persisted(self) -> "tuple[str, ...]":
        return tuple(o.subject_id for o in self.outcomes if o.outcome == "persisted")

    def to_dict(self) -> dict:
        return {
            "outcomes": [{"subjectId": o.subject_id, "outcome": o.outcome,
                         "defects": list(o.defects)} for o in self.outcomes],
            "persisted": list(self.persisted),
        }


def _validate_answer(answer: dict) -> "list[str]":
    if "blocked" in answer:
        return []
    defects = schema_defects(answer, material_schema())
    defects.extend(tag_axis_violations(answer.get("tags") or ()))
    return defects


def _write_entries(entries: "list[dict]", path: Path, *, base_doc: "dict | None" = None) -> Path:
    """Atomic replace, mirroring `RunLedger.write_done`'s own temp-file-then-`os.replace` discipline
    so a killed process leaves either the old corpus file or the new one, never half of one."""
    path.parent.mkdir(parents=True, exist_ok=True)
    doc = dict(base_doc) if base_doc is not None else {"schemaVersion": 1, "kind": "material"}
    doc["entries"] = entries
    payload = json.dumps(doc, ensure_ascii=False, indent=2) + "\n"
    handle, tmp_name = tempfile.mkstemp(dir=str(path.parent), suffix=".tmp")
    try:
        with os.fdopen(handle, "w", encoding="utf-8") as fh:
            fh.write(payload)
        os.replace(tmp_name, path)
    except BaseException:
        Path(tmp_name).unlink(missing_ok=True)
        raise
    return path


def run_batch(plan: RunPlan, answers: "dict[str, dict]", *,
             materials_path: "Path | None" = None,
             ledger: "RunLedger | None" = None) -> BatchResult:
    """Validate each subject's authored answer, mint its entry, and persist.

    `answers` is a plain `{subject_id: draft}` mapping — the model-call transport is out of this
    module's scope (the brief/schema pair is what any transport needs; wiring a live endpoint is a
    separate, later concern the same way `setgen`'s own graph wiring landed after its brief/schema/
    emit modules did).

    Upsert, not append-only: a subject already carrying a corpus row (only reachable here via
    `plan_run`'s reconcile branch or `plan_overwrite`'s explicit request — never via an untouched
    hand-authored row, which never becomes a `Subject` in the first place) REPLACES that row in
    place rather than appending a duplicate.
    """
    materials_path = materials_path or MATERIALS_PATH
    ledger = ledger or RunLedger(DEFAULT_LEDGER_PATH)

    doc: "dict" = {"schemaVersion": 1, "kind": "material"}
    if materials_path.exists():
        doc = json.loads(materials_path.read_text(encoding="utf-8"))
    entries: "list[dict]" = list(doc.get("entries") or [])
    by_runtime = _existing_by_runtime_id(entries)

    outcomes: "list[Outcome]" = []
    persisted: "list[dict]" = []

    for subject in plan.subjects:
        answer = answers.get(subject.subject_id)
        if answer is None:
            outcomes.append(Outcome(subject.subject_id, "missing_answer"))
            continue
        if answer.get("blocked"):
            outcomes.append(Outcome(subject.subject_id, "blocked", (str(answer["blocked"]),)))
            continue
        defects = _validate_answer(answer)
        if defects:
            outcomes.append(Outcome(subject.subject_id, "refused", tuple(defects)))
            continue

        existing = by_runtime.get(subject.subject_id)
        seq = int(existing["id"].rsplit(".", 1)[-1]) if existing is not None else emit.next_seq(entries)
        entry = emit.build_entry(subject.material, answer, seq=seq)

        if existing is not None:
            entries = [entry if e is existing else e for e in entries]
        else:
            entries.append(entry)
        by_runtime[subject.subject_id] = entry
        persisted.append(entry)
        ledger.mark_done(subject.subject_id, entry)
        outcomes.append(Outcome(subject.subject_id, "persisted"))

    if persisted:
        _write_entries(entries, materials_path, base_doc=doc)

    return BatchResult(outcomes=tuple(outcomes), entries=tuple(persisted), materials_path=materials_path)
