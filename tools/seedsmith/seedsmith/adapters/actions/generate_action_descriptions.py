"""seedsmith.adapters.actions.generate_action_descriptions — actions' own resumable backfill
entrypoint (`spec-content-completeness-actions.md` §4, `seedsmith-content-standard` Task 8).

Reads the real committed corpus via A-C1's own `load_committed` (excludes `_rounds/` — the same
loader `generate_innate_picker.py` already uses, `../load.py`), plans work with the shared
`pipeline.backfill` contract (missing-only automatic; `--force` for a manual full regenerate — the
resolved automatic-backfill contract, spec-content-completeness-core.md §3), calls a local model
for each subject needing one, and writes `description` + `_provenance` straight back into whichever
committed file (`committed-round-<n>.json`) each action actually lives in.

**Ledger, not `_manifest.json`/`_rounds/` — the two are orthogonal, not a replacement.**
`_manifest.json`/`_rounds/` (`load.py`) decide what enters the CORPUS GRAPH at all (pre-commit
staging vs. committed content); the new `RunLedger` here (`data/seed/actions/_runs/
description-backfill.ledger.json`) tracks a completely different, narrower question — "does this
ALREADY-COMMITTED id have a description yet" — keyed by committed action ids the loader has
already accepted. Neither could do the other's job: the loader has no notion of "half-finished
content on an accepted row," and the ledger has no notion of "this row is still pre-acceptance
review, not real yet." `spec-content-completeness-actions.md` §3 names this explicitly.

**Zero model calls in `--dry-run`** (`generate_commander_effects.py`'s own `--dry-run` precedent):
prints the plan and returns without opening a connection.
"""
from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Callable

from .description_backfill.derive import (
    apply_updates_to_doc, canonical_dump, group_ids_by_path, stamp_description,
)
from .description_backfill.prompts import (
    DESCRIPTION_SCHEMA, PROMPT_VERSION, SCHEMA_VERSION, SYSTEM_PROMPT, build_brief,
)
from .load import load_committed
from ...pipeline.backfill import force_regenerate, plan_missing
from ...pipeline.llm_caller import LlmCallerConfig, call_model, extract_json
from ...pipeline.provenance import Provenance
from ...pipeline.run_ledger import RunLedger
from ...pipeline.staleness import brief_hash as hash_brief
from ...pipeline.staleness import staleness_key

__all__ = ["run", "plan", "backfill", "ACTIONS_ROOT", "LEDGER_PATH"]

REPO_ROOT = Path(__file__).resolve().parents[5]
ACTIONS_ROOT = REPO_ROOT / "data" / "seed" / "actions"
LEDGER_PATH = ACTIONS_ROOT / "_runs" / "description-backfill.ledger.json"

#: `finding` field of the stamped `Provenance` (`pipeline/provenance.py`'s own "why does this row
#: exist" contract) — the real metric+subject pair that would report a gap for this row today
#: (`ContentFieldMissing`'s own `f"{spec.domain}:{kind}"` subject shape, `metrics/
#: content_completeness.py:92`), not a per-row id: the metric itself only ever reports per-kind
#: aggregates, so this is the most specific truthful value available.
FINDING = "Content/FieldMissing:actions:action-seed"


def _utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def plan(*, actions_root: Path = ACTIONS_ROOT, ledger_path: Path = LEDGER_PATH,
        force: bool = False, only: "tuple[str, ...] | None" = None) -> "list[str]":
    """The automatic-vs-manual split (spec §3), computed with zero model calls — split out from
    `backfill` so a caller (or a test) can see exactly what would run before any call is made."""
    load_result = load_committed(actions_root)
    all_ids = sorted(e.id for e in load_result.corpus.by_kind("action-seed"))
    target_ids = [i for i in only if i in all_ids] if only else all_ids
    ledger = RunLedger(ledger_path)
    if force:
        return force_regenerate(ledger, target_ids, scope="ids" if only else "all")
    return plan_missing(ledger, target_ids)


def backfill(*, actions_root: Path = ACTIONS_ROOT, ledger_path: Path = LEDGER_PATH,
            config: LlmCallerConfig, force: bool = False, only: "tuple[str, ...] | None" = None,
            dry_run: bool = False, now_fn: Callable[[], str] = _utc_now) -> dict:
    """The real generation pass. Pure enough to unit-test the planning half (`plan`, above)
    without a model; this function is the one that actually calls one and writes to disk, so its
    own tests are the live-corpus proof this task's acceptance asks for, not a synthetic fixture.
    """
    load_result = load_committed(actions_root)
    entries_by_id = {e.id: e for e in load_result.corpus.by_kind("action-seed")}
    to_generate = plan(actions_root=actions_root, ledger_path=ledger_path, force=force, only=only)

    if dry_run:
        return {"planned": to_generate, "dryRun": True, "generated": []}

    ledger = RunLedger(ledger_path)
    updates_by_id: "dict[str, dict]" = {}
    generated: "list[str]" = []
    for action_id in to_generate:
        entry = entries_by_id[action_id]
        brief = build_brief(entry.data)
        raw = call_model(SYSTEM_PROMPT, brief, config=config, schema=DESCRIPTION_SCHEMA)
        description = str(extract_json(raw)["description"]).strip()
        if not description:
            # A blank model answer is a real generation defect, not a value to persist — leaving
            # the ledger untouched means the NEXT automatic run retries this id rather than
            # silently accepting an empty string as "done" forever.
            continue

        current_key = staleness_key(brief_hash=hash_brief(brief), prompt_version=PROMPT_VERSION,
                                    schema_version=SCHEMA_VERSION, model_id=config.model)
        generated_utc = now_fn()
        prov = Provenance(pipeline="seedsmith.adapters.actions.description_backfill",
                          model=config.model, prompt_version=PROMPT_VERSION,
                          # Actions carries no numeric budget concept (unlike items' tier-band
                          # budgets) — 0 is a structural placeholder for a required dataclass
                          # field this domain does not use, not a magnitude or a balance number.
                          budget_version=0, finding=FINDING, generated_utc=generated_utc)
        provenance_dict = {**prov.to_dict(), "stalenessKey": current_key}
        updates_by_id[action_id] = stamp_description(
            entry.data, description=description, provenance=provenance_dict)
        ledger.mark_done(action_id, {"promptVersion": PROMPT_VERSION,
                                     "schemaVersion": SCHEMA_VERSION, "model": config.model,
                                     "generatedUtc": generated_utc})
        generated.append(action_id)

    if updates_by_id:
        by_path = group_ids_by_path([entries_by_id[i] for i in updates_by_id])
        for rel_path, ids_in_file in by_path.items():
            file_path = actions_root / rel_path
            doc = json.loads(file_path.read_text(encoding="utf-8"))
            file_updates = {i: updates_by_id[i] for i in ids_in_file if i in updates_by_id}
            new_doc = apply_updates_to_doc(doc, file_updates)
            file_path.write_text(canonical_dump(new_doc), encoding="utf-8")

    return {"planned": to_generate, "dryRun": False, "generated": generated}


def run(argv=None) -> int:
    ap = argparse.ArgumentParser(
        description="Backfill `description` onto committed action-seed rows missing one.")
    ap.add_argument("--dry-run", action="store_true", help="print the plan, call no model")
    ap.add_argument("--force", action="store_true",
                    help="regenerate every targeted id even if it already has a description")
    ap.add_argument("--only", default="", help="comma-separated action ids to target")
    ap.add_argument("--endpoint", default="http://localhost:1234/v1/chat/completions")
    ap.add_argument("--model", default="google/gemma-4-26b-a4b-qat")
    args = ap.parse_args(argv)

    only = tuple(x.strip() for x in args.only.split(",") if x.strip()) or None
    config = LlmCallerConfig(endpoint=args.endpoint, model=args.model)

    result = backfill(force=args.force, only=only, dry_run=args.dry_run, config=config)
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


def main(argv=None) -> int:
    return run(argv)


if __name__ == "__main__":  # pragma: no cover - dev entrypoint
    raise SystemExit(main())
