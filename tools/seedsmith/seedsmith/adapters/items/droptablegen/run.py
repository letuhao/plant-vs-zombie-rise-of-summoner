"""seedsmith.adapters.items.droptablegen.run — the run plan, `RunLedger` wiring, and the partition
file writer for `drop-tables-gen` (docs/architecture/item-seedgen/spec-drop-tables-gen.md).

Mirrors `basetypegen.run`'s own docstring: this module assembles a batch deterministically and
hands back a plan a caller executes against an `answer_fn` / `call`. The model call itself is not
made here -- that is the shared `workflow`/`Pipeline` graph's job. Resume/reconcile/overwrite go
through `pipeline.run_ledger.RunLedger` (module 1, `generator-harness`), never a second mechanism.

**One ledger, one of the four frozen partition slots per run** -- `droptable-draw-d{slot}-{i:03d}`
subject ids, mirroring `basetypegen.run`'s own "open-ended draw slots scoped per partition"
discipline, since a drop table is authored content with no closed universe the way a gem's affix
family or a base type's role+frame pair are.
"""
from __future__ import annotations

import argparse
import json
import os
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

from . import brief as brief_mod
from . import emit as emit_mod
from . import schema as schema_mod
from . import tuning
from ....pipeline.run_ledger import RunLedger

REPO_ROOT = tuning.REPO_ROOT
DEFAULT_LEDGER_PATH = REPO_ROOT / "data" / "seed" / "items" / "_runs" / \
    "drop-tables-gen.ledger.json"


def _partition_file(slot: int, *, drop_tables_dir: "Path | None" = None) -> Path:
    directory = drop_tables_dir or tuning.DROP_TABLES_DIR
    return directory / f"d{slot}.json"


def load_existing(slot: int, *, drop_tables_dir: "Path | None" = None) -> "dict[str, dict]":
    p = _partition_file(slot, drop_tables_dir=drop_tables_dir)
    if not p.exists():
        return {}
    doc = json.loads(p.read_text(encoding="utf-8"))
    return {e["id"]: e for e in doc.get("entries", [])}


def _draw_prefix(slot: int) -> str:
    return f"droptable-draw-d{slot}-"


@dataclass(frozen=True)
class Subject:
    subject_id: str
    seq: int
    plan: "brief_mod.RowSlotPlan"
    brief: str
    schema: dict


@dataclass(frozen=True)
class RunPlan:
    slot: int
    subjects: "tuple[Subject, ...]"
    existing: "dict[str, dict]"

    @property
    def complete(self) -> bool:
        return True  # open-ended content: a plan for N new draws is always "complete" for that N

    def summary(self) -> dict:
        return {"slot": self.slot, "toGenerate": len(self.subjects),
                "existingEntries": len(self.existing)}


def _seq_from_subject(subject_id: str, prefix: str) -> "int | None":
    if not subject_id.startswith(prefix):
        return None
    suffix = subject_id[len(prefix):]
    return int(suffix) if suffix.isdigit() else None


def _next_draw_index(done: "dict[str, dict]", prefix: str) -> int:
    indices = [i for sid in done if (i := _seq_from_subject(sid, prefix)) is not None]
    return (max(indices) + 1) if indices else 0


def is_valid(_subject_id: str, entry: dict, *, existing: "dict[str, dict]") -> bool:
    """The reconcile half `RunLedger.plan` exists for: a ledger row counts as done only if its
    recorded table id still exists in the CURRENT on-disk partition file AND still carries the same
    `name` -- a hand edit that renamed or deleted the table resurfaces the draw as needing work."""
    table_id = entry.get("entryId")
    if not table_id or table_id not in existing:
        return False
    return existing[table_id].get("name") == entry.get("name")


def plan_run(*, slot: int, count: int, ledger: RunLedger,
            drop_tables_dir: "Path | None" = None, theme_hint: str = "") -> RunPlan:
    if slot not in tuning.LEGAL_SLOTS:
        raise ValueError(f"slot must be one of {tuning.LEGAL_SLOTS}, got {slot}")
    if count < 1:
        raise ValueError(f"count must be >= 1, got {count}")

    existing = load_existing(slot, drop_tables_dir=drop_tables_dir)
    done = ledger.read_done()
    prefix = _draw_prefix(slot)
    start_draw = _next_draw_index(done, prefix)
    start_entry_seq = emit_mod.next_seq(tuple(existing), slot)

    subjects = []
    for i in range(count):
        draw_index = start_draw + i
        # seq_index for the row-slot rotation is the TABLE's own minted sequence position, so two
        # different partition slots (which start their own entry-seq counters independently) still
        # each rotate deterministically over their own history rather than colliding on index 0.
        seq_index = start_entry_seq + i - 1
        b = brief_mod.build_drop_table_brief(seq_index, theme_note=theme_hint)
        subjects.append(Subject(
            subject_id=f"{prefix}{draw_index:03d}", seq=start_entry_seq + i,
            plan=b.plan, brief=b.render(), schema=dict(b.schema)))

    return RunPlan(slot=slot, subjects=tuple(subjects), existing=existing)


def resolve_answer(answer: dict, *, plan: "brief_mod.RowSlotPlan", table_id: str) -> "dict | None":
    """One model answer -> one finished drop-table entry dict, or `None` if the model set
    `blocked`. Re-validates the answer's own structure first ("validate before accept",
    `pipeline.model`'s guardrail 5) -- `assemble_entry`'s checks are defense in depth against an
    illegal VALUE, not a substitute for this structural pass."""
    if answer.get("blocked"):
        return None
    errors = schema_mod.validate_answer(
        answer, row_count=plan.row_count, drop_band_enum=plan.drop_band_enum,
        rarity_ids=plan.rarity_ids, offer_rarity_floor=True)
    if errors:
        raise ValueError(f"invalid drop-table answer: {'; '.join(errors)}")
    return emit_mod.assemble_entry(
        table_id=table_id, name=answer["name"], drop_bands=list(answer["rowBands"]),
        equipment_slots=list(plan.equipment_slots), material_refs=list(plan.material_refs),
        consumable_refs=list(plan.consumable_refs), gem_refs=list(plan.gem_refs),
        rarity_floor=answer.get("rarityFloor"), legal_role_frames=plan.legal_role_frames,
        legal_material_refs=plan.legal_material_refs,
        legal_consumable_refs=plan.legal_consumable_refs, legal_gem_refs=plan.legal_gem_refs,
        legal_drop_bands=plan.drop_band_enum, legal_rarity_ids=plan.rarity_ids,
    )


def run_draws(plan: RunPlan, *, ledger: RunLedger,
             call: "Callable[[str, dict], dict]") -> "tuple[dict[str, dict], dict[str, dict]]":
    """Executes every subject in `plan`, marking each resolved draw done in `ledger` as it
    completes -- not all-or-nothing, mirroring `basetypegen.run.run_draws`."""
    fresh: "dict[str, dict]" = {}
    blocked: "dict[str, dict]" = {}

    for subject in plan.subjects:
        answer = call(subject.brief, subject.schema)
        if answer.get("blocked"):
            blocked[subject.subject_id] = {"reason": answer["blocked"]}
            continue
        table_id = emit_mod.mint_table_id(plan.slot, subject.seq)
        entry = resolve_answer(answer, plan=subject.plan, table_id=table_id)
        if entry is None:
            blocked[subject.subject_id] = {"reason": "no reason given"}
            continue
        fresh[entry["id"]] = entry
        ledger.mark_done(subject.subject_id, {"entryId": entry["id"], "name": entry["name"]})

    return fresh, blocked


def write_corpus(slot: int, fresh: "dict[str, dict]", *, existing: "dict[str, dict] | None" = None,
                 drop_tables_dir: "Path | None" = None, model: str = "seedsmith-droptablegen",
                 authored_utc: str = "") -> Path:
    """Merges `fresh` into the existing `dN.json` and writes it back -- additive only, matching
    the spec's acceptance #3 ('through generator-harness's ledger, into the existing d1..d4.json
    corpus shape'). `_meta` is preserved verbatim when the file already exists, matching
    `basetypegen.run.write_corpus`'s own discipline."""
    p = _partition_file(slot, drop_tables_dir=drop_tables_dir)
    ex = existing if existing is not None else load_existing(slot, drop_tables_dir=drop_tables_dir)
    merged = {**ex, **fresh}
    entries = [merged[k] for k in sorted(merged)]
    if p.exists():
        doc = json.loads(p.read_text(encoding="utf-8"))
        doc["entries"] = entries
    else:
        doc = {
            "schemaVersion": 1, "kind": "drop-table",
            "_meta": {
                "partition": f"drop-tables/{slot}", "contractVersion": 1, "exemplarVersion": 1,
                "promptVersion": 1, "batch": f"drop-tables-{slot}-droptablegen", "model": model,
                "authoredUtc": authored_utc, "sourceRef": "docs/architecture/item/entry-shapes.md#9",
            },
            "entries": entries,
        }
    return _write_document(p, doc)


def _write_document(path: Path, doc: dict) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
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


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Author new drop tables for one frozen partition slot.")
    ap.add_argument("--slot", type=int, required=True, choices=list(tuning.LEGAL_SLOTS))
    ap.add_argument("--dry-run", action="store_true", help="assemble briefs, make no model calls")
    ap.add_argument("--count", type=int, default=1, help="how many new tables to draw this run")
    ap.add_argument("--theme", default="", help="an optional theme hint in the brief")
    ap.add_argument("--write", action="store_true", help="write the merged partition back to disk")
    ap.add_argument("--overwrite", "--force", default="",
                    help="comma-separated draw ids to regenerate, or the literal 'all' "
                         "(--force is an accepted alias, matching content-completeness-core's "
                         "own naming convention -- RunLedger.force()/generate_commander_effects.py "
                         "--force)")
    args = ap.parse_args(argv)

    ledger = RunLedger(DEFAULT_LEDGER_PATH)

    if args.dry_run:
        plan = plan_run(slot=args.slot, count=args.count, ledger=ledger, theme_hint=args.theme)
        print(json.dumps(plan.summary(), ensure_ascii=False, indent=2))
        if plan.subjects:
            print("--- sample brief ---")
            print(plan.subjects[0].brief)
        return 0

    if args.overwrite:
        done = ledger.read_done()
        ids_needing_work = ledger.force(list(done), args.overwrite) if args.overwrite == "all" \
            else ledger.force([s.strip() for s in args.overwrite.split(",") if s.strip()], "ids")
        print(json.dumps({"overwrite": ids_needing_work}, ensure_ascii=False))
        return 0

    raise SystemExit(
        "REFUSING TO RUN: no model `call` is wired into this CLI entrypoint yet -- use --dry-run "
        "to inspect the plan, or drive `plan_run`/`run_draws` directly with an injected `call`.")


if __name__ == "__main__":  # pragma: no cover - dev entrypoint
    raise SystemExit(main())
