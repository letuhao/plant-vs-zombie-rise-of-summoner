"""seedsmith.adapters.items.basetypegen.run — the run plan, the ledger wiring, and the dry run.

Wires `pipeline.run_ledger.RunLedger` for the spec's own acceptance #2 ("Output writes to the
existing corpus path ... through generator-harness's ledger — append+reconcile by default, explicit
`--overwrite` for a full redo") and testing strategy ("Harness tests: resume/reconcile/overwrite").

**One ledger, every partition.** A `(role, frame, band)` triple is not a closed grid the way
`setgen`'s themes or `affixfamgen`'s partitions are pre-declared — a caller may run this against any
of the 30 role-frames × however many bands exist, so subject ids are open-ended DRAW SLOTS scoped
per partition, mirroring `milestonegen.run`'s own `milestone-draw-{i:03d}` discipline rather than a
fixed subject list.

`is_valid` (the ledger's own reconcile hook) checks that a "done" draw's recorded output id still
exists in the CURRENT on-disk partition file, carrying the SAME `class`/`implicitFamily` the ledger
recorded — a hand edit that renamed, deleted, or reclassified the entry resurfaces the draw as
needing work rather than being trusted forever.
"""
from __future__ import annotations

import argparse
import json
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

from seedsmith.pipeline.run_ledger import RunLedger

from . import brief as brief_mod
from . import emit as emit_mod
from . import tuning

REPO_ROOT = tuning.REPO_ROOT
DEFAULT_LEDGER_PATH = REPO_ROOT / "data" / "seed" / "items" / "_runs" / \
    "base-types-gen.ledger.json"


def _partition_file(role: str, frame: str, band: str, *,
                    base_types_dir: "Path | None" = None) -> Path:
    directory = base_types_dir or tuning.BASE_TYPES_DIR
    return directory / f"{frame}-{role}-{band}.json"


def load_existing(role: str, frame: str, band: str, *,
                  base_types_dir: "Path | None" = None) -> "dict[str, dict]":
    p = _partition_file(role, frame, band, base_types_dir=base_types_dir)
    if not p.exists():
        return {}
    doc = json.loads(p.read_text(encoding="utf-8"))
    return {e["id"]: e for e in doc.get("entries", [])}


def _draw_prefix(role: str, frame: str, band: str) -> str:
    return f"basetype-draw-{role}-{frame}-{band}-"


@dataclass(frozen=True)
class Subject:
    subject_id: str
    seq: int
    brief: str
    schema: dict


@dataclass(frozen=True)
class RunPlan:
    partition: "brief_mod.PartitionContext"
    subjects: "tuple[Subject, ...]"
    existing: "dict[str, dict]"

    @property
    def complete(self) -> bool:
        return True  # open-ended content: a plan for N new draws is always "complete" for that N

    def summary(self) -> dict:
        return {"partition": self.partition.partition_key, "toGenerate": len(self.subjects),
                "existingEntries": len(self.existing)}


def _seq_from_subject(subject_id: str, prefix: str) -> "int | None":
    if not subject_id.startswith(prefix):
        return None
    suffix = subject_id[len(prefix):]
    return int(suffix) if suffix.isdigit() else None


def _next_seq_from_ledger(done: "dict[str, dict]", prefix: str) -> int:
    indices = [i for sid in done if (i := _seq_from_subject(sid, prefix)) is not None]
    return (max(indices) + 1) if indices else 0


def is_valid(subject_id: str, entry: dict, *, existing: "dict[str, dict]") -> bool:
    entry_id = entry.get("entryId")
    if not entry_id or entry_id not in existing:
        return False
    real = existing[entry_id]
    return (real.get("class") == entry.get("class")
            and real.get("implicit", {}).get("family") == entry.get("implicitFamily"))


def plan_run(*, role: str, frame: str, band: str, count: int, ledger: RunLedger,
            base_types_dir: "Path | None" = None, theme_hint: str = "") -> RunPlan:
    if count < 1:
        raise ValueError(f"count must be >= 1, got {count}")
    brief_obj0 = brief_mod.build_base_type_brief(role, frame, band, theme_note=theme_hint,
                                                 base_types_dir=base_types_dir)
    partition = brief_obj0.partition
    existing = load_existing(role, frame, band, base_types_dir=base_types_dir)
    done = ledger.read_done()
    prefix = _draw_prefix(role, frame, band)
    start_draw = _next_seq_from_ledger(done, prefix)
    # The entry's own minted-id sequence continues past the real corpus, never the draw counter —
    # the two are independent numbers (a draw may be blocked and mint nothing).
    start_entry_seq = emit_mod.next_seq(tuple(existing))

    subjects = []
    for i in range(count):
        draw_index = start_draw + i
        b = brief_mod.build_base_type_brief(role, frame, band, theme_note=theme_hint,
                                            base_types_dir=base_types_dir)
        subjects.append(Subject(
            subject_id=f"{prefix}{draw_index:03d}", seq=start_entry_seq + i,
            brief=b.render(), schema=dict(b.schema)))

    return RunPlan(partition=partition, subjects=tuple(subjects), existing=existing)


def resolve_answer(answer: dict, *, partition: "brief_mod.PartitionContext", seq: int,
                   gen_tuning: "tuning.GenTuning | None" = None) -> "dict | None":
    """One model answer -> one finished entry dict, or `None` if the model set `blocked`."""
    if answer.get("blocked"):
        return None
    return emit_mod.assemble_entry(answer, partition, seq=seq, gen_tuning=gen_tuning)


def run_draws(plan: RunPlan, *, ledger: RunLedger,
             call: "Callable[[str, dict], dict]") -> "tuple[dict[str, dict], dict[str, dict]]":
    """Executes every subject in `plan`, marking each resolved draw done in `ledger` as it
    completes — not all-or-nothing, mirroring `milestonegen.run.run_draws`."""
    gt = tuning.load_gen_tuning()
    existing = dict(plan.existing)
    fresh: "dict[str, dict]" = {}
    blocked: "dict[str, dict]" = {}

    for subject in plan.subjects:
        answer = call(subject.brief, subject.schema)
        if answer.get("blocked"):
            blocked[subject.subject_id] = {"reason": answer["blocked"]}
            continue
        entry = resolve_answer(answer, partition=plan.partition, seq=subject.seq, gen_tuning=gt)
        if entry is None:
            blocked[subject.subject_id] = {"reason": "no reason given"}
            continue
        fresh[entry["id"]] = entry
        existing[entry["id"]] = entry
        ledger.mark_done(subject.subject_id, {
            "entryId": entry["id"], "class": entry["class"],
            "implicitFamily": entry["implicit"]["family"],
        })

    return fresh, blocked


def write_corpus(role: str, frame: str, band: str, fresh: "dict[str, dict]", *,
                 existing: "dict[str, dict] | None" = None,
                 base_types_dir: "Path | None" = None,
                 source_ref: str = "", model: str = "seedsmith-basetypegen",
                 authored_utc: str = "") -> Path:
    """Merges `fresh` into the existing partition file and writes it back — additive only, matching
    `spec-base-types-gen.md` acceptance #2's 'append+reconcile by default'."""
    p = _partition_file(role, frame, band, base_types_dir=base_types_dir)
    ex = existing if existing is not None else load_existing(role, frame, band,
                                                             base_types_dir=base_types_dir)
    merged = {**ex, **fresh}
    entries = [merged[k] for k in sorted(merged)]
    if p.exists():
        doc = json.loads(p.read_text(encoding="utf-8"))
        doc["entries"] = entries
    else:
        doc = emit_mod.emit_document(
            entries, batch=f"base-types-gen-{role}-{frame}-{band}",
            partition=f"base-types/{frame}-{role}-{band}", source_ref=source_ref, model=model,
            authored_utc=authored_utc)
    return emit_mod.write_document(p, doc)


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Author base-type identities for one (role, frame, "
                                             "band) partition.")
    ap.add_argument("--role", required=True, help="one of core.v1.json's 15 body role ids")
    ap.add_argument("--frame", required=True, choices=list(tuning.FRAMES))
    ap.add_argument("--band", required=True, help="the partition's band letter, e.g. 'a' or 'b'")
    ap.add_argument("--dry-run", action="store_true", help="assemble briefs, make no model calls")
    ap.add_argument("--count", type=int, default=1, help="how many new entries to draw this run")
    ap.add_argument("--theme", default="", help="an optional theme hint in the brief")
    ap.add_argument("--write", action="store_true", help="write the merged partition back to disk")
    ap.add_argument("--overwrite", "--force", default="",
                    help="comma-separated draw ids to regenerate, or the literal 'all' "
                         "(--force is an accepted alias, matching content-completeness-core's "
                         "own naming convention -- RunLedger.force()/generate_commander_effects.py "
                         "--force)")
    ap.add_argument("--endpoint", default="", help="live model endpoint; enables a real run")
    ap.add_argument("--model", default="", help="overrides load_config()'s own model for this run")
    ap.add_argument("--authored-utc", default="", dest="authored_utc",
                    help="stamped into a NEW partition file's _meta.authoredUtc; an existing "
                         "partition keeps its own")
    args = ap.parse_args(argv)

    ledger = RunLedger(DEFAULT_LEDGER_PATH)

    if args.dry_run:
        plan = plan_run(role=args.role, frame=args.frame, band=args.band, count=args.count,
                        ledger=ledger, theme_hint=args.theme)
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

    if not args.write:
        raise SystemExit(
            "seedsmith: refused — no --write. Use --dry-run to inspect the plan first, "
            "then re-run with --write --endpoint <url> to actually call a model and persist.")
    if not args.endpoint:
        raise SystemExit(
            "seedsmith: --write refused — no --endpoint. A real run needs a live model "
            "(--endpoint <url> [--model <name>]); --dry-run needs neither.")

    # ⛔ Real gap, closed 2026-09-08: this branch used to be an unconditional `raise SystemExit`
    # ("REFUSING TO RUN: no model call is wired into this CLI entrypoint yet") — `plan_run`/
    # `run_draws`/`write_corpus` were all real and tested, but nothing in this file ever built the
    # `call` `run_draws` already declares and tests against. `live_answer_caller` is that piece.
    import dataclasses

    from ....pipeline.llm_caller import live_answer_caller, load_config

    base_config = load_config()
    config = dataclasses.replace(base_config, endpoint=args.endpoint,
                                 model=args.model or base_config.model)
    plan = plan_run(role=args.role, frame=args.frame, band=args.band, count=args.count,
                    ledger=ledger, theme_hint=args.theme)
    fresh, blocked = run_draws(plan, ledger=ledger, call=live_answer_caller(config))
    if fresh:
        write_corpus(args.role, args.frame, args.band, fresh, existing=plan.existing,
                    model=config.model, authored_utc=args.authored_utc)
    print(json.dumps({"planned": len(plan.subjects), "fresh": len(fresh),
                      "blocked": len(blocked), "blockedReasons": blocked},
                     ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":  # pragma: no cover - dev entrypoint
    raise SystemExit(main())
